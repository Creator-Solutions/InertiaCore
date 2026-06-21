using InertiaCore.Ssr;

namespace InertiaCore.Services;

/// <summary>
/// Per-request state container for Inertia.js operations.
/// Registered as Scoped — one instance per HTTP request.
/// </summary>
public sealed class InertiaState
{
    /// <summary>
    /// Shared props set via <c>IInertia.Share()</c>.
    /// Persisted for the entire request lifetime.
    /// </summary>
    public Dictionary<string, object?> SharedProps { get; } = new();

    /// <summary>
    /// Flash props set via <c>IInertia.Flash()</c>.
    /// Available only for the current request (single-request scope).
    /// </summary>
    public Dictionary<string, object?> FlashProps { get; } = new();

    /// <summary>
    /// Fixed asset version string.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Lazy asset version factory.
    /// </summary>
    public Func<string?>? VersionFunc { get; set; }

    /// <summary>
    /// Cached SSR response, set after the first SSR dispatch in a request.
    /// Migrated from <c>HttpContext.Features</c> — see InertiaState.
    /// </summary>
    public SsrResponse? SsrResponse { get; set; }
}
