namespace InertiaCore.Props;

/// <summary>
/// A prop resolved a single time and remembered by the client, then reused on
/// subsequent pages that include it. The page object advertises it through
/// <c>onceProps</c>, and requests that already hold the value (listed in the
/// <c>X-Inertia-Except-Once-Props</c> header) skip resolution entirely.
/// See: https://inertiajs.com/once-props
/// </summary>
public class OnceProp : InvokableProp
{
    internal OnceProp(Func<object?> value) : base(value) => Once();

    internal OnceProp(Func<Task<object?>> value) : base(value) => Once();

    /// <summary>
    /// Creates a once prop that resolves its value from another invokable prop, used by
    /// <c>Inertia.Once&lt;T&gt;()</c>.
    /// </summary>
    /// <param name="value">The invokable prop that produces the value.</param>
    internal OnceProp(InvokableProp value) : base(value) => Once();
}
