using InertiaCore.Contracts;
using InertiaCore.Extensions;
using InertiaCore.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;


namespace InertiaCoreTests;

public partial class Tests
{
    [Test]
    [Description("Resolving IInertia from DI returns an InertiaService instance.")]
    public void TestIInertiaResolvesFromDI()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddInertia();

        var serviceProvider = builder.Services.BuildServiceProvider();
        var inertia = serviceProvider.GetRequiredService<IInertia>();

        Assert.That(inertia, Is.InstanceOf<InertiaService>());
    }

    [Test]
    [Description("Resolving IInertia twice from the same scope returns the same instance.")]
    public void TestIInertiaIsScopedSameInstance()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddInertia();

        var serviceProvider = builder.Services.BuildServiceProvider();

        using var scope1 = serviceProvider.CreateScope();
        var inertia1 = scope1.ServiceProvider.GetRequiredService<IInertia>();
        var inertia2 = scope1.ServiceProvider.GetRequiredService<IInertia>();

        Assert.That(inertia1, Is.SameAs(inertia2));
    }

    [Test]
    [Description("Resolving IInertia from different scopes returns different instances.")]
    public void TestIInertiaScopedIsolation()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddInertia();

        var serviceProvider = builder.Services.BuildServiceProvider();

        IInertia inertia1, inertia2;
        using (var scope1 = serviceProvider.CreateScope())
        {
            inertia1 = scope1.ServiceProvider.GetRequiredService<IInertia>();
        }

        using (var scope2 = serviceProvider.CreateScope())
        {
            inertia2 = scope2.ServiceProvider.GetRequiredService<IInertia>();
        }

        Assert.That(inertia1, Is.Not.SameAs(inertia2));
    }

    [Test]
    [Description("InertiaState is registered as Scoped and isolated per scope.")]
    public void TestInertiaStateScopeIsolation()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddInertia();

        var serviceProvider = builder.Services.BuildServiceProvider();

        InertiaState state1, state2;
        using (var scope1 = serviceProvider.CreateScope())
        {
            state1 = scope1.ServiceProvider.GetRequiredService<InertiaState>();
            state1.SharedProps["key"] = "value from scope 1";
        }

        using (var scope2 = serviceProvider.CreateScope())
        {
            state2 = scope2.ServiceProvider.GetRequiredService<InertiaState>();
            state2.SharedProps["key"] = "value from scope 2";
        }

        Assert.That(state1.SharedProps["key"], Is.EqualTo("value from scope 1"));
        Assert.That(state2.SharedProps["key"], Is.Not.EqualTo(state1.SharedProps["key"]));
    }

    [Test]
    [Description("Calling Share() on IInertia does not bleed into another scope.")]
    public void TestSharedDataDoesNotBleedAcrossScopes()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddInertia();

        var serviceProvider = builder.Services.BuildServiceProvider();

        using (var scope1 = serviceProvider.CreateScope())
        {
            var inertia = scope1.ServiceProvider.GetRequiredService<IInertia>();
            inertia.Share("secret", "from scope 1");
        }

        using (var scope2 = serviceProvider.CreateScope())
        {
            var state = scope2.ServiceProvider.GetRequiredService<InertiaState>();
            Assert.That(state.SharedProps.ContainsKey("secret"), Is.False);
        }
    }

    [Test]
    [Description("IInertia.Render returns an IActionResult.")]
    public async Task TestInertiaServiceRenderReturnsIActionResult()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddInertia();

        var serviceProvider = builder.Services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var inertia = scope.ServiceProvider.GetRequiredService<IInertia>();

        var result = await inertia.Render("Test/Page", new { Test = "Data" });

        Assert.That(result, Is.InstanceOf<IActionResult>());
    }
}
