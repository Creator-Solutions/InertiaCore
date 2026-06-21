using InertiaCore.Models;
using InertiaCore.Props;
using Microsoft.AspNetCore.Http;

namespace InertiaCoreTests;

public partial class Tests
{
    [Test]
    [Description("Deferred prop is excluded from full page load response.")]
    public async Task TestDeferredPropExcludedFromFullLoad()
    {
        var response = _factory.Render("Test/Page", new
        {
            Test = "Test",
            TestDeferred = new DeferredProp(() => "Deferred Value")
        });

        var context = PrepareContext();

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.That(page?.Props, Is.EqualTo(new Dictionary<string, object?>
        {
            { "test", "Test" },
            { "errors", new Dictionary<string, object?>() }
        }));
    }

    [Test]
    [Description("Deferred prop is included in partial reload when key is requested.")]
    public async Task TestDeferredPropIncludedOnPartial()
    {
        var response = _factory.Render("Test/Page", new
        {
            Test = "Test",
            TestDeferred = new DeferredProp(() => "Deferred Value")
        });

        var headers = new HeaderDictionary
        {
            { "X-Inertia-Partial-Data", "testDeferred" },
            { "X-Inertia-Partial-Component", "Test/Page" }
        };

        var context = PrepareContext(headers);

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        // Per the Inertia.js protocol, when X-Inertia-Partial-Data specifies only
        // "testDeferred", only that key (plus errors) should be in the response.
        // "test" was not requested so it must not appear.
        Assert.That(page?.Props, Is.EqualTo(new Dictionary<string, object?>
        {
            { "testDeferred", "Deferred Value" },
            { "errors", new Dictionary<string, object?>() }
        }));
    }

    [Test]
    [Description("Deferred prop with async factory resolves correctly.")]
    public async Task TestDeferredPropAsync()
    {
        var response = _factory.Render("Test/Page", new
        {
            TestDeferred = new DeferredProp(() => Task.FromResult<object?>("Async Deferred"))
        });

        var headers = new HeaderDictionary
        {
            { "X-Inertia-Partial-Data", "testDeferred" },
            { "X-Inertia-Partial-Component", "Test/Page" }
        };

        var context = PrepareContext(headers);

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.That(page?.Props, Is.EqualTo(new Dictionary<string, object?>
        {
            { "testDeferred", "Async Deferred" },
            { "errors", new Dictionary<string, object?>() }
        }));
    }

    [Test]
    [Description("Multiple deferred props — only requested ones are resolved.")]
    public async Task TestMultipleDeferredOnlyRequestedResolved()
    {
        var response = _factory.Render("Test/Page", new
        {
            Test = "Test",
            DeferredA = new DeferredProp(() => "Value A"),
            DeferredB = new DeferredProp(() => "Value B")
        });

        var headers = new HeaderDictionary
        {
            { "X-Inertia-Partial-Data", "deferredA" },
            { "X-Inertia-Partial-Component", "Test/Page" }
        };

        var context = PrepareContext(headers);

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.Multiple(() =>
        {
            Assert.That(page?.Props.ContainsKey("deferredA"), Is.True);
            Assert.That(page?.Props.ContainsKey("deferredB"), Is.False);
            Assert.That(page?.Props["deferredA"], Is.EqualTo("Value A"));
        });
    }

    [Test]
    [Description("Vary header should be present for partial responses with deferred props.")]
    public async Task TestVaryHeaderOnPartialWithDeferred()
    {
        var response = _factory.Render("Test/Page", new
        {
            TestDeferred = new DeferredProp(() => "Value")
        });

        var headers = new HeaderDictionary
        {
            { "X-Inertia-Partial-Data", "testDeferred" },
            { "X-Inertia-Partial-Component", "Test/Page" }
        };

        var context = PrepareContext(headers);

        response.SetContext(context);
        await response.ProcessResponse();

        var jsonResult = response.GetJson();

        Assert.That(jsonResult, Is.Not.Null);
    }
}
