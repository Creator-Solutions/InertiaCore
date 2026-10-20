using System;
using System.Threading;
using System.Threading.Tasks;
using InertiaCore.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InertiaCore.Props;

/// <summary>
/// Internal <see cref="InvokableProp"/> that resolves its value from the current
/// request's service provider. Created via the <c>Inertia.Prop&lt;T&gt;()</c> family.
/// </summary>
/// <typeparam name="T">The service type to resolve.</typeparam>
internal sealed class ResolvedProp<T> : InvokableProp, IInertiaProp<T>
    where T : class
{
    public ResolvedProp() : base(Sentinel)
    {
    }

    private static readonly object Sentinel = new();

    public async ValueTask<object?> ResolveAsync(HttpContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var services = context.RequestServices
            ?? throw new InvalidOperationException(
                $"Cannot resolve prop of type '{typeof(T).FullName}': no request service provider is available.");

        var resolved = services.GetRequiredService<T>();

        // A resolved service may itself be an async factory (Func<...>), an awaitable,
        // or another invokable prop; funnel it through the shared resolution pipeline.
        return await resolved.ResolveValueAsync(context, cancellationToken);
    }
}
