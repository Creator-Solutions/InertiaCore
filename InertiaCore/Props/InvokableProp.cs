using InertiaCore.Extensions;

namespace InertiaCore.Props;

/// <summary>
/// Base type for lazily-evaluated Inertia props such as <see cref="LazyProp"/>,
/// <see cref="AlwaysProp"/>, <see cref="DeferredProp"/> and <see cref="MergeProp"/>.
/// <para>
/// Merge behaviour can be composed onto any prop using the fluent
/// <see cref="Merge"/>, <see cref="DeepMerge"/>, <see cref="Prepend()"/>,
/// <see cref="Append(string[])"/> and <see cref="MatchOn(string[])"/> methods.
/// See: https://inertiajs.com/merging-props
/// </para>
/// </summary>
public class InvokableProp : IMergeable
{
    private readonly object? _value;
    private readonly MergeState _merge = new();

    protected InvokableProp(object? value) => _value = value;

    internal Task<object?> Invoke()
    {
        return _value switch
        {
            Func<object?> f => f.ResolveAsync(),
            Task t => t.ResolveResult(),
            InvokableProp p => p.Invoke(),
            _ => Task.FromResult(_value)
        };
    }

    /// <summary>
    /// Marks the prop so its value is merged (appended at the root level by default)
    /// with the existing client-side value during partial reloads instead of replacing it.
    /// </summary>
    /// <returns>The same prop, for chaining.</returns>
    public InvokableProp Merge()
    {
        _merge.Merge = true;
        return this;
    }

    /// <summary>
    /// Marks the prop for a deep merge with the existing client-side value.
    /// Implies <see cref="Merge"/>.
    /// </summary>
    /// <returns>The same prop, for chaining.</returns>
    public InvokableProp DeepMerge()
    {
        _merge.Merge = true;
        _merge.DeepMerge = true;
        return this;
    }

    /// <summary>
    /// Prepends the value at the root level instead of appending it.
    /// Implies <see cref="Merge"/>.
    /// </summary>
    /// <returns>The same prop, for chaining.</returns>
    public InvokableProp Prepend()
    {
        _merge.Merge = true;
        _merge.Append = false;
        return this;
    }

    /// <summary>
    /// Appends the value at the given nested path(s) within the prop.
    /// Implies <see cref="Merge"/>.
    /// </summary>
    /// <param name="paths">One or more dotted paths within the prop, such as <c>"data"</c>.</param>
    /// <returns>The same prop, for chaining.</returns>
    public InvokableProp Append(params string[] paths)
    {
        _merge.Merge = true;
        _merge.AppendsAtPaths.AddRange(paths);
        return this;
    }

    /// <summary>
    /// Prepends the value at the given nested path(s) within the prop.
    /// Implies <see cref="Merge"/>.
    /// </summary>
    /// <param name="paths">One or more dotted paths within the prop, such as <c>"messages"</c>.</param>
    /// <returns>The same prop, for chaining.</returns>
    public InvokableProp Prepend(params string[] paths)
    {
        _merge.Merge = true;
        _merge.PrependsAtPaths.AddRange(paths);
        return this;
    }

    /// <summary>
    /// Sets the key field(s) used to match existing items when merging, so matching
    /// items are updated in place instead of being appended.
    /// </summary>
    /// <param name="keys">
    /// One or more key fields, optionally nested, such as <c>"id"</c> or <c>"data.id"</c>.
    /// </param>
    /// <returns>The same prop, for chaining.</returns>
    public InvokableProp MatchOn(params string[] keys)
    {
        _merge.MatchOn.AddRange(keys);
        return this;
    }

    bool IMergeable.ShouldMerge => _merge.Merge;

    bool IMergeable.ShouldDeepMerge => _merge.DeepMerge;

    bool IMergeable.AppendsAtRoot => _merge.AppendsAtRoot;

    bool IMergeable.PrependsAtRoot => _merge.PrependsAtRoot;

    IReadOnlyList<string> IMergeable.AppendsAtPaths => _merge.AppendsAtPaths;

    IReadOnlyList<string> IMergeable.PrependsAtPaths => _merge.PrependsAtPaths;

    IReadOnlyList<string> IMergeable.MatchesOn => _merge.MatchOn;
}
