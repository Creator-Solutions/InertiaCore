namespace InertiaCore.Models;

/// <summary>
/// Controls how Vite's dynamically imported assets are prefetched after the
/// entry points have been rendered. Mirrors Laravel's prefetch strategies.
/// </summary>
public enum PrefetchStrategy
{
    /// <summary>No prefetching is performed.</summary>
    None = 0,

    /// <summary>Prefetch assets a few at a time (see <see cref="ViteOptions.PrefetchConcurrency"/>).</summary>
    Waterfall = 1,

    /// <summary>Prefetch every discovered asset at once.</summary>
    Aggressive = 2
}
