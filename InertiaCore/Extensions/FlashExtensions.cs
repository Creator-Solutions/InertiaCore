using System;
using System.Linq;
using InertiaCore.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InertiaCore.Extensions;

/// <summary>
/// Extension methods for Inertia flash persistence.
/// </summary>
public static class FlashExtensions
{
    /// <summary>
    /// Stores Inertia flash data in <see cref="IDistributedCache"/> instead of a cookie, so
    /// payloads larger than <c>InertiaFlashOptions.MaxCookiePayloadBytes</c> do not throw
    /// <c>InertiaFlashPayloadTooLargeException</c>.
    /// </summary>
    /// <remarks>
    /// An <c>IDistributedCache</c> must already be registered, for example with
    /// <c>services.AddDistributedMemoryCache()</c> or a real cache provider. The registration
    /// may happen before or after <c>AddInertia()</c>.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// No <see cref="IDistributedCache"/> registration was found.
    /// </exception>
    public static IServiceCollection AddInertiaDistributedCacheFlash(this IServiceCollection services)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));

        if (!services.Any(descriptor => descriptor.ServiceType == typeof(IDistributedCache)))
            throw new InvalidOperationException(
                "No IDistributedCache registration was found. Register one first, for example: " +
                "services.AddDistributedMemoryCache() (or a real distributed cache) and then call " +
                "services.AddInertiaDistributedCacheFlash().");

        services.Replace(ServiceDescriptor.Scoped<IInertiaFlashProvider, DistributedCacheFlashProvider>());
        return services;
    }
}
