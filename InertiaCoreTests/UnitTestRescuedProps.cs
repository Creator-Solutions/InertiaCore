using InertiaCore.Models;
using InertiaCore.Props;
using Microsoft.AspNetCore.Http;

namespace InertiaCoreTests;

public partial class Tests
{
    [Test]
    [Description("A rescued deferred prop that throws is omitted and listed in rescuedProps.")]
    public async Task TestRescuedDeferredProp()
    {
        var response = _factory.Render("Test/Page", new
        {
            Permissions = new DeferredProp(() => throw new InvalidOperationException("boom"), rescue: true),
            Other = "ok"
        });

        var headers = new HeaderDictionary
        {
            { "X-Inertia-Partial-Data", "permissions,other" },
            { "X-Inertia-Partial-Component", "Test/Page" }
        };

        var context = PrepareContext(headers);

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.Multiple(() =>
        {
            Assert.That(page?.RescuedProps, Is.EqualTo(new List<string> { "permissions" }));
            Assert.That(page?.Props.ContainsKey("permissions"), Is.False);
            Assert.That(page?.Props["other"], Is.EqualTo("ok"));
        });
    }

    [Test]
    [Description("A throwing non-rescued prop still fails the request.")]
    public void TestNonRescuedPropThrows()
    {
        var response = _factory.Render("Test/Page", new
        {
            Permissions = new DeferredProp(() => throw new InvalidOperationException("boom"))
        });

        var headers = new HeaderDictionary
        {
            { "X-Inertia-Partial-Data", "permissions" },
            { "X-Inertia-Partial-Component", "Test/Page" }
        };

        var context = PrepareContext(headers);
        response.SetContext(context);

        Assert.ThrowsAsync<InvalidOperationException>(() => response.ProcessResponse());
    }

    [Test]
    [Description("Rescue can be enabled fluently and does not affect other props.")]
    public async Task TestRescueFluent()
    {
        var response = _factory.Render("Test/Page", new
        {
            A = new DeferredProp(() => "a").Rescue(),
            B = new DeferredProp(() => throw new InvalidOperationException("boom")).Rescue()
        });

        var headers = new HeaderDictionary
        {
            { "X-Inertia-Partial-Data", "a,b" },
            { "X-Inertia-Partial-Component", "Test/Page" }
        };

        var context = PrepareContext(headers);

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.Multiple(() =>
        {
            Assert.That(page?.RescuedProps, Is.EqualTo(new List<string> { "b" }));
            Assert.That(page?.Props["a"], Is.EqualTo("a"));
            Assert.That(page?.Props.ContainsKey("b"), Is.False);
        });
    }
}
