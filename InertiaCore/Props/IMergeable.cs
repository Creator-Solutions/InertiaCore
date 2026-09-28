namespace InertiaCore.Props;

/// <summary>
/// Internal marker implemented by props that carry merge metadata. The serializer
/// inspects this abstraction instead of special-casing concrete prop types.
/// See: https://inertiajs.com/merging-props
/// </summary>
internal interface IMergeable
{
    /// <summary>Whether the prop should be merged on partial reloads.</summary>
    bool ShouldMerge { get; }

    /// <summary>Whether the prop should be deep merged.</summary>
    bool ShouldDeepMerge { get; }

    /// <summary>Whether the value should be appended at the root level.</summary>
    bool AppendsAtRoot { get; }

    /// <summary>Whether the value should be prepended at the root level.</summary>
    bool PrependsAtRoot { get; }

    /// <summary>Nested paths within the prop that should be appended.</summary>
    IReadOnlyList<string> AppendsAtPaths { get; }

    /// <summary>Nested paths within the prop that should be prepended.</summary>
    IReadOnlyList<string> PrependsAtPaths { get; }

    /// <summary>Key fields used to match existing items when merging.</summary>
    IReadOnlyList<string> MatchesOn { get; }
}
