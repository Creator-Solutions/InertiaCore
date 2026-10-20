using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Channels;
using InertiaCore.Models;
using InertiaCore.Ssr;
using InertiaCore.Utils;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InertiaCoreTests;

/// <summary>
/// Unit tests for <see cref="InertiaSsrHostedService"/> using a fake process launcher and a stub
/// HTTP handler, so the start/health/restart/shutdown lifecycle is exercised without Node.
/// </summary>
[TestFixture]
public class UnitTestInertiaSsrHostedService
{
    private static InertiaOptions Managed(Action<InertiaSsrOptions>? configure = null)
    {
        var options = new InertiaOptions { SsrEnabled = true };
        options.Ssr.Managed = true;
        configure?.Invoke(options.Ssr);
        return options;
    }

    private static InertiaSsrHostedService Create(
        FakeLauncher launcher,
        InertiaOptions options,
        IHttpClientFactory? httpClientFactory = null,
        IViteBuilder? vite = null,
        Microsoft.Extensions.Logging.ILogger? logger = null)
    {
        var services = new ServiceCollection();
        if (vite != null) services.AddSingleton(vite);
        else services.AddSingleton<IViteBuilder>(new NeverHotViteBuilder());

        return new InertiaSsrHostedService(
            launcher,
            httpClientFactory ?? new HealthOkFactory(),
            services.BuildServiceProvider(),
            Options.Create(options),
            (Microsoft.Extensions.Logging.ILogger<InertiaSsrHostedService>)(
                logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<InertiaSsrHostedService>.Instance));
    }

    [Test]
    [Description("SC: the service is a no-op when management is disabled.")]
    public async Task NotManaged_DoesNotLaunch()
    {
        var launcher = new FakeLauncher();
        var service = Create(launcher, new InertiaOptions { SsrEnabled = true });

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.That(launcher.Created, Is.Empty);
    }

    [Test]
    [Description("SC: the service does not start when SSR is disabled.")]
    public async Task SsrDisabled_DoesNotLaunch()
    {
        var options = new InertiaOptions { SsrEnabled = false };
        options.Ssr.Managed = true;

        var launcher = new FakeLauncher();
        var service = Create(launcher, options);

        await service.StartAsync(CancellationToken.None);

        Assert.That(launcher.Created, Is.Empty);
    }

    [Test]
    [Description("SC: the service does not start when the Vite dev server is running.")]
    public async Task ViteHot_DoesNotLaunch()
    {
        var launcher = new FakeLauncher();
        var service = Create(launcher, Managed(), vite: new HotViteBuilder());

        await service.StartAsync(CancellationToken.None);

        Assert.That(launcher.Created, Is.Empty);
    }

    [Test]
    [Description("SC: a managed process is launched and becomes healthy.")]
    public async Task LaunchesAndBecomesHealthy()
    {
        var launcher = new FakeLauncher();
        var process = launcher.Enqueue();
        var service = Create(launcher, Managed());

        await service.StartAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(launcher.Created, Has.Count.EqualTo(1));
            Assert.That(process.Started, Is.True);
        });

        await service.StopAsync(CancellationToken.None);
    }

    [Test]
    [Description("SC: standard output and error lines are forwarded to the logger.")]
    public async Task ForwardsOutputToLogger()
    {
        var launcher = new FakeLauncher();
        var process = launcher.Enqueue();
        process.EnqueueStandardOutput("hello stdout");
        process.EnqueueStandardError("hello stderr");

        var logger = new CapturingLogger();
        var service = Create(launcher, Managed(), logger: logger);

        await service.StartAsync(CancellationToken.None);
        await process.WaitForOutputDrainedAsync();
        await service.StopAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(logger.Messages, Has.Some.Contains("hello stdout"));
            Assert.That(logger.Messages, Has.Some.Contains("hello stderr"));
        });
    }

    [Test]
    [Description("SC: if the health endpoint never succeeds within StartupTimeout the process is killed.")]
    public async Task HealthTimeout_KillsProcess()
    {
        var launcher = new FakeLauncher();
        var process = launcher.Enqueue();
        var service = Create(launcher, Managed(o =>
        {
            o.StartupTimeout = TimeSpan.FromMilliseconds(150);
            o.MaxRestarts = 0;
        }), httpClientFactory: new HealthFailFactory());
        process.ExitOnWait = false;

        await service.StartAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(process.KillCount, Is.GreaterThan(0));
            Assert.That(process.Disposed, Is.True);
        });
    }

    [Test]
    [Description("SC: a crashed process is restarted after the backoff.")]
    public async Task Crash_RestartsAfterBackoff()
    {
        var launcher = new FakeLauncher();
        var first = launcher.Enqueue();
        var second = launcher.Enqueue();
        var service = Create(launcher,
            Managed(o => o.InitialBackoff = TimeSpan.FromMilliseconds(20)));

        await service.StartAsync(CancellationToken.None);
        Assert.That(launcher.Created, Has.Count.EqualTo(1));

        first.Crash(exitCode: 1);

        await WaitUntilAsync(() => launcher.Created.Count == 2, TimeSpan.FromSeconds(3));
        Assert.Multiple(() =>
        {
            Assert.That(launcher.Created, Has.Count.EqualTo(2));
            Assert.That(second.Started, Is.True);
        });

        await service.StopAsync(CancellationToken.None);
    }

    [Test]
    [Description("SC: restarts stop once MaxRestarts is exceeded.")]
    public async Task Crash_GivesUpAfterMaxRestarts()
    {
        // The bundle never becomes healthy, so each relaunch fails health and is killed; the
        // restart budget accumulates until it is exhausted and no further process is created.
        var launcher = new FakeLauncher();
        launcher.Enqueue();
        var service = Create(launcher, Managed(o =>
        {
            o.StartupTimeout = TimeSpan.FromMilliseconds(60);
            o.InitialBackoff = TimeSpan.FromMilliseconds(1);
            o.MaxBackoff = TimeSpan.FromMilliseconds(1);
            o.MaxRestarts = 1;
        }), httpClientFactory: new HealthFailFactory());

        await service.StartAsync(CancellationToken.None);

        await WaitUntilAsync(() => launcher.Created.Count >= 2, TimeSpan.FromSeconds(3));
        await Task.Delay(300);

        // warmup + 1 allowed restart = 2 processes total.
        Assert.That(launcher.Created, Has.Count.EqualTo(2));

        await service.StopAsync(CancellationToken.None);
    }

    [Test]
    [Description("SC: RestartPolicy.None never restarts.")]
    public async Task Crash_RestartPolicyNone_DoesNotRestart()
    {
        var launcher = new FakeLauncher();
        var process = launcher.Enqueue();
        var service = Create(launcher, Managed(o => o.RestartPolicy = RestartPolicy.None));

        await service.StartAsync(CancellationToken.None);
        process.Crash(exitCode: 1);

        await Task.Delay(150);
        Assert.That(launcher.Created, Has.Count.EqualTo(1));

        await service.StopAsync(CancellationToken.None);
    }

    [Test]
    [Description("SC: graceful shutdown posts to /shutdown and waits for exit; no kill when it exits cleanly.")]
    public async Task GracefulShutdown_PostsAndWaits()
    {
        var launcher = new FakeLauncher();
        var process = launcher.Enqueue();
        var handler = new HealthOkFactory(recordShutdown: true);
        var service = Create(launcher, Managed(), httpClientFactory: handler);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(handler.ShutdownPosts, Is.EqualTo(1));
            Assert.That(process.KillCount, Is.EqualTo(0), "a clean exit needs no kill");
        });
    }

    [Test]
    [Description("SC: if the process does not exit after /shutdown it is killed (whole tree).")]
    public async Task GracefulShutdown_Timeout_Kills()
    {
        var launcher = new FakeLauncher();
        var process = launcher.Enqueue();
        process.ExitOnWait = false;

        var handler = new HealthOkFactory(recordShutdown: true);
        var service = Create(launcher, Managed(o => o.ShutdownTimeout = TimeSpan.FromMilliseconds(50)),
            httpClientFactory: handler);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(handler.ShutdownPosts, Is.EqualTo(1));
            Assert.That(process.KillCount, Is.GreaterThan(0));
        });
    }

    [Test]
    [Description("SC: an intentional stop does not trigger a restart.")]
    public async Task IntentionalStop_DoesNotRestart()
    {
        var launcher = new FakeLauncher();
        var process = launcher.Enqueue();
        process.ExitOnWait = false;

        var service = Create(launcher, Managed(o => o.ShutdownTimeout = TimeSpan.FromMilliseconds(50)));

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        await Task.Delay(150);
        Assert.That(launcher.Created, Has.Count.EqualTo(1));
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(20);
        }

        Assert.Fail("Condition was not met within the timeout.");
    }

    private sealed class FakeLauncher : IInertiaSsrProcessLauncher
    {
        private readonly List<FakeProcess> _queue = new();
        private int _index;

        public List<FakeProcess> Created { get; } = new();

        public FakeProcess Enqueue()
        {
            var process = new FakeProcess();
            _queue.Add(process);
            return process;
        }

        public IInertiaSsrProcess Create(InertiaSsrOptions options)
        {
            var process = _index < _queue.Count ? _queue[_index] : new FakeProcess();
            _index++;
            Created.Add(process);
            return process;
        }
    }

    private sealed class FakeProcess : IInertiaSsrProcess
    {
        private readonly Channel<string> _stdout = Channel.CreateUnbounded<string>();
        private readonly Channel<string> _stderr = Channel.CreateUnbounded<string>();
        private readonly SemaphoreSlim _outputDrained = new(0);

        public bool Started { get; private set; }
        public bool HasExited { get; private set; }
        public int ExitCode { get; private set; }
        public int Id { get; } = Random.Shared.Next(1000, 9999);
        public int KillCount { get; private set; }
        public bool Disposed { get; private set; }

        /// <summary>When true, WaitForExitAsync reports the process exited (simulating a clean stop).</summary>
        public bool ExitOnWait { get; set; } = true;

        public event EventHandler? Exited;

        public void Start() => Started = true;

        public void EnqueueStandardOutput(string line)
        {
            _stdout.Writer.TryWrite(line);
            _outputDrained.Release();
        }

        public void EnqueueStandardError(string line)
        {
            _stderr.Writer.TryWrite(line);
            _outputDrained.Release();
        }

        public async Task WaitForOutputDrainedAsync()
        {
            // Wait for the two lines to be consumed by the pump loops.
            for (var i = 0; i < 2; i++)
                await _outputDrained.WaitAsync(TimeSpan.FromSeconds(2));
        }

        public async Task<string?> ReadStandardOutputLineAsync(CancellationToken cancellationToken) =>
            await _stdout.Reader.ReadAsync(cancellationToken);

        public async Task<string?> ReadStandardErrorLineAsync(CancellationToken cancellationToken) =>
            await _stderr.Reader.ReadAsync(cancellationToken);

        public Task<bool> WaitForExitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (ExitOnWait)
            {
                HasExited = true;
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public void Kill()
        {
            KillCount++;
            var wasRunning = !HasExited;
            HasExited = true;
            if (wasRunning) Exited?.Invoke(this, EventArgs.Empty);
        }

        public void Crash(int exitCode)
        {
            ExitCode = exitCode;
            HasExited = true;
            Exited?.Invoke(this, EventArgs.Empty);
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            _stdout.Writer.TryComplete();
            _stderr.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class HealthOkFactory : IHttpClientFactory
    {
        private readonly bool _recordShutdown;
        private int _shutdownPosts;

        public HealthOkFactory(bool recordShutdown = false) => _recordShutdown = recordShutdown;

        public int ShutdownPosts => _shutdownPosts;

        public HttpClient CreateClient(string name = "")
        {
            return new HttpClient(new StubHandler(request =>
            {
                if (_recordShutdown && request.Method == HttpMethod.Post)
                    Interlocked.Increment(ref _shutdownPosts);

                return new HttpResponseMessage(HttpStatusCode.OK);
            }), disposeHandler: false);
        }
    }

    private sealed class HealthFailFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name = "") =>
            new(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)), disposeHandler: false);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_handler(request));
        }
    }

    private sealed class NeverHotViteBuilder : IViteBuilder
    {
        public HtmlString ReactRefresh() => HtmlString.Empty;
        public Task<HtmlString> ReactRefreshAsync() => Task.FromResult(HtmlString.Empty);
        public HtmlString Input(params string[] entryPoints) => HtmlString.Empty;
        public Task<HtmlString> InputAsync(params string[] entryPoints) => Task.FromResult(HtmlString.Empty);
        public string? GetManifest() => null;
        public string? GetManifestHash() => null;
        public bool IsRunningHot() => false;
        public string Asset(string entryPoint) => entryPoint;
        public string Content(string entryPoint) => entryPoint;
    }

    private sealed class HotViteBuilder : IViteBuilder
    {
        public HtmlString ReactRefresh() => HtmlString.Empty;
        public Task<HtmlString> ReactRefreshAsync() => Task.FromResult(HtmlString.Empty);
        public HtmlString Input(params string[] entryPoints) => HtmlString.Empty;
        public Task<HtmlString> InputAsync(params string[] entryPoints) => Task.FromResult(HtmlString.Empty);
        public string? GetManifest() => null;
        public string? GetManifestHash() => null;
        public bool IsRunningHot() => true;
        public string Asset(string entryPoint) => entryPoint;
        public string Content(string entryPoint) => entryPoint;
    }

    private sealed class CapturingLogger : Microsoft.Extensions.Logging.ILogger<InertiaSsrHostedService>
    {
        public ConcurrentBag<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
