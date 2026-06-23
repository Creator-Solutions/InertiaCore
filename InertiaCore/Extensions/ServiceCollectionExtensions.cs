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
        services.AddSingleton<IInertiaVersionProvider>(sp => new DelegateInertiaVersionProvider(() => versionFactory(sp)));
        services.AddSingleton<IInertiaVersionResolver>(sp =>
            new DelegateInertiaVersionResolver(versionFactory, sp));
        return services;
    }

    public static IServiceCollection AddInertiaVersion<TResolver>(this IServiceCollection services)
        where TResolver : class, IInertiaVersionResolver
    {
        services.TryAddScoped<IErrorBagService, ErrorBagService>();
        services.AddSingleton<IInertiaVersionResolver, TResolver>();
        services.AddSingleton<IInertiaVersionProvider>(sp =>
        {
            var resolver = sp.GetRequiredService<IInertiaVersionResolver>();
            return new DelegateInertiaVersionProvider(() => resolver.GetVersion());
        });
        return services;
    }
}