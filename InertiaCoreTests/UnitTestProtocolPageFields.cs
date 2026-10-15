using System.Text.Json;
using InertiaCore;
using InertiaCore.Extensions;
using InertiaCore.Models;
using InertiaCore.Services;
using InertiaCore.Ssr;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Moq;

namespace InertiaCoreTests;

public partial class Tests
{
    private static string Serialize(object value) =>
        JsonSerializer.Serialize(value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    [Test]
    [Description("encryptHistory is emitted when set.")]
    public async Task TestEncryptHistorySerializedWhenSet()
    {
        _factory.EncryptHistory();

        var response = _factory.Render("Test/Page");
        var context = PrepareContext();
        response.SetContext(context);
        await response.ProcessResponse();

        var json = Serialize(response.GetJson().Value!);
        Assert.That(json, Does.Contain("\"encryptHistory\":true"));
    }

    [Test]
    [Description("clearHistory is emitted when set.")]
    public async Task TestClearHistorySerializedWhenSet()
    {
        _factory.ClearHistory();

        var response = _factory.Render("Test/Page");
        var context = PrepareContext();
        response.SetContext(context);
        await response.ProcessResponse();

        var json = Serialize(response.GetJson().Value!);
        Assert.That(json, Does.Contain("\"clearHistory\":true"));
    }

    [Test]
    [Description("History flags are omitted when not set.")]
    public async Task TestHistoryFlagsOmittedWhenFalse()
    {
        var response = _factory.Render("Test/Page");
        var context = PrepareContext();
        response.SetContext(context);
        await response.ProcessResponse();

        var json = Serialize(response.GetJson().Value!);
        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Not.Contain("encryptHistory"));
            Assert.That(json, Does.Not.Contain("clearHistory"));
        });
    }

    [Test]
    [Description("sharedProps lists the keys registered via Inertia.Share.")]
    public async Task TestSharedPropsMetadata()
    {
        _factory.Share("auth", new { user = "jane" });
        _factory.Share("appName", "My App");

        var response = _factory.Render("Test/Page", new { Page = "specific" });
        var context = PrepareContext();
        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.Multiple(() =>
        {
            Assert.That(page?.SharedProps, Is.EquivalentTo(new[] { "auth", "appName" }));
            Assert.That(Serialize(page!), Does.Contain("\"sharedProps\""));
        });
    }

    [Test]
    [Description("sharedProps is omitted when nothing is shared.")]
    public async Task TestSharedPropsOmittedWhenEmpty()
    {
        var response = _factory.Render("Test/Page", new { Test = "Test" });
        var context = PrepareContext();
        response.SetContext(context);
        await response.ProcessResponse();

        Assert.That(Serialize(response.GetJson().Value!), Does.Not.Contain("sharedProps"));
    }

    [Test]
    [Description("A preserveFragment flag set on a redirect is emitted on the target page.")]
    public async Task TestPreserveFragmentSerializedWhenSet()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.PreserveInertiaFragment();

        var factory = new ResponseFactory(
            new HttpContextAccessor { HttpContext = httpContext },
            new Mock<IGateway>().Object,
            Options.Create(new InertiaOptions()),
            new InertiaState(),
            Options.Create(new JsonOptions()));

        var response = factory.Render("Test/Page");

        var actionContext = new ActionContext(
            httpContext, new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
        response.SetContext(actionContext);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.Multiple(() =>
        {
            Assert.That(page?.PreserveFragment, Is.True);
            Assert.That(Serialize(page!), Does.Contain("\"preserveFragment\":true"));
        });
    }
}
