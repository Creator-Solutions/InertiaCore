using System;
using System.IO.Abstractions;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using InertiaCore.Models;
using InertiaCore.Ssr;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InertiaCore.DevServer;

/// <summary>
/// Runs the configured package-manager script for the Vite dev server, waits for Vite's
/// hot file to appear and stops the process when the application shuts down.
/// </summary>
/// <remarks>
/// The helper is a no-op when the dev server is already running (the hot file exists and
/// the URL it contains is reachable), so a developer who started Vite by hand is not
/// affected. It is only used from <c>UseViteDevServer()</c>, which is Development-only.
/// </remarks>
internal sealed class ViteDevServer : IAsyncDisposable
{
    private readonly IViteDevProcessLauncher _launcher;
    private readonly IFileSystem _fileSystem;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ViteDevServer> _logger;
    private readonly ViteDevServerOptions _options;
    private readonly ViteOptions _viteOptions;
    private readonly string _contentRoot;

    private readonly CancellationTokenSource _stopping = new();
    private IViteDevProcess? _process;
    private bool _intentionalStop;
    private bool _disposed;

    public ViteDevServer(
        IViteDevProcessLauncher launcher,
        IFileSystem fileSystem,
        IHttpClientFactory httpClientFactory,
        IOptions<ViteDevServerOptions> options,
        IOptions<ViteOptions> viteOptions,
        ILogger<ViteDevServer> logger,
        string contentRoot)
    {
        _launcher = launcher;
        _fileSystem = fileSystem;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _viteOptions = viteOptions.Value;
        _logger = logger;
        _contentRoot = contentRoot;
    }

    /// <summary>
    /// Starts the dev server unless it is already running, then waits for readiness.
    /// Returns <see langword="true"/> when the dev server is ready (started here or
    /// already running); <see langword="false"/> when it could not be started.
    /// </summary>
    public async Task<bool> StartAsync(CancellationToken cancellationToken)
    {
        if (await IsAlreadyRunningAsync(cancellationToken).ConfigureAwait(false))
        {
            _logger.LogInformation(
                "The Vite dev server is already running (hot file {HotFile} is present and reachable); " +
                "the helper will not start another.",
                HotFilePath);
            return true;
        }

        var workingDirectory = ResolveWorkingDirectory();

        _process = _launcher.Create(_options, workingDirectory);
        _process.Exited += OnProcessExited;
        _process.Start();

        _logger.LogInformation(
            "Started the Vite dev server with {PackageManager} run {Script} in {Directory} (pid {Pid}).",
            _options.PackageManager.ToString().ToLowerInvariant(),
            _options.ScriptName,
            workingDirectory,
            _process.Id);

        _ = PumpAsync(_process, isError: false);
        _ = PumpAsync(_process, isError: true);

        if (await WaitForHotFileAsync(cancellationToken).ConfigureAwait(false))
        {
            _logger.LogInformation("The Vite dev server is ready (hot file {HotFile}).", HotFilePath);
            return true;
        }

        _logger.LogError(
            "The Vite dev server did not write its hot file within {Timeout}; stopping it.",
            _options.ReadyTimeout);

        await StopProcessAsync().ConfigureAwait(false);
        return false;
    }

    /// <summary>The hot file path, resolved under the public directory.</summary>
    internal string HotFilePath =>
        _fileSystem.Path.Combine(
            _contentRoot,
            _viteOptions.PublicDirectory,
            string.IsNullOrWhiteSpace(_options.HotFile) ? _viteOptions.HotFile : _options.HotFile!);

    private async Task<bool> IsAlreadyRunningAsync(CancellationToken cancellationToken)
    {
        if (!_fileSystem.File.Exists(HotFilePath)) return false;

        // The hot file records the dev server URL. A stale file (Vite is not listening)
        // must not stop the helper from starting its own process.
        try
        {
            var url = _fileSystem.File.ReadAllText(HotFilePath).Trim();
            if (string.IsNullOrEmpty(url)) return false;

            using var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not probe the existing Vite dev server URL.");
            return false;
        }
    }

    private async Task<bool> WaitForHotFileAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + _options.ReadyTimeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (cancellationToken.IsCancellationRequested || _stopping.IsCancellationRequested)
                return false;

            if (_process is null || _process.HasExited)
                return false;

            if (_fileSystem.File.Exists(HotFilePath))
                return true;

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

    private async Task PumpAsync(IViteDevProcess process, bool isError)
    {
        try
        {
            while (true)
            {
                var line = isError
                    ? await process.ReadStandardErrorLineAsync(_stopping.Token).ConfigureAwait(false)
                    : await process.ReadStandardOutputLineAsync(_stopping.Token).ConfigureAwait(false);

                if (line is null) break;

                if (isError) _logger.LogWarning("[vite:stderr] {Line}", line);
                else _logger.LogInformation("[vite:stdout] {Line}", line);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "The Vite dev server output stream ended.");
        }
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        if (_disposed || _intentionalStop) return;
        _logger.LogWarning("The Vite dev server process exited.");
    }

    private string ResolveWorkingDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_options.WorkingDirectory))
        {
            var configured = _options.WorkingDirectory!;
            return _fileSystem.Path.IsPathRooted(configured)
                ? configured
                : _fileSystem.Path.GetFullPath(_fileSystem.Path.Combine(_contentRoot, configured));
        }

        // Walk up from the content root looking for package.json so `dotnet run` from a
        // nested project directory still finds the frontend.
        var directory = _contentRoot;
        while (!string.IsNullOrEmpty(directory))
        {
            if (_fileSystem.File.Exists(_fileSystem.Path.Combine(directory, "package.json")))
                return directory;

            var parent = _fileSystem.Path.GetDirectoryName(directory);
            if (string.IsNullOrEmpty(parent) || parent == directory) break;
            directory = parent;
        }

        return _contentRoot;
    }

    private async Task StopProcessAsync()
    {
        if (_process is null) return;

        _intentionalStop = true;
        _logger.LogInformation("Stopping the Vite dev server (pid {Pid}).", _process.Id);

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

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _stopping.Cancel();

        await StopProcessAsync().ConfigureAwait(false);

        _stopping.Dispose();
    }
}
