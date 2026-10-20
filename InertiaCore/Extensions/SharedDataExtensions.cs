using System;
using InertiaCore.Services;
using Microsoft.Extensions.DependencyInjection;

namespace InertiaCore.Extensions;

/// <summary>
/// Extension methods for registering Inertia shared-data providers.
/// </summary>
public static class SharedDataExtensions
{
    /// <summary>
    /// Registers a shared-data provider that runs for every Inertia response.
    /// </summary>
    /// <remarks>
    /// Providers are resolved per request from the request's scope, so a provider may depend
    /// on scoped services (for example a <c>DbContext</c>). Multiple providers may be
    /// registered; they execute in registration order, and a later provider overrides the
    /// keys of an earlier one. The registration may happen before or after <c>AddInertia()</c>.
    /// </remarks>
    /// <typeparam name="TProvider">The provider type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddInertiaSharedData<TProvider>(this IServiceCollection services)
        where TProvider : class, IInertiaSharedDataProvider
    {
        if (services is null) throw new ArgumentNullException(nameof(services));

        services.AddScoped<IInertiaSharedDataProvider, TProvider>();
        return services;
    }
}
