using InertiaCore.Contracts;
using InertiaCore.Models;
using InertiaCore.Resolvers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InertiaCore.Extensions;

public static class InertiaServiceCollectionExtensions
{

    public static IServiceCollection AddInertiaVersion(this IServiceCollection services, string version)
    {
        services.AddSingleton<IInertiaVersionResolver>(new StaticInertiaVersionResolver(version));
        return services;
    }

    public static IServiceCollection AddInertiaVersion(this IServiceCollection services, Func<IServiceProvider, string> versionFactory)
    {
        services.AddSingleton<IInertiaVersionResolver>(sp =>
            new DelegateInertiaVersionResolver(versionFactory, sp));
        return services;
    }

    public static IServiceCollection AddInertiaVersion<TResolver>(this IServiceCollection services)
        where TResolver : class, IInertiaVersionResolver
    {
        services.AddSingleton<IInertiaVersionResolver, TResolver>();
        return services;
    }
}