namespace InertiaCore.Props;

/// <summary>
/// Holds the merge configuration for a single prop. Mirrors the state tracked by
/// the Laravel adapter's <c>MergesProps</c> trait.
/// </summary>
internal sealed class MergeState
{
    /// <summary>Whether the prop should be merged on partial reloads.</summary>
    public bool Merge { get; set; }

    /// <summary>Whether the prop should be deep merged.</summary>
    public bool DeepMerge { get; set; }

    /// <summary>
    /// Whether the prop should be appended (true, the default) or prepended (false)
    /// at the root level.
    /// </summary>
    public bool Append { get; set; } = true;

    /// <summary>Nested paths within the prop that should be appended.</summary>
    public List<string> AppendsAtPaths { get; } = new();

    /// <summary>Nested paths within the prop that should be prepended.</summary>
    public List<string> PrependsAtPaths { get; } = new();

    /// <summary>Key fields used to match existing items when merging.</summary>
    public List<string> MatchOn { get; } = new();

    /// <summary>Whether the prop merges at the root level (no nested paths).</summary>
    private bool MergesAtRoot => AppendsAtPaths.Count == 0 && PrependsAtPaths.Count == 0;

    /// <summary>Whether the value should be appended at the root level.</summary>
    public bool AppendsAtRoot => Merge && Append && MergesAtRoot;

    /// <summary>Whether the value should be prepended at the root level.</summary>
    public bool PrependsAtRoot => Merge && !Append && MergesAtRoot;
}
