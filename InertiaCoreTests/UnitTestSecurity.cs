using InertiaCore;
using InertiaCore.Extensions;
using InertiaCore.Models;
using InertiaCore.Services;
using InertiaCore.Ssr;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace InertiaCoreTests;

[TestFixture]
public class UnitTestSecurity
{
    [Test]
    [Description("Static facade Share is scoped to the current request and never leaks globally.")]
    public void StaticFacade_Share_IsRequestScoped()
    {
        var services = new ServiceCollection();
        services.AddInertia();
        using var provider = services.BuildServiceProvider();
        using var scopeA = provider.CreateScope();
        using var scopeB = provider.CreateScope();

        var ctxA = new DefaultHttpContext { RequestServices = scopeA.ServiceProvider };
        var ctxB = new DefaultHttpContext { RequestServices = scopeB.ServiceProvider };

        var accessor = new HttpContextAccessor();
        Inertia.UseContextAccessor(accessor);

        try
        {
            Inertia.ClearSharedData();

            accessor.HttpContext = ctxA;
            Inertia.Share("Key", "A");

            accessor.HttpContext = ctxB;
            Inertia.Share("Key", "B");

            var stateA = scopeA.ServiceProvider.GetRequiredService<InertiaState>();
            var stateB = scopeB.ServiceProvider.GetRequiredService<InertiaState>();

            Assert.Multiple(() =>
            {
                Assert.That(stateA.SharedProps["key"], Is.EqualTo("A"));
                Assert.That(stateB.SharedProps["key"], Is.EqualTo("B"));
                Assert.That(Inertia.GetSharedData(), Is.Empty, "Request-scoped shares must not touch the global registry.");
            });
        }
        finally
        {
            Inertia.UseContextAccessor(null);
        }
    }

    [Test]
    [Description("Static facade Share outside a request populates the global registry (startup sharing).")]
    public void StaticFacade_Share_OutsideRequest_IsGlobal()
    {
        Inertia.UseContextAccessor(null);
        Inertia.ClearSharedData();
        try
        {
            Inertia.Share("AppName", "My App");
            Assert.That(Inertia.GetSharedData()["appName"], Is.EqualTo("My App"));
        }
        finally
        {
            Inertia.ClearSharedData();
        }
    }

    [Test]
    [Description("ValidatePages rejects undeclared components and accepts registered ones.")]
    public void ValidatePages_EnforcesRegistry()
    {
        var factory = CreateFactory(new InertiaOptions { ValidatePages = true });

        Assert.Throws<InvalidOperationException>(() => factory.Render("Unknown/Page"));

        InertiaPageRegistry.Register("Known/Page");
        Assert.DoesNotThrow(() => factory.Render("Known/Page"));
    }

    [Test]
    [Description("Validation is disabled by default so existing apps are unaffected.")]
    public void ValidatePages_DisabledByDefault()
    {
        var factory = CreateFactory(new InertiaOptions());
        Assert.DoesNotThrow(() => factory.Render("Any/Page"));
    }

    private static ResponseFactory CreateFactory(InertiaOptions options)
    {
        var contextAccessor = new Mock<IHttpContextAccessor>();
        var httpClientFactory = new Mock<IHttpClientFactory>();

        var inertiaOptions = new Mock<IOptions<InertiaOptions>>();
        inertiaOptions.SetupGet(x => x.Value).Returns(options);

        var jsonOptions = new Mock<IOptions<JsonOptions>>();
        jsonOptions.SetupGet(x => x.Value).Returns(new JsonOptions());

        return new ResponseFactory(
            contextAccessor.Object,
            new Gateway(httpClientFactory.Object),
            inertiaOptions.Object,
            new InertiaState(),
            jsonOptions.Object);
    }
}
