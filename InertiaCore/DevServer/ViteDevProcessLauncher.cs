using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using InertiaCore.Models;

namespace InertiaCore.DevServer;

/// <summary>
/// Default <see cref="IViteDevProcessLauncher"/> that spawns a real package-manager
/// process with redirected standard output and error.
/// </summary>
internal class ViteDevProcessLauncher : IViteDevProcessLauncher
{
    /// <summary>
    /// The platform the launcher is running on. Overridable so the Windows
    /// <c>cmd.exe</c> resolution can be tested off Windows.
    /// </summary>
    protected internal virtual bool IsWindows =>
        OperatingSystem.IsWindows();

    /// <summary>
    /// The directories searched for the package-manager executable, in order. Overridable
    /// so tests can control resolution.
    /// </summary>
    protected internal virtual IEnumerable<string> SearchPaths
    {
        get
        {
            var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var separator = IsWindows ? ';' : ':';
            foreach (var entry in path.Split(separator, StringSplitOptions.RemoveEmptyEntries))
                yield return entry.Trim();
        }
    }

    /// <summary>Maps a package manager to the base executable name.</summary>
    internal static string ExecutableName(VitePackageManager packageManager) => packageManager switch
    {
        VitePackageManager.Npm => "npm",
        VitePackageManager.Pnpm => "pnpm",
        VitePackageManager.Yarn => "yarn",
        VitePackageManager.Bun => "bun",
        _ => "npm",
    };

    /// <summary>
    /// The arguments passed to the package manager to run <paramref name="script"/>. The
    /// <c>run</c> verb is spelled out so every supported manager accepts it.
    /// </summary>
    internal static IReadOnlyList<string> RunArguments(string script) =>
        new[] { "run", script };

    /// <inheritdoc />
    public IViteDevProcess Create(ViteDevServerOptions options, string workingDirectory) =>
        new SystemProcess(new Process { StartInfo = BuildStartInfo(options, workingDirectory) });

    /// <summary>
    /// Builds the <see cref="ProcessStartInfo"/> used to launch the package manager. Exposed
    /// so tests can assert the resolved executable and arguments without spawning a process.
    /// </summary>
    internal ProcessStartInfo BuildStartInfo(ViteDevServerOptions options, string workingDirectory)
    {
        var executable = ExecutableName(options.PackageManager);
        var startInfo = new ProcessStartInfo
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory
        };

        if (IsWindows)
        {
            // npm/pnpm/yarn/bun resolve to .cmd shims on Windows, which cannot be
            // executed directly; run them through the command interpreter. An explicit
            // executable path (contains a separator) is passed through unchanged.
            if (executable.IndexOfAny(new[] { '/', '\\' }) >= 0)
            {
                startInfo.FileName = executable;
            }
            else
            {
                startInfo.FileName = "cmd.exe";
                startInfo.ArgumentList.Add("/c");
                startInfo.ArgumentList.Add(ResolveWindowsShim(executable));
            }
        }
        else
        {
            startInfo.FileName = executable;
        }

        foreach (var argument in RunArguments(options.ScriptName))
            startInfo.ArgumentList.Add(argument);

        // Vite opens a browser by default; suppress it so `dotnet run` does not pop a tab.
        startInfo.Environment["BROWSER"] = "none";

        return startInfo;
    }

    /// <summary>
    /// Whether a file exists. Overridable so the shim lookup can be exercised without
    /// touching the real file system.
    /// </summary>
    protected internal virtual bool FileExists(string path) => File.Exists(path);

    /// <summary>
    /// Resolves a package-manager name to a Windows executable. Prefers a <c>.cmd</c> shim
    /// (found on <see cref="SearchPaths"/>) and falls back to the bare name so the command
    /// interpreter can resolve it from its own search path.
    /// </summary>
    internal string ResolveWindowsShim(string executable)
    {
        foreach (var directory in SearchPaths)
        {
            if (string.IsNullOrWhiteSpace(directory)) continue;

            var candidate = Path.Combine(directory, executable + ".cmd");
            try
            {
                if (FileExists(candidate)) return candidate;
            }
            catch (Exception)
            {
                // An unreadable PATH entry is skipped rather than failing startup.
            }
        }

        return executable;
    }

    private sealed class SystemProcess : IViteDevProcess
    {
        private readonly Process _process;
        private bool _exitedAttached;
        private bool _disposed;

        public SystemProcess(Process process) => _process = process;

        public bool HasExited => _process.HasExited;

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
