namespace InertiaCore.Models;

/// <summary>
/// How a managed SSR process is restarted after it exits unexpectedly.
/// </summary>
public enum RestartPolicy
{
    /// <summary>
    /// Never restart the process. A crash is logged and the application keeps serving
    /// through the client-side rendering fallback.
    /// </summary>
    None,

    /// <summary>
    /// Restart the process with exponential backoff, up to <see cref="InertiaSsrOptions.MaxRestarts"/> times.
    /// </summary>
    OnFailure
}
