using InertiaCore;
using InertiaCore.Models;

namespace InertiaCoreTests;

public partial class Tests
{
    [Test]
    public async Task TestSharedProps()
    {
        var response = _factory.Render("Test/Page", new
        {
            Test = "Test"
        });

        _factory.Share("TestShared", "Shared");

        var context = PrepareContext();

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.That(page?.Props, Is.EqualTo(new Dictionary<string, object?>
        {
            { "test", "Test" },
            { "testShared", "Shared" },
            { "errors", new Dictionary<string, object?>() }
        }));
    }

    [Test]
    public async Task TestGlobalSharedProps()
    {
        Inertia.ClearSharedData();
        Inertia.Share("globalKey", "globalValue");

        var response = _factory.Render("Test/Page", new { Test = "Test" });
        var context = PrepareContext();
        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.That(page?.Props, Is.EqualTo(new Dictionary<string, object?>
        {
            { "test", "Test" },
            { "globalKey", "globalValue" },
            { "errors", new Dictionary<string, object?>() }
        }));
    }

    [Test]
    public async Task TestGlobalSharedMergePriority()
    {
        Inertia.ClearSharedData();
        Inertia.Share("key", "global");

        _factory.Share("key", "request");

        var response = _factory.Render("Test/Page", new { key = "component" });
        var context = PrepareContext();
        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.That(page?.Props, Is.EqualTo(new Dictionary<string, object?>
        {
            { "key", "component" },
            { "errors", new Dictionary<string, object?>() }
        }));
    }

    [Test]
    public async Task TestGlobalSharedWithClosure()
    {
        Inertia.ClearSharedData();
        Inertia.Share("dynamic", new Func<object?>(() => "resolved"));

        var response = _factory.Render("Test/Page", new { Test = "Test" });
        var context = PrepareContext();
        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.That(page?.Props, Is.EqualTo(new Dictionary<string, object?>
        {
            { "test", "Test" },
            { "dynamic", "resolved" },
            { "errors", new Dictionary<string, object?>() }
        }));
        Assert.That(page?.Props["dynamic"], Is.Not.InstanceOf<Func<object?>>());
    }

    [Test]
    public async Task TestRequestScopedShareStillWorks()
    {
        Inertia.ClearSharedData();

        var response = _factory.Render("Test/Page", new { Test = "Test" });
        _factory.Share("reqKey", "reqValue");
        var context = PrepareContext();
        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.That(page?.Props, Is.EqualTo(new Dictionary<string, object?>
        {
            { "test", "Test" },
            { "reqKey", "reqValue" },
            { "errors", new Dictionary<string, object?>() }
        }));
    }
}
