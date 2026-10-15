using InertiaCore.Contracts;
using InertiaCore.Extensions;
using InertiaCore.Services.Version;
using Microsoft.Extensions.DependencyInjection;

namespace InertiaCoreTests;

/// <summary>
/// SC-05: version resolvers registered through AddInertiaVersion must not capture the
/// root IServiceProvider, which would turn request-scoped dependencies into
/// application-wide singletons shared between requests.
/// </summary>
[TestFixture]
public class UnitInertiaVersionLifetime
{
    private sealed class ScopedId
    {
        public string Value { get; } = Guid.NewGuid().ToString();
    }

    private sealed class ScopedDependentResolver : IInertiaVersionResolver
    {
        private readonly ScopedId _id;

        public ScopedDependentResolver(ScopedId id) => _id = id;

        public string GetVersion() => _id.Value;
    }

    private static string ResolveResolver(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IInertiaVersionResolver>().GetVersion();
    }

    private static string ResolveProvider(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IInertiaVersionProvider>().GetVersion();
    }

    [Test]
    [Description("SC-05: a delegate resolver resolves its scoped dependency per scope.")]
    public void AddInertiaVersion_Delegate_Resolver_UsesScopedDependency()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedId>();
        services.AddInertiaVersion(sp => sp.GetRequiredService<ScopedId>().Value);

        using var provider = services.BuildServiceProvider();

        Assert.That(ResolveResolver(provider), Is.Not.EqualTo(ResolveResolver(provider)));
    }

    [Test]
    [Description("SC-05: a delegate provider resolves its scoped dependency per scope.")]
    public void AddInertiaVersion_Delegate_Provider_UsesScopedDependency()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedId>();
        services.AddInertiaVersion(sp => sp.GetRequiredService<ScopedId>().Value);

        using var provider = services.BuildServiceProvider();

        Assert.That(ResolveProvider(provider), Is.Not.EqualTo(ResolveProvider(provider)));
    }

    [Test]
    [Description("SC-05: a custom resolver type is created per scope, not shared.")]
    public void AddInertiaVersion_CustomResolver_UsesScopedDependency()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedId>();
        services.AddInertiaVersion<ScopedDependentResolver>();

        using var provider = services.BuildServiceProvider();

        Assert.That(ResolveResolver(provider), Is.Not.EqualTo(ResolveResolver(provider)));
    }
}
