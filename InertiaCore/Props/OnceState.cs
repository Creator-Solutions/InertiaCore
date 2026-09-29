namespace InertiaCore.Props;

/// <summary>
/// Holds the once configuration for a single prop. Mirrors the state tracked by the
/// Laravel adapter's <c>ResolvesOnce</c> trait.
/// </summary>
internal sealed class OnceState
{
    /// <summary>Whether the prop should be resolved once and cached by the client.</summary>
    public bool Once { get; set; }

    /// <summary>An optional custom key used to identify the prop across pages.</summary>
    public string? Key { get; set; }

    /// <summary>The expiration timestamp in milliseconds, or <see langword="null"/> for no expiry.</summary>
    public long? ExpiresAt { get; set; }

    /// <summary>Whether the prop should be force-refreshed even if the client already holds it.</summary>
    public bool Fresh { get; set; }
}
