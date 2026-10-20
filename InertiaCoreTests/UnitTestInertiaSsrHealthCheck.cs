using System.Net;
using InertiaCore.Extensions;
using InertiaCore.Models;
using InertiaCore.Ssr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace InertiaCoreTests;

/// <summary>
/// Unit tests for <see cref="InertiaSsrHealthCheck"/> (IN-69): Healthy when SSR is disabled
/// or the server answers, Degraded when the circuit breaker is open and Unhealthy when the
/// SSR server cannot be reached.
/// </summary>
[TestFixture]
public class UnitTestInertiaSsrHealthCheck
{
    private const string DerivedHealthUrl = "http://127.0.0.1:13714/health";

    [Test]
    [Description("SC: with SSR disabled the check reports Healthy without probing.")]
    public void SsrDisabled_ReportsHealthy()
    {
        var handler = new StubHandler((_, _) => throw new HttpRequestException("should not be called"));
        var check = new InertiaSsrHealthCheck(
            new StubHttpClientFactory(handler),
            Options.Create(new InertiaOptions()));

        var result = check.CheckHealthAsync(new HealthCheckContext()).GetAwaiter().GetResult();

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(HealthStatus.Healthy));
            Assert.That(result.Description, Does.Contain("disabled"));
            Assert.That(handler.Calls, Is.EqualTo(0), "no probe is made when SSR is disabled");
        });
    }

    [Test]
    [Description("SC: a reachable SSR server reports Healthy, probing the health URL derived from SsrUrl.")]
    public async Task ServerReachable_ReportsHealthy_UsesDerivedHealthUrl()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var check = new InertiaSsrHealthCheck(
            new StubHttpClientFactory(handler),
            Options.Create(SsrEnabled()));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(HealthStatus.Healthy));
            Assert.That(handler.LastRequestUri, Is.EqualTo(DerivedHealthUrl), "the health URL is derived from SsrUrl");
        });
    }

    [Test]
    [Description("SC: an explicit SsrHealthUrl takes precedence over the derived URL.")]
    public async Task ExplicitHealthUrl_IsUsed()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var options = SsrEnabled(o => o.SsrHealthUrl = "http://127.0.0.1:39999/custom-health");
        var check = new InertiaSsrHealthCheck(new StubHttpClientFactory(handler), Options.Create(options));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(HealthStatus.Healthy));
            Assert.That(handler.LastRequestUri, Is.EqualTo("http://127.0.0.1:39999/custom-health"));
        });
    }

    [Test]
    [Description("SC: an unreachable SSR server reports Unhealthy.")]
    public async Task ServerUnreachable_ReportsUnhealthy()
    {
        var handler = new StubHandler((_, _) => throw new HttpRequestException("connection refused"));
        var check = new InertiaSsrHealthCheck(
            new StubHttpClientFactory(handler),
            Options.Create(SsrEnabled()));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.That(result.Status, Is.EqualTo(HealthStatus.Unhealthy));
    }

    [Test]
    [Description("SC: a non-success health response reports Unhealthy.")]
    public async Task NonSuccessStatus_ReportsUnhealthy()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        var check = new InertiaSsrHealthCheck(
            new StubHttpClientFactory(handler),
            Options.Create(SsrEnabled()));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.That(result.Status, Is.EqualTo(HealthStatus.Unhealthy));
    }

    [Test]
    [Description("SC: an invalid SsrHealthUrl reports Unhealthy.")]
    public async Task InvalidHealthUrl_ReportsUnhealthy()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var options = SsrEnabled(o => o.SsrHealthUrl = "file:///etc/passwd");
        var check = new InertiaSsrHealthCheck(new StubHttpClientFactory(handler), Options.Create(options));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(HealthStatus.Unhealthy));
            Assert.That(handler.Calls, Is.EqualTo(0), "no probe is made with an invalid URL");
        });
    }

    [Test]
    [Description("SC: an open circuit breaker reports Degraded and fails fast without probing.")]
    public async Task CircuitOpen_ReportsDegraded_Fast()
    {
        var handler = new StubHandler((_, _) => throw new HttpRequestException("connection refused"));

        var services = new ServiceCollection();
        services.AddInertia(options =>
        {
            options.SsrEnabled = true;
            options.SsrUrl = "http://127.0.0.1:13714/render";
            options.SsrResilience.MaxRetryAttempts = 1;
            options.SsrResilience.CircuitBreakerMinimumThroughput = 2;
            options.SsrResilience.CircuitBreakerFailureRatio = 1.0;
            options.SsrResilience.CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(30);
            options.SsrResilience.CircuitBreakerBreakDuration = TimeSpan.FromSeconds(30);
        });
        services.AddHttpClient(InertiaSsrClient.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        await using var provider = services.BuildServiceProvider();

        // Trip the circuit: the first dispatch makes two attempts (one retry).
        var gateway = provider.GetRequiredService<IGateway>();
        await gateway.Dispatch(new { Test = "Test" }, "http://127.0.0.1:13714/render");
        var callsWhenOpen = handler.Calls;

        var check = new InertiaSsrHealthCheck(
            provider.GetRequiredService<IHttpClientFactory>(),
            provider.GetRequiredService<IOptions<InertiaOptions>>());

        var started = System.Diagnostics.Stopwatch.StartNew();
        var result = await check.CheckHealthAsync(new HealthCheckContext());
        started.Stop();

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(HealthStatus.Degraded), "an open circuit is Degraded");
            Assert.That(handler.Calls, Is.EqualTo(callsWhenOpen), "the probe does not contact the server while open");
            Assert.That(started.Elapsed, Is.LessThan(TimeSpan.FromMilliseconds(500)), "Degraded is reported without waiting");
        });
    }

    private static InertiaOptions SsrEnabled(Action<InertiaOptions>? configure = null)
    {
        var options = new InertiaOptions
        {
            SsrEnabled = true,
            SsrUrl = "http://127.0.0.1:13714/render"
        };
        configure?.Invoke(options);
        return options;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;
        private int _calls;
        private Uri? _lastRequestUri;

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) =>
            _handler = handler;

        public int Calls => Volatile.Read(ref _calls);

        public string? LastRequestUri => _lastRequestUri?.ToString();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            _lastRequestUri = request.RequestUri;
            return _handler(request, cancellationToken);
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public StubHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }
}
