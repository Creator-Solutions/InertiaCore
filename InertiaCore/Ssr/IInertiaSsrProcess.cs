using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace InertiaCore.Ssr;

/// <summary>
/// A launched SSR child process. Abstracted so the lifecycle logic in
/// <see cref="InertiaSsrHostedService"/> can be tested without spawning a real Node process.
/// </summary>
internal interface IInertiaSsrProcess : IAsyncDisposable
{
    /// <summary>Whether the process has exited.</summary>
    bool HasExited { get; }

    /// <summary>The process exit code (valid once <see cref="HasExited"/> is true).</summary>
    int ExitCode { get; }

    /// <summary>Raised when the process exits.</summary>
    event EventHandler? Exited;

    /// <summary>The process identifier, for diagnostics.</summary>
    int Id { get; }

    /// <summary>Reads a line of the process's standard output, or <see langword="null"/> at end of stream.</summary>
    Task<string?> ReadStandardOutputLineAsync(CancellationToken cancellationToken);

    /// <summary>Reads a line of the process's standard error, or <see langword="null"/> at end of stream.</summary>
    Task<string?> ReadStandardErrorLineAsync(CancellationToken cancellationToken);

    /// <summary>Starts the process.</summary>
    void Start();

    /// <summary>
    /// Requests the process to stop and waits up to <paramref name="timeout"/> for it to exit.
    /// Returns <see langword="true"/> when the process has exited.
    /// </summary>
    Task<bool> WaitForExitAsync(TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>Kills the process and its entire process tree.</summary>
    void Kill();
}

/// <summary>
/// Launches <see cref="IInertiaSsrProcess"/> instances. The real implementation spawns a
/// <see cref="System.Diagnostics.Process"/> with redirected streams; tests substitute a fake.
/// </summary>
internal interface IInertiaSsrProcessLauncher
{
    /// <summary>Creates (but does not start) a process for the supplied options.</summary>
    IInertiaSsrProcess Create(Models.InertiaSsrOptions options);
}
