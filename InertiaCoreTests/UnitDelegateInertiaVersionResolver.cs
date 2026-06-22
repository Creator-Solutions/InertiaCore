using InertiaCore.Resolvers;
using Microsoft.Extensions.DependencyInjection;

namespace InertiaCoreTests;

public class DelegateInertiaVersionResolverTests
{
    [Test]
    public void GetVersion_CallsFactory_AndReturnsResult()
    {
        // Arrange
        var expected = "dynamic-version";
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var resolver = new DelegateInertiaVersionResolver(sp => expected, serviceProvider);

        // Act
        var version = resolver.GetVersion();

        // Assert
        Assert.AreEqual(expected, version);
    }

    [Test]
    public void GetVersion_PassesServiceProvider_ToFactory()
    {
        // Arrange
        var serviceProvider = new ServiceCollection()
            .AddSingleton("test-value")
            .BuildServiceProvider();

        var resolver = new DelegateInertiaVersionResolver(sp =>
        {
            var val = sp.GetRequiredService<string>();
            return $"version-{val}";
        }, serviceProvider);

        // Act
        var version = resolver.GetVersion();

        // Assert
        Assert.AreEqual("version-test-value", version);
    }
}
