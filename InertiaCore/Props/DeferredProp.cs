using InertiaCore.Extensions;

namespace InertiaCore.Props;

/// <summary>
/// A deferred prop that is excluded from the initial full page load and resolved only
/// when explicitly requested via X-Inertia-Partial-Data. Deferred props are announced
/// to the client through the page object's <c>deferredProps</c> metadata, grouped by
/// the group they were declared with.
/// See: https://inertiajs.com/deferred-props
/// </summary>
public class DeferredProp : InvokableProp, IRescuable
{
    private bool _rescue;

    /// <summary>
    /// Creates a deferred prop with a synchronous factory.
    /// </summary>
    /// <param name="value">The factory that produces the prop value.</param>
    /// <param name="group">The defer group used to fetch props in parallel.</param>
    /// <param name="rescue">Whether resolution failures should be rescued.</param>
    internal DeferredProp(Func<object?> value, string group = "default", bool rescue = false) : base(value)
    {
        Group = group;
        _rescue = rescue;
    }

    /// <summary>
    /// Creates a deferred prop with an asynchronous factory.
    /// </summary>
    /// <param name="value">The asynchronous factory that produces the prop value.</param>
    /// <param name="group">The defer group used to fetch props in parallel.</param>
    /// <param name="rescue">Whether resolution failures should be rescued.</param>
    internal DeferredProp(Func<Task<object?>> value, string group = "default", bool rescue = false) : base(value)
    {
        Group = group;
        _rescue = rescue;
    }

    /// <summary>The defer group this prop belongs to. Defaults to <c>"default"</c>.</summary>
    internal string Group { get; }

    bool IRescuable.ShouldRescue => _rescue;

    /// <summary>
    /// Rescues resolution failures for this prop: the exception is logged, the value is
    /// omitted, and the prop is reported through <c>rescuedProps</c> instead of failing
    /// the whole request.
    /// </summary>
    /// <param name="value">Whether to rescue. Defaults to <see langword="true"/>.</param>
    /// <returns>The same prop, for chaining.</returns>
    public DeferredProp Rescue(bool value = true)
    {
        _rescue = value;
        return this;
    }
}
