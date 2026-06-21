using InertiaCore.Extensions;

namespace InertiaCore.Props;

/// <summary>
/// A deferred prop that is excluded from the initial full page load
/// and resolved only when explicitly requested via X-Inertia-Partial-Data.
/// See: https://inertiajs.com/deferred-props
/// </summary>
public class DeferredProp : InvokableProp
{
    /// <summary>
    /// Creates a deferred prop with a synchronous factory.
    /// </summary>
    internal DeferredProp(Func<object?> value) : base(value)
    {
    }

    /// <summary>
    /// Creates a deferred prop with an asynchronous factory.
    /// </summary>
    internal DeferredProp(Func<Task<object?>> value) : base(value)
    {
    }
}
