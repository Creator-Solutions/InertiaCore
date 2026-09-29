namespace InertiaCore.Props;

/// <summary>
/// Internal marker implemented by props that should be resolved only once and
/// remembered by the client across navigations. The serializer inspects this
/// abstraction instead of special-casing concrete prop types.
/// See: https://inertiajs.com/once-props
/// </summary>
internal interface IOnceable
{
    /// <summary>Whether the prop should be resolved once and cached by the client.</summary>
    bool ShouldResolveOnce { get; }

    /// <summary>An optional custom key used to identify the prop across pages.</summary>
    string? Key { get; }

    /// <summary>The expiration timestamp in milliseconds, or <see langword="null"/> for no expiry.</summary>
    long? ExpiresAt { get; }

    /// <summary>Whether the prop should be force-refreshed even if the client already holds it.</summary>
    bool ForceRefresh { get; }
}
