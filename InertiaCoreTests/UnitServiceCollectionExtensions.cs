using InertiaCore.Contracts;
using InertiaCore.Extensions;
using InertiaCore.Resolvers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InertiaCoreTests;

[TestFixture]
public class ServiceCollectionExtensionsTests
{
    [Test]
    public void AddInertiaVersion_WithStatic_RegistersStaticResolver()
    {
        var services = new ServiceCollection();
        services.AddInertiaVersion("test-version");

        var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<IInertiaVersionResolver>();

        Assert.That(resolver, Is.InstanceOf<StaticInertiaVersionResolver>());
        var staticResolver = (StaticInertiaVersionResolver)resolver;
        Assert.That(staticResolver.GetVersion(), Is.EqualTo("test-version"));
    }

    [Test]
    public void AddInertiaVersion_WithDelegate_RegistersDelegateResolver()
    {
        var services = new ServiceCollection();
        services.AddInertiaVersion(sp => "delegate-version");

        var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<IInertiaVersionResolver>();

        Assert.That(resolver, Is.InstanceOf<DelegateInertiaVersionResolver>());
        var delegateResolver = (DelegateInertiaVersionResolver)resolver;
        Assert.That(delegateResolver.GetVersion(), Is.EqualTo("delegate-version"));
    }

    [Test]
    public void AddInertiaVersion_WithCustomType_RegistersCustomResolver()
    {
        var services = new ServiceCollection();
        services.AddInertiaVersion<CustomResolver>();

        var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<IInertiaVersionResolver>();

        Assert.That(resolver, Is.InstanceOf<CustomResolver>());
    }

    [Test]
    public void TryAdd_Fallback_OnlyAddsIfNotAlreadyRegistered()
    {
        var services = new ServiceCollection();

        // Pre-register a custom resolver
        services.AddSingleton<IInertiaVersionResolver>(new StaticInertiaVersionResolver("custom"));

        // Fallback – should not replace the existing registration
        services.TryAddSingleton<IInertiaVersionResolver>(sp =>
            new StaticInertiaVersionResolver("fallback"));

        var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<IInertiaVersionResolver>();

        Assert.That(resolver, Is.InstanceOf<StaticInertiaVersionResolver>());
        var staticResolver = (StaticInertiaVersionResolver)resolver;
        Assert.That(staticResolver.GetVersion(), Is.EqualTo("custom"));
    }

    // Dummy custom resolver for testing
    private class CustomResolver : IInertiaVersionResolver
    {
        public string GetVersion() => "custom-version";
    }
}