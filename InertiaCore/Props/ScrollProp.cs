namespace InertiaCore.Props;

/// <summary>
/// A paginated prop configured for Inertia's infinite scroll. The inner data array
/// is merged (appended when loading forward, prepended when loading backward) during
/// partial reloads, and the page object carries the pagination cursor under
/// <c>scrollProps</c>.
/// <para>
/// The merge direction is taken from the <c>X-Inertia-Infinite-Scroll-Merge-Intent</c>
/// request header sent by the client.
/// </para>
/// See: https://inertiajs.com/infinite-scroll
/// </summary>
public class ScrollProp : InvokableProp
{
    internal ScrollProp(object? value, ScrollMetadata metadata, string wrapper) : base(value)
    {
        Metadata = metadata;
        Wrapper = wrapper;
        ConfigureMergeIntent(prepend: false);
    }

    /// <summary>The cursor metadata emitted under <c>scrollProps</c>.</summary>
    internal ScrollMetadata Metadata { get; }

    /// <summary>The key of the array within the prop that should be merged.</summary>
    internal string Wrapper { get; }

    /// <summary>
    /// Sets the merge direction for the current request. Reuses the generic merge
    /// state so the prop is labelled at <c>{prop}.{wrapper}</c>.
    /// </summary>
    internal void ConfigureMergeIntent(bool prepend)
    {
        ClearMergePaths();

        if (prepend)
            Prepend(Wrapper);
        else
            Append(Wrapper);
    }
}
