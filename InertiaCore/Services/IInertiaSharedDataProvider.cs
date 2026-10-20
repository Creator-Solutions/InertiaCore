using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace InertiaCore.Services;

/// <summary>
/// Contributes shared props to every Inertia response. Register any number of providers
/// with <c>services.AddInertiaSharedData&lt;T&gt;()</c>; they are resolved per request from
/// the request's service scope and merged in registration order.
/// <para>
/// A provider is the right place to build shared data from scoped services — the current
/// user, permissions, feature flags or a database context — without holding them in a
/// startup delegate.
/// </para>
/// <para>
/// Returned values may be plain values, <c>Func&lt;object?&gt;</c> factories, or lazy and
/// deferred props (<c>IInertia.Lazy</c> / <c>IInertia.Defer</c>); they are resolved by the
/// same pipeline as component props.
/// </para>
/// </summary>
public interface IInertiaSharedDataProvider
{
    /// <summary>
    /// Produces the shared props contributed by this provider for the current request.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="cancellationToken">A token that is cancelled when the request is aborted.</param>
    /// <returns>
    /// The shared props keyed by prop name. Keys are camelCased on merge. Return an empty
    /// dictionary to contribute nothing.
    /// </returns>
    ValueTask<IReadOnlyDictionary<string, object?>> GetSharedDataAsync(
        HttpContext context,
        CancellationToken cancellationToken);
}
