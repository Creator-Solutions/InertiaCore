using InertiaCore.Extensions;

namespace InertiaCore.Props;

/// <summary>
/// Base type for lazily-evaluated Inertia props such as <see cref="LazyProp"/>,
/// <see cref="AlwaysProp"/>, <see cref="DeferredProp"/>, <see cref="MergeProp"/> and
/// <see cref="OnceProp"/>.
/// <para>
/// Merge behaviour can be composed onto any prop using the fluent
/// <see cref="Merge"/>, <see cref="DeepMerge"/>, <see cref="Prepend()"/>,
/// <see cref="Append(string[])"/> and <see cref="MatchOn(string[])"/> methods, and
/// once behaviour using <see cref="Once"/>, <see cref="As"/>, <see cref="Fresh"/> and
/// <see cref="Until(TimeSpan)"/>.
/// </para>
/// </summary>
public class InvokableProp : IMergeable, IOnceable
{
    private readonly object? _value;
    private readonly MergeState _merge = new();
    private readonly OnceState _once = new();

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

    /// <summary>
    /// Marks the prop to be resolved once and remembered by the client, so it is
    /// skipped on subsequent visits where the client already holds it.
    /// See: https://inertiajs.com/once-props
    /// </summary>
    /// <param name="value">Whether the prop should resolve once. Defaults to <see langword="true"/>.</param>
    /// <returns>The same prop, for chaining.</returns>
    public InvokableProp Once(bool value = true)
    {
        _once.Once = value;
        return this;
    }

    /// <summary>
    /// Sets a custom key used to identify the once prop across pages, so the same data
    /// can be shared under different prop names.
    /// </summary>
    /// <param name="key">The once key.</param>
    /// <returns>The same prop, for chaining.</returns>
    public InvokableProp As(string key)
    {
        _once.Key = key;
        return this;
    }

    /// <summary>
    /// Forces the once prop to be sent to the client even if the client already holds it.
    /// </summary>
    /// <param name="value">Whether to force a refresh. Defaults to <see langword="true"/>.</param>
    /// <returns>The same prop, for chaining.</returns>
    public InvokableProp Fresh(bool value = true)
    {
        _once.Fresh = value;
        return this;
    }

    /// <summary>
    /// Sets an expiration for the once prop, after which the client refreshes it.
    /// </summary>
    /// <param name="ttl">The time-to-live relative to now.</param>
    /// <returns>The same prop, for chaining.</returns>
    public InvokableProp Until(TimeSpan ttl)
    {
        _once.ExpiresAt = DateTimeOffset.UtcNow.Add(ttl).ToUnixTimeMilliseconds();
        return this;
    }

    /// <summary>
    /// Sets an absolute expiration for the once prop, after which the client refreshes it.
    /// </summary>
    /// <param name="until">The absolute expiration instant.</param>
    /// <returns>The same prop, for chaining.</returns>
    public InvokableProp Until(DateTimeOffset until)
    {
        _once.ExpiresAt = until.ToUnixTimeMilliseconds();
        return this;
    }

    bool IMergeable.ShouldMerge => _merge.Merge;

    bool IMergeable.ShouldDeepMerge => _merge.DeepMerge;

    bool IMergeable.AppendsAtRoot => _merge.AppendsAtRoot;

    bool IMergeable.PrependsAtRoot => _merge.PrependsAtRoot;

    IReadOnlyList<string> IMergeable.AppendsAtPaths => _merge.AppendsAtPaths;

    IReadOnlyList<string> IMergeable.PrependsAtPaths => _merge.PrependsAtPaths;

    IReadOnlyList<string> IMergeable.MatchesOn => _merge.MatchOn;

    bool IOnceable.ShouldResolveOnce => _once.Once;

    string? IOnceable.Key => _once.Key;

    long? IOnceable.ExpiresAt => _once.ExpiresAt;

    bool IOnceable.ForceRefresh => _once.Fresh;

    /// <summary>
    /// Clears any nested append/prepend paths so a caller (such as <see cref="ScrollProp"/>)
    /// can set a single merge direction for the current request.
    /// </summary>
    internal void ClearMergePaths()
    {
        _merge.AppendsAtPaths.Clear();
        _merge.PrependsAtPaths.Clear();
    }
}
