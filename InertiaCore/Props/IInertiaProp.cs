using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace InertiaCore.Props;

/// <summary>
/// A prop whose value is produced by resolving a service from the current request's
/// service provider. Created via <c>Inertia.Prop&lt;T&gt;()</c> and its modifiers
/// <c>Lazy&lt;T&gt;()</c>, <c>Defer&lt;T&gt;(group)</c> and <c>Once&lt;T&gt;()</c>.
/// <para>
/// The target type <typeparamref name="T"/> is resolved from
/// <see cref="HttpContext.RequestServices"/> for every request, so it may depend on
/// scoped services (for example an application service or a <c>DbContext</c>).
/// </para>
/// </summary>
/// <typeparam name="T">The service type whose instance is used as the prop value.</typeparam>
public interface IInertiaProp<out T>
{
    /// <summary>
    /// Resolves the prop value from the given request's service provider.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="cancellationToken">
    /// The token that cancels resolution when the request is aborted.
    /// </param>
    /// <returns>The resolved prop value.</returns>
    ValueTask<object?> ResolveAsync(HttpContext context, CancellationToken cancellationToken);
}
