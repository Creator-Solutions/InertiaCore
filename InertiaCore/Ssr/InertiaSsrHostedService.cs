using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using InertiaCore.Models;
using InertiaCore.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InertiaCore.Ssr;

/// <summary>
/// Hosted service that manages the lifecycle of the Node SSR process: it starts the bundle,
/// waits for its health endpoint, forwards standard output and error to <see cref="ILogger"/>,
/// restarts it on crash with exponential backoff and shuts it down gracefully (via the
/// <c>/shutdown</c> endpoint) before killing the process tree.
/// </summary>
/// <remarks>
/// The service is registered by <c>AddInertia()</c> when <see cref="InertiaSsrOptions.Managed"/>
/// is enabled and it is a no-op when <see cref="InertiaOptions.SsrEnabled"/> is
/// <see langword="false"/> or when the Vite dev server is running.
/// </remarks>
internal sealed class InertiaSsrHostedService : IHostedService
{
    private readonly IInertiaSsrProcessLauncher _launcher;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InertiaSsrHostedService> _logger;
    private readonly InertiaOptions _options;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _stopping = new();

    private IInertiaSsrProcess? _process;
    private bool _shuttingDown;
    private int _restartCount;

    public InertiaSsrHostedService(
        IInertiaSsrProcessLauncher launcher,
        IHttpClientFactory httpClientFactory,
        IServiceProvider serviceProvider,
        IOptions<InertiaOptions> options,
        ILogger<InertiaSsrHostedService> logger)
    {
        _launcher = launcher;
        _httpClientFactory = httpClientFactory;
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Ssr.Managed)
            return Task.CompletedTask;

        if (!_options.SsrEnabled)
        {
            _logger.LogInformation(
                "Managed SSR is enabled but SSR is disabled (SsrEnabled is false); the SSR process will not be started.");
            return Task.CompletedTask;
        }

        if (IsViteRunning())
        {
            _logger.LogInformation(
                "Managed SSR is enabled but the Vite dev server is running; the SSR process will not be started.");
            return Task.CompletedTask;
        }

        return StartProcessAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _shuttingDown = true;
        _stopping.Cancel();

        await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            await StopProcessAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task StartProcessAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await LaunchAndAwaitHealthAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task LaunchAndAwaitHealthAsync(CancellationToken cancellationToken)
    {
        _process = _launcher.Create(_options.Ssr);
        _process.Exited += OnProcessExited;
        _process.Start();

        _logger.LogInformation("Started managed SSR process (pid {Pid}).", _process.Id);

        _ = PumpAsync(_process, isError: false);
        _ = PumpAsync(_process, isError: true);

        if (await WaitForHealthAsync(cancellationToken).ConfigureAwait(false))
        {
            _restartCount = 0;
            _logger.LogInformation("Managed SSR process is healthy at {Url}.", _options.Ssr.HealthUrl);
            return;
        }

        _logger.LogError(
            "Managed SSR process did not become healthy at {Url} within {Timeout}; killing it. Rendering will fall back to the client.",
            _options.Ssr.HealthUrl,
            _options.Ssr.StartupTimeout);

        await KillProcessAsync().ConfigureAwait(false);
    }

    private async Task<bool> WaitForHealthAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + _options.Ssr.StartupTimeout;
        var client = _httpClientFactory.CreateClient();

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (cancellationToken.IsCancellationRequested || _stopping.IsCancellationRequested)
                return false;

            if (_process is null || _process.HasExited)
                return false;

            try
            {
                using var response = await client
                    .GetAsync(_options.Ssr.HealthUrl, cancellationToken)
                    .ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                    return true;
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        return false;
    }

    private async Task PumpAsync(IInertiaSsrProcess process, bool isError)
    {
        try
        {
            while (true)
            {
                var line = isError
                    ? await process.ReadStandardErrorLineAsync(_stopping.Token).ConfigureAwait(false)
                    : await process.ReadStandardOutputLineAsync(_stopping.Token).ConfigureAwait(false);

                if (line is null) break;

                if (isError) _logger.LogWarning("[ssr:stderr] {Line}", line);
                else _logger.LogInformation("[ssr:stdout] {Line}", line);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SSR process output stream ended.");
        }
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        if (_shuttingDown) return;

        _ = HandleCrashAsync();
    }

    private async Task HandleCrashAsync()
    {
        await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (_shuttingDown) return;

            var exitCode = TryGetExitCode();
            _logger.LogError(
                "Managed SSR process exited unexpectedly with code {ExitCode}.",
                exitCode);

            if (_options.Ssr.RestartPolicy == RestartPolicy.None)
            {
                _logger.LogWarning("RestartPolicy is None; the SSR process will not be restarted.");
                return;
            }

            if (_options.Ssr.MaxRestarts >= 0 && _restartCount >= _options.Ssr.MaxRestarts)
            {
                _logger.LogError(
                    "Managed SSR process exceeded the maximum number of restarts ({Max}); giving up. Rendering will fall back to the client.",
                    _options.Ssr.MaxRestarts);
                return;
            }

            var backoff = ComputeBackoff(_restartCount);
            _restartCount++;

            _logger.LogInformation(
                "Restarting managed SSR process in {Backoff} (attempt {Attempt}).",
                backoff,
                _restartCount);

            try
            {
                await Task.Delay(backoff, _stopping.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (_shuttingDown) return;

            await DisposeProcessAsync().ConfigureAwait(false);
            await LaunchAndAwaitHealthAsync(_stopping.Token).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private int TryGetExitCode()
    {
        try
        {
            return _process is { HasExited: true } ? _process.ExitCode : -1;
        }
        catch (InvalidOperationException)
        {
            return -1;
        }
    }

    private TimeSpan ComputeBackoff(int attempt)
    {
        var initial = _options.Ssr.InitialBackoff;
        var max = _options.Ssr.MaxBackoff;

        var factor = Math.Pow(2, Math.Min(attempt, 16));
        var ticks = initial.Ticks * factor;

        return ticks >= max.Ticks ? max : TimeSpan.FromTicks((long)ticks);
    }

    private async Task StopProcessAsync()
    {
        if (_process is null) return;

        _logger.LogInformation("Shutting down managed SSR process (pid {Pid}).", _process.Id);

        await TryGracefulShutdownAsync().ConfigureAwait(false);

        var exited = await _process
            .WaitForExitAsync(_options.Ssr.ShutdownTimeout, CancellationToken.None)
            .ConfigureAwait(false);

        if (!exited)
        {
            _logger.LogWarning(
                "Managed SSR process did not shut down within {Timeout}; killing the process tree.",
                _options.Ssr.ShutdownTimeout);
            _process.Kill();
        }

        await DisposeProcessAsync().ConfigureAwait(false);
    }

    private async Task TryGracefulShutdownAsync()
    {
        // Use a fresh bounded token: _stopping is already cancelled by StopAsync, so the
        // shutdown request must not observe it.
        using var timeout = new CancellationTokenSource(_options.Ssr.ShutdownTimeout);

        try
        {
            var client = _httpClientFactory.CreateClient();
            using var response = await client
                .PostAsync(_options.Ssr.ShutdownUrl, content: null, timeout.Token)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task KillProcessAsync()
    {
        if (_process is null) return;

        try
        {
            if (!_process.HasExited) _process.Kill();
        }
        catch (InvalidOperationException)
        {
        }

        await DisposeProcessAsync().ConfigureAwait(false);
    }

    private async Task DisposeProcessAsync()
    {
        if (_process is null) return;

        _process.Exited -= OnProcessExited;
        await _process.DisposeAsync().ConfigureAwait(false);
        _process = null;
    }

    private bool IsViteRunning()
    {
        var vite = _serviceProvider.GetService(typeof(IViteBuilder)) as IViteBuilder;
        if (vite is null) return false;

        try
        {
            return vite.IsRunningHot();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not determine whether the Vite dev server is running.");
            return false;
        }
    }
}
