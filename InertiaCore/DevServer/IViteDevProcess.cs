using System;
using System.Threading;
using System.Threading.Tasks;

namespace InertiaCore.DevServer;

/// <summary>
/// A launched Vite dev server child process. Abstracted so the lifecycle logic in
/// <see cref="ViteDevServer"/> can be tested without spawning a real process.
/// </summary>
internal interface IViteDevProcess : IAsyncDisposable
{
    /// <summary>Whether the process has exited.</summary>
    bool HasExited { get; }

    /// <summary>The process identifier, for diagnostics.</summary>
    int Id { get; }

    /// <summary>Raised when the process exits.</summary>
    event EventHandler? Exited;

    /// <summary>Reads a line of the process's standard output, or <see langword="null"/> at end of stream.</summary>
    Task<string?> ReadStandardOutputLineAsync(CancellationToken cancellationToken);

    /// <summary>Reads a line of the process's standard error, or <see langword="null"/> at end of stream.</summary>
    Task<string?> ReadStandardErrorLineAsync(CancellationToken cancellationToken);

    /// <summary>Starts the process.</summary>
    void Start();

    /// <summary>Kills the process and its entire process tree.</summary>
    void Kill();
}

/// <summary>
/// Launches <see cref="IViteDevProcess"/> instances. The real implementation spawns a
/// <see cref="System.Diagnostics.Process"/> with redirected streams; tests substitute a fake.
/// </summary>
internal interface IViteDevProcessLauncher
{
    /// <summary>
    /// Creates (but does not start) a dev server process that runs
    /// <paramref name="options"/>. The executable is resolved for the current platform.
    /// </summary>
    IViteDevProcess Create(Models.ViteDevServerOptions options, string workingDirectory);
}
