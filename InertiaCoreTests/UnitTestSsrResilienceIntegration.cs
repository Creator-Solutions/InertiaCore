using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InertiaCoreTests;

/// <summary>
/// End-to-end integration tests for the SSR resilience pipeline (IN-69). The TestApp host
/// is pointed at an in-test Kestrel sidecar on a loopback port, which can be made slow,
/// failing or healthy on demand, so the total-timeout budget, the circuit breaker's
/// fail-fast and recovery, and the Healthy/Degraded/Unhealthy health states are exercised
/// through the real standard resilience handler.
/// </summary>
[TestFixture]
public class UnitTestSsrResilienceIntegration
{
    [Test]
    [Description("SC: a slow SSR server trips the total timeout and the page falls back to client-side rendering within the budget.")]
    public async Task SlowSsrServer_TimesOut_FallsBackWithinBudget()
    {
        await using var sidecar = await Sidecar.StartAsync();
        sidecar.RenderBehavior = async context =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), context.RequestAborted);
            return Results.Ok();
        };

        using var factory = new ResilienceFactory(new Dictionary<string, string>
        {
            ["SsrResilience:SsrUrl"] = sidecar.RenderUrl,
            ["SsrResilience:TimeoutSeconds"] = "1",
            ["SsrResilience:MaxRetryAttempts"] = "1",
            ["SsrResilience:RetryBaseDelayMs"] = "50",
        });
        using var client = factory.CreateClient();

        var stopwatch = Stopwatch.StartNew();
        var response = await client.GetAsync("/ssr/html");
        stopwatch.Stop();
        var html = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccessStatusCode, Is.True);
            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)),
                "the fallback happened within the configured 1s budget");
            Assert.That(html, Does.StartWith("<script data-page=\"app\""),
                "a timed-out dispatch degrades to the client-side script tag");
            Assert.That(html, Does.Not.Contain("<p>ssr</p>"), "no SSR body was rendered");
        });
    }

    [Test]
    [Description("SC: failing responses trip the circuit breaker; subsequent calls fail fast without contacting the server; after the break a healthy server closes the circuit.")]
    public async Task FailingSsrServer_TripsCircuit_FailsFast_ThenRecovers()
    {
        await using var sidecar = await Sidecar.StartAsync();
        sidecar.RenderBehavior = _ => Task.FromResult(Results.StatusCode(500));

        using var factory = new ResilienceFactory(new Dictionary<string, string>
        {
            ["SsrResilience:SsrUrl"] = sidecar.RenderUrl,
            ["SsrResilience:TimeoutSeconds"] = "5",
            ["SsrResilience:MaxRetryAttempts"] = "1",
            ["SsrResilience:RetryBaseDelayMs"] = "50",
            ["SsrResilience:MinimumThroughput"] = "2",
            ["SsrResilience:FailureRatio"] = "1",
            ["SsrResilience:SamplingDurationSeconds"] = "10",
            ["SsrResilience:BreakDurationSeconds"] = "1",
        });
        using var client = factory.CreateClient();

        // First dispatch: one retry makes two failed attempts, which trips the breaker.
        var first = await client.GetAsync("/ssr/html");
        var firstHtml = await first.Content.ReadAsStringAsync();

        Assert.That(firstHtml, Does.StartWith("<script data-page=\"app\""),
            "the failing server falls back to client-side rendering");
        Assert.That(sidecar.RenderCalls, Is.EqualTo(2), "the transient failure was retried once");
        Assert.That(sidecar.BreakerTripped, Is.True, "the circuit breaker opened after two failures");

        // Fail fast: the open circuit must not contact the SSR server at all.
        var stopwatch = Stopwatch.StartNew();
        var second = await client.GetAsync("/ssr/html");
        stopwatch.Stop();
        var secondHtml = await second.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(secondHtml, Does.StartWith("<script data-page=\"app\""),
                "an open circuit still degrades to client-side rendering");
            Assert.That(sidecar.RenderCalls, Is.EqualTo(2),
                "an open circuit does not contact the SSR server");
            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromMilliseconds(500)),
                "the open circuit fails fast without waiting");
        });

        // Recover: heal the server, poll until the trial call closes the circuit.
        sidecar.RenderBehavior = null;

        var recovered = false;
        string? recoveredHtml = null;
        for (var i = 0; i < 12 && !recovered; i++)
        {
            var resp = await client.GetAsync("/ssr/html");
            var bodyText = await resp.Content.ReadAsStringAsync();
            recovered = bodyText.Contains("<p>ssr</p>");
            recoveredHtml = bodyText;
            if (!recovered) await Task.Delay(250);
        }

        Assert.Multiple(() =>
        {
            Assert.That(recovered, Is.True, "the circuit closed after the break duration elapsed");
            Assert.That(recoveredHtml, Does.Contain("<p>ssr</p>"),
                "a successful trial call closes the circuit and renders server-side again");
            Assert.That(sidecar.RenderCalls, Is.EqualTo(3),
                "exactly the trial call reached the healed server");
        });
    }

    [Test]
    [Description("SC: with SSR enabled and the server answering, the health check reports Healthy.")]
    public async Task HealthCheck_ServerAnswers_ReportsHealthy()
    {
        await using var sidecar = await Sidecar.StartAsync();

        using var factory = new ResilienceFactory(new Dictionary<string, string>
        {
            ["SsrResilience:SsrUrl"] = sidecar.RenderUrl,
            ["SsrResilience:MaxRetryAttempts"] = "1",
            ["SsrResilience:RetryBaseDelayMs"] = "50",
            ["UseHealthChecks"] = "true",
        });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body, Does.Contain("Healthy"));
        });
    }

    [Test]
    [Description("SC: with the SSR server unreachable, the health check reports Unhealthy (503).")]
    public async Task HealthCheck_ServerUnreachable_ReportsUnhealthy()
    {
        await using var sidecar = await Sidecar.StartAsync();
        var renderUrl = sidecar.RenderUrl;
        await sidecar.DisposeAsync();

        using var factory = new ResilienceFactory(new Dictionary<string, string>
        {
            ["SsrResilience:SsrUrl"] = renderUrl,
            ["SsrResilience:MaxRetryAttempts"] = "1",
            ["SsrResilience:RetryBaseDelayMs"] = "50",
            ["SsrResilience:MinimumThroughput"] = "10",
            ["UseHealthChecks"] = "true",
        });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
            Assert.That(body, Does.Contain("Unhealthy"));
        });
    }

    [Test]
    [Description("SC: with the circuit breaker open, the health check reports Degraded (200) without probing the server.")]
    public async Task HealthCheck_CircuitOpen_ReportsDegradedWithoutProbing()
    {
        await using var sidecar = await Sidecar.StartAsync();
        sidecar.RenderBehavior = _ => Task.FromResult(Results.StatusCode(500));

        using var factory = new ResilienceFactory(new Dictionary<string, string>
        {
            ["SsrResilience:SsrUrl"] = sidecar.RenderUrl,
            ["SsrResilience:TimeoutSeconds"] = "5",
            ["SsrResilience:MaxRetryAttempts"] = "1",
            ["SsrResilience:RetryBaseDelayMs"] = "50",
            ["SsrResilience:MinimumThroughput"] = "2",
            ["SsrResilience:FailureRatio"] = "1",
            ["SsrResilience:SamplingDurationSeconds"] = "10",
            ["SsrResilience:BreakDurationSeconds"] = "30",
            ["UseHealthChecks"] = "true",
        });
        using var client = factory.CreateClient();

        // Trip the breaker: one dispatch, two failed attempts.
        var render = await client.GetAsync("/ssr/html");
        Assert.That(sidecar.BreakerTripped, Is.True, "the circuit breaker opened");
        Assert.That(render.IsSuccessStatusCode, Is.True);

        var healthCallsWhenOpen = sidecar.HealthCalls;
        var stopwatch = Stopwatch.StartNew();
        var response = await client.GetAsync("/health");
        stopwatch.Stop();
        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body, Does.Contain("Degraded"), "an open circuit is Degraded");
            Assert.That(sidecar.HealthCalls, Is.EqualTo(healthCallsWhenOpen),
                "the health check does not probe the server while the circuit is open");
            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromMilliseconds(500)),
                "Degraded is reported fail-fast");
        });
    }

    /// <summary>
    /// A minimal Kestrel host on a loopback port with a configurable /render endpoint
    /// (Inertia SSR JSON shape by default) and a /health endpoint.
    /// </summary>
    private sealed class Sidecar : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly CancellationTokenSource _cts = new();
        private int _renderCalls;
        private int _healthCalls;
        private bool _disposed;

        private Sidecar(WebApplication app)
        {
            _app = app;
        }

        public int Port { get; private set; }

        public string RenderUrl => $"http://127.0.0.1:{Port}/render";

        public int RenderCalls => Volatile.Read(ref _renderCalls);

        public int HealthCalls => Volatile.Read(ref _healthCalls);

        public Func<HttpContext, Task<IResult>>? RenderBehavior { get; set; }

        /// <summary>True once at least two failed renders have been observed (the breaker's configured threshold).</summary>
        public bool BreakerTripped => Volatile.Read(ref _renderFailures) >= 2;

        private int _renderFailures;

        public static async Task<Sidecar> StartAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.Host.ConfigureHostOptions(o => o.ShutdownTimeout = TimeSpan.FromSeconds(2));
            builder.WebHost.UseUrls("http://127.0.0.1:0");

            var app = builder.Build();
            var sidecar = new Sidecar(app);

            app.MapPost("/render", async (HttpContext context) =>
            {
                Interlocked.Increment(ref sidecar._renderCalls);

                IResult result;
                try
                {
                    result = sidecar.RenderBehavior != null
                        ? await sidecar.RenderBehavior(context)
                        : Ok();
                }
                catch
                {
                    result = Results.StatusCode(500);
                }

                if (result is not StatusCodeResult { StatusCode: StatusCodes.Status200OK })
                    Interlocked.Increment(ref sidecar._renderFailures);

                return result;
            });

            app.MapGet("/health", () =>
            {
                Interlocked.Increment(ref sidecar._healthCalls);
                return Results.Ok();
            });

            await app.StartAsync();

            var address = app.Urls.FirstOrDefault(u => !u.EndsWith(":0"))
                ?? throw new InvalidOperationException(
                    $"The sidecar did not bind a concrete port (Urls: {string.Join(", ", app.Urls)}).");
            sidecar.Port = new Uri(address).Port;

            return sidecar;
        }

        private static IResult Ok() =>
            Results.Json(new { head = Array.Empty<string>(), body = "<p>ssr</p>" });

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            await _cts.CancelAsync();
            _cts.Dispose();

            try
            {
                await _app.StopAsync(CancellationToken.None);
            }
            catch
            {
                // Shutdown races with in-flight cancelled requests; disposal still proceeds.
            }

            await _app.DisposeAsync();
        }
    }

    private sealed class ResilienceFactory : WebApplicationFactory<Program>
    {
        private readonly IReadOnlyDictionary<string, string> _settings;

        public ResilienceFactory(IReadOnlyDictionary<string, string> settings) => _settings = settings;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("UseSsrResilience", "true");
            foreach (var (key, value) in _settings) builder.UseSetting(key, value);
        }
    }
}
