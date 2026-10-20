using System;
using System.Collections.Generic;

namespace InertiaCore.Models;

/// <summary>
/// Options for the managed Node SSR process. Enable <see cref="Managed"/> to have
/// <c>AddInertia()</c> register a hosted service that starts the Node bundle, waits for its
/// health endpoint, forwards its output to <c>ILogger</c>, restarts it on crash and shuts it
/// down gracefully on application stop.
/// </summary>
/// <remarks>
/// Management is automatically disabled when <see cref="InertiaOptions.SsrEnabled"/> is
/// <see langword="false"/> or when the Vite dev server is running (the dev server renders the
/// bundle itself). When <see cref="Managed"/> is enabled and <see cref="InertiaOptions.SsrUrl"/>
/// is left at its default, the gateway URL is aligned with <see cref="Port"/>.
/// </remarks>
public class InertiaSsrOptions
{
    /// <summary>
    /// Whether the adapter launches and manages the Node SSR process. Disabled by default so
    /// applications that run the SSR server themselves are unaffected.
    /// </summary>
    public bool Managed { get; set; }

    /// <summary>
    /// The Node executable to launch. A relative name is resolved against <c>PATH</c>.
    /// </summary>
    public string NodePath { get; set; } = "node";

    /// <summary>
    /// Path to the SSR entry script (for example <c>ssr.mjs</c>). A relative path is resolved
    /// against <see cref="WorkingDirectory"/>.
    /// </summary>
    public string ScriptPath { get; set; } = "ssr.mjs";

    /// <summary>
    /// Port the SSR server listens on. Also used to derive the health and shutdown endpoints,
    /// and (when <see cref="InertiaOptions.SsrUrl"/> is left at its default) the gateway URL.
    /// </summary>
    public int Port { get; set; } = 13714;

    /// <summary>
    /// Maximum time to wait for the health endpoint after launching before treating the start
    /// as failed.
    /// </summary>
    public TimeSpan StartupTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// When the SSR process exits unexpectedly. Defaults to <see cref="Models.RestartPolicy.OnFailure"/>.
    /// </summary>
    public RestartPolicy RestartPolicy { get; set; } = RestartPolicy.OnFailure;

    /// <summary>
    /// Delay before the first restart. Each subsequent restart doubles the delay until
    /// <see cref="MaxBackoff"/> is reached.
    /// </summary>
    public TimeSpan InitialBackoff { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Upper bound for the exponential restart backoff.
    /// </summary>
    public TimeSpan MaxBackoff { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Maximum number of restarts before giving up. A value of <c>-1</c> restarts indefinitely.
    /// </summary>
    public int MaxRestarts { get; set; } = 5;

    /// <summary>
    /// Working directory for the launched process. Defaults to the application base directory
    /// when <see langword="null"/>.
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Extra environment variables applied to the launched process.
    /// </summary>
    public IDictionary<string, string> EnvironmentVariables { get; set; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Time to wait for a graceful <c>/shutdown</c> before killing the process tree.
    /// </summary>
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The health endpoint URL derived from <see cref="Port"/>.
    /// </summary>
    public string HealthUrl => $"http://127.0.0.1:{Port}/health";

    /// <summary>
    /// The graceful shutdown endpoint URL derived from <see cref="Port"/>.
    /// </summary>
    public string ShutdownUrl => $"http://127.0.0.1:{Port}/shutdown";

    /// <summary>
    /// The render gateway URL derived from <see cref="Port"/>.
    /// </summary>
    public string RenderUrl => $"http://127.0.0.1:{Port}/render";
}
