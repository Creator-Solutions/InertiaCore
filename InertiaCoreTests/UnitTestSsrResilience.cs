using System.Diagnostics;
using System.Net;
using System.Text;
using InertiaCore;
using InertiaCore.Extensions;
using InertiaCore.Models;
using InertiaCore.Services;
using InertiaCore.Ssr;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InertiaCoreTests;

/// <summary>
/// Unit tests for the SSR resilience pipeline (IN-69): the typed client wrapped in the
/// standard resilience handler (total timeout, retry with exponential backoff and jitter,
/// circuit breaker and attempt timeout). The real pipeline from <c>AddInertia()</c> is
/// exercised with a stubbed primary message handler.
/// </summary>
[TestFixture]
public class UnitTestSsrResilience
{
    private const string SsrUrl = "http://ssr.test/render";

    [Test]
    [Description("SC: a slow SSR server triggers the timeout and falls back within the configured budget.")]
    public async Task Dispatch_SlowServer_TimesOutWithinBudget()
    {
        var handler = new StubHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return Ok();
        });

        var (gateway, _, provider) = CreateGateway(handler, o =>
        {
            o.SsrTimeout = TimeSpan.FromMilliseconds(300);
        });
        using var _ = provider;

        var stopwatch = Stopwatch.StartNew();
        var result = await gateway.Dispatch(new { Test = "Test" }, SsrUrl);
        stopwatch.Stop();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Null, "a timed-out dispatch falls back to client-side rendering");
            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2)),
                "the fallback happened within the configured budget");
            Assert.That(handler.Calls, Is.EqualTo(1), "timeouts are not retried");
        });
    }

    [Test]
    [Description("SC: transient transport failures are retried with jittered backoff and then succeed.")]
    public async Task Dispatch_TransientFailures_AreRetriedThenSucceed()
    {
        var calls = 0;
        var handler = new StubHandler((_, _) =>
        {
            if (Interlocked.Increment(ref calls) <= 2)
                throw new HttpRequestException("connection reset");
            return Task.FromResult(Ok());
        });

        var (gateway, _, provider) = CreateGateway(handler, o =>
        {
            o.SsrResilience.MaxRetryAttempts = 2;
            o.SsrResilience.RetryBaseDelay = TimeSpan.FromMilliseconds(10);
        });
        using var _ = provider;

        var result = await gateway.Dispatch(new { Test = "Test" }, SsrUrl);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null, "the third attempt succeeds");
            Assert.That(result!.Body, Is.EqualTo("<p>ok</p>"));
            Assert.That(handler.Calls, Is.EqualTo(3), "two failures were retried once each");
        });
    }

    [Test]
    [Description("SC: the circuit opens after the configured failures and subsequent calls fail fast without waiting.")]
    public async Task Dispatch_CircuitOpens_FailsFastWithoutWaiting()
    {
        var handler = new StubHandler((_, _) => throw new HttpRequestException("connection refused"));

        var (gateway, _, provider) = CreateGateway(handler, OpenCircuit);
        using var _ = provider;

        // The first dispatch makes two attempts (one retry), which trips the circuit.
        Assert.That(await gateway.Dispatch(new { Test = "Test" }, SsrUrl), Is.Null);
        var callsWhenOpen = handler.Calls;

        var stopwatch = Stopwatch.StartNew();
        var result = await gateway.Dispatch(new { Test = "Test" }, SsrUrl);
        stopwatch.Stop();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Null, "an open circuit still falls back to client-side rendering");
            Assert.That(handler.Calls, Is.EqualTo(callsWhenOpen), "an open circuit does not contact the SSR server");
            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromMilliseconds(500)),
                "an open circuit fails fast without waiting");
        });
    }

    [Test]
    [Description("SC: after the break duration a trial call is let through; success closes the circuit.")]
    public async Task Dispatch_CircuitRecovers_AfterBreakDuration()
    {
        var healthy = false;
        var handler = new StubHandler((_, _) => healthy
            ? Task.FromResult(Ok())
            : throw new HttpRequestException("connection refused"));

        var (gateway, _, provider) = CreateGateway(handler, o =>
        {
            OpenCircuit(o);
            o.SsrResilience.CircuitBreakerBreakDuration = TimeSpan.FromMilliseconds(500);
        });
        using var _ = provider;

        Assert.That(await gateway.Dispatch(new { Test = "Test" }, SsrUrl), Is.Null);

        await Task.Delay(700); // let the break duration elapse
        healthy = true;

        var result = await gateway.Dispatch(new { Test = "Test" }, SsrUrl);

        Assert.That(result, Is.Not.Null, "a successful trial call closes the circuit");
        Assert.That(result!.Body, Is.EqualTo("<p>ok</p>"));
    }

    [Test]
    [Description("SC: an open circuit makes Html() render the client-side script without contacting the SSR server.")]
    public async Task Html_CircuitOpen_RendersClientSide()
    {
        var handler = new StubHandler((_, _) => throw new HttpRequestException("connection refused"));

        var (gateway, _, provider) = CreateGateway(handler, OpenCircuit);
        using var _ = provider;

        // Trip the circuit with one dispatch (two attempts, one retry).
        await gateway.Dispatch(new { Test = "Test" }, SsrUrl);
        var callsWhenOpen = handler.Calls;

        var factory = new ResponseFactory(
            new HttpContextAccessor(),
            gateway,
            provider.GetRequiredService<IOptions<InertiaOptions>>(),
            new InertiaState(),
            Options.Create(new JsonOptions()));

        var html = await factory.Html(new { Test = "Test" });

        Assert.Multiple(() =>
        {
            Assert.That(html.ToString(), Does.StartWith("<script data-page=\"app\""),
                "the response degrades to the client-side script tag");
            Assert.That(handler.Calls, Is.EqualTo(callsWhenOpen), "no SSR request was sent while the circuit was open");
        });
    }

    private static void OpenCircuit(InertiaOptions o)
    {
        o.SsrResilience.MaxRetryAttempts = 1;
        o.SsrResilience.CircuitBreakerMinimumThroughput = 2;
        o.SsrResilience.CircuitBreakerFailureRatio = 1.0;
        o.SsrResilience.CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(30);
        o.SsrResilience.CircuitBreakerBreakDuration = TimeSpan.FromSeconds(30);
    }

    private static (IGateway Gateway, StubHandler Handler, ServiceProvider Provider) CreateGateway(
        StubHandler handler,
        Action<InertiaOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddInertia(options =>
        {
            options.SsrEnabled = true;
            options.SsrUrl = SsrUrl;
            configure(options);
        });

        services.AddHttpClient(InertiaSsrClient.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IGateway>(), handler, provider);
    }

    private static HttpResponseMessage Ok() =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"head\":[\"<title>x</title>\"],\"body\":\"<p>ok</p>\"}",
                Encoding.UTF8,
                "application/json")
        };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;
        private int _calls;

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) =>
            _handler = handler;

        public int Calls => Volatile.Read(ref _calls);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            return _handler(request, cancellationToken);
        }
    }
}
