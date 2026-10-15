using System;
using InertiaCore.Contracts;
using InertiaCore.Models;
using InertiaCore.Resolvers;
using InertiaCore.Services;
using InertiaCore.Services.Version;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InertiaCore.Extensions;

public static class InertiaServiceCollectionExtensions
{

    public static IServiceCollection AddInertiaVersion(this IServiceCollection services, string version)
    {
        services.TryAddScoped<IErrorBagService, ErrorBagService>();
        services.AddSingleton<IInertiaVersionProvider>(new DefaultInertiaVersionProvider(version));
        services.AddSingleton<IInertiaVersionResolver>(new StaticInertiaVersionResolver(version));
        return services;
    }

    public static IServiceCollection AddInertiaVersion(this IServiceCollection services, Func<IServiceProvider, string> versionFactory)
    {
        services.TryAddScoped<IErrorBagService, ErrorBagService>();

        // Registered as scoped so the captured IServiceProvider is the per-request
        // scope rather than the root provider. A singleton registration would turn
        // any request-scoped dependency used by the factory into an application-wide
        // singleton shared between requests (a captive dependency).
        services.AddScoped<IInertiaVersionProvider>(sp => new DelegateInertiaVersionProvider(() => versionFactory(sp)));
        services.AddScoped<IInertiaVersionResolver>(sp =>
            new DelegateInertiaVersionResolver(versionFactory, sp));
        return services;
    }

    public static IServiceCollection AddInertiaVersion<TResolver>(this IServiceCollection services)
        where TResolver : class, IInertiaVersionResolver
    {
        services.TryAddScoped<IErrorBagService, ErrorBagService>();

        // Scoped for the same reason as the delegate overload: a custom resolver may
        // depend on request-scoped services and must not be shared between requests.
        services.AddScoped<IInertiaVersionResolver, TResolver>();
        services.AddScoped<IInertiaVersionProvider>(sp =>
        {
            var resolver = sp.GetRequiredService<IInertiaVersionResolver>();
            return new DelegateInertiaVersionProvider(() => resolver.GetVersion());
        });
        return services;
    }
}