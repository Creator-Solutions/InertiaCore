namespace InertiaCore.Logging;

/// <summary>
/// Stable, documented event identifiers for InertiaCore log messages. Ranges are reserved
/// per subsystem so new events can be added without colliding with existing ones:
/// <list type="bullet">
/// <item><description>1000-1099: core / asset versioning</description></item>
/// <item><description>1100-1199: prop resolution</description></item>
/// <item><description>2000-2099: SSR gateway</description></item>
/// <item><description>3000-3099: managed SSR hosted service</description></item>
/// <item><description>4000-4099: SSR health check</description></item>
/// <item><description>5000-5099: Vite dev server</description></item>
/// <item><description>6000-6099: Vite dev server helper</description></item>
/// </list>
/// </summary>
public static class InertiaLogEvents
{
    // Core / asset versioning (1000-1099)
    public const int VersionMismatch = 1000;

    // Prop resolution (1100-1199)
    public const int PropResolutionFailed = 1100;

    // SSR gateway (2000-2099)
    public const int SsrInvalidUrl = 2000;
    public const int SsrRequestFailedStatus = 2001;
    public const int SsrResponseTooLarge = 2002;
    public const int SsrNoBody = 2003;
    public const int SsrCircuitOpen = 2004;
    public const int SsrTimedOut = 2005;
    public const int SsrRateLimited = 2006;
    public const int SsrCancelled = 2007;
    public const int SsrHttpFailed = 2008;
    public const int SsrInvalidJson = 2009;

    // Managed SSR hosted service (3000-3099)
    public const int SsrManagedDisabled = 3000;
    public const int SsrManagedViteRunning = 3001;
    public const int SsrManagedStarted = 3002;
    public const int SsrManagedHealthy = 3003;
    public const int SsrManagedUnhealthy = 3004;
    public const int SsrStdErr = 3005;
    public const int SsrStdOut = 3006;
    public const int SsrStreamEnded = 3007;
    public const int SsrProcessExited = 3008;
    public const int SsrRestartNone = 3009;
    public const int SsrMaxRestartsExceeded = 3010;
    public const int SsrRestarting = 3011;
    public const int SsrShuttingDown = 3012;
    public const int SsrShutdownTimeout = 3013;
    public const int SsrViteProbeFailed = 3014;

    // SSR health check (4000-4099)
    public const int SsrHealthProbeFailed = 4000;

    // Vite dev server (5000-5099)
    public const int ViteAlreadyRunning = 5000;
    public const int ViteStarted = 5001;
    public const int ViteReady = 5002;
    public const int ViteNotReady = 5003;
    public const int ViteProbeFailed = 5004;
    public const int ViteStdErr = 5005;
    public const int ViteStdOut = 5006;
    public const int ViteStreamEnded = 5007;
    public const int ViteProcessExited = 5008;
    public const int ViteStopping = 5009;

    // Vite dev server helper (6000-6099)
    public const int ViteOptionsNotRegistered = 6000;
    public const int ViteLifetimeNotRegistered = 6001;
}
