using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using InertiaCore.Models;

namespace InertiaCore.Ssr;

/// <summary>
/// Default <see cref="IInertiaSsrProcessLauncher"/> that spawns a real
/// <see cref="Process"/> with redirected standard output and error.
/// </summary>
internal sealed class ProcessLauncher : IInertiaSsrProcessLauncher
{
    /// <inheritdoc />
    public IInertiaSsrProcess Create(InertiaSsrOptions options)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = options.NodePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = options.WorkingDirectory ?? AppContext.BaseDirectory
        };

        startInfo.ArgumentList.Add(options.ScriptPath);

        // Expose the configured port to the bundle so a script can bind to it without extra
        // configuration. Caller-provided variables win.
        startInfo.Environment["SSR_PORT"] = options.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);

        foreach (var (key, value) in options.EnvironmentVariables)
            startInfo.Environment[key] = value;

        return new SystemProcess(new Process { StartInfo = startInfo });
    }

    private sealed class SystemProcess : IInertiaSsrProcess
    {
        private readonly Process _process;
        private bool _exitedAttached;
        private bool _disposed;

        public SystemProcess(Process process) => _process = process;

        public bool HasExited => _process.HasExited;

        public int ExitCode => _process.ExitCode;

        public int Id => _process.Id;

        public event EventHandler? Exited;

        public void Start()
        {
            _process.EnableRaisingEvents = true;
            _process.Exited += OnExited;
            _exitedAttached = true;
            _process.Start();
        }

        private void OnExited(object? sender, EventArgs e) => Exited?.Invoke(this, EventArgs.Empty);

        public Task<string?> ReadStandardOutputLineAsync(CancellationToken cancellationToken) =>
            _process.StandardOutput.ReadLineAsync(cancellationToken).AsTask();

        public Task<string?> ReadStandardErrorLineAsync(CancellationToken cancellationToken) =>
            _process.StandardError.ReadLineAsync(cancellationToken).AsTask();

        public async Task<bool> WaitForExitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(timeout);
                await _process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException)
            {
                return _process.HasExited;
            }
        }

        public void Kill()
        {
            try
            {
                if (!_process.HasExited)
                    _process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // The process already exited between the check and the kill.
            }
            catch (NotSupportedException)
            {
                // Kill(entireProcessTree) is unavailable on some platforms; fall back to Kill().
                try
                {
                    if (!_process.HasExited) _process.Kill();
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        public ValueTask DisposeAsync()
        {
            if (_disposed) return ValueTask.CompletedTask;
            _disposed = true;

            if (_exitedAttached) _process.Exited -= OnExited;
            _process.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
