namespace InertiaCore.Models;

/// <summary>
/// How Inertia resolves the top-level props of a page.
/// </summary>
public enum PropResolution
{
    /// <summary>
    /// Resolve props one after another. This is the default because a prop may
    /// resolve a scoped service (such as a <c>DbContext</c>) that is not thread-safe,
    /// so parallel resolution can fault.
    /// </summary>
    Sequential,

    /// <summary>
    /// Resolve independent props concurrently. Only use this when every prop is safe
    /// to run in parallel (no shared scoped state).
    /// </summary>
    Parallel,
}
