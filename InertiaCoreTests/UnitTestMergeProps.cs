using InertiaCore.Models;
using InertiaCore.Props;
using Microsoft.AspNetCore.Http;

namespace InertiaCoreTests;

public partial class Tests
{
    [Test]
    [Description("A full visit with a merge prop returns mergeProps containing the prop name.")]
    public async Task TestMergePropOnFullVisit()
    {
        var response = _factory.Render("Test/Page", new
        {
            Posts = _factory.Merge(new[] { 1, 2, 3 })
        });

        var context = PrepareContext();

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.Multiple(() =>
        {
            Assert.That(page?.MergeProps, Is.EqualTo(new List<string> { "posts" }));
            Assert.That(page?.Props.ContainsKey("posts"), Is.True);
        });
    }

    [Test]
    [Description("Prepend adds the prop to prependProps.")]
    public async Task TestPrependProp()
    {
        var response = _factory.Render("Test/Page", new
        {
            Notifications = _factory.Merge(new[] { 1 }).Prepend()
        });

        var context = PrepareContext();

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.Multiple(() =>
        {
            Assert.That(page?.PrependProps, Is.EqualTo(new List<string> { "notifications" }));
            Assert.That(page?.MergeProps, Is.Null);
        });
    }

    [Test]
    [Description("DeepMerge adds the prop to deepMergeProps.")]
    public async Task TestDeepMergeProp()
    {
        var response = _factory.Render("Test/Page", new
        {
            Conversations = _factory.DeepMerge(new { data = new[] { 1, 2 } })
        });

        var context = PrepareContext();

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.Multiple(() =>
        {
            Assert.That(page?.DeepMergeProps, Is.EqualTo(new List<string> { "conversations" }));
            Assert.That(page?.MergeProps, Is.Null);
        });
    }

    [Test]
    [Description("MatchOn adds a dotted entry to matchPropsOn.")]
    public async Task TestMatchOnProp()
    {
        var response = _factory.Render("Test/Page", new
        {
            Posts = _factory.Merge(new[] { 1 }).MatchOn("id")
        });

        var context = PrepareContext();

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.Multiple(() =>
        {
            Assert.That(page?.MergeProps, Is.EqualTo(new List<string> { "posts" }));
            Assert.That(page?.MatchPropsOn, Is.EqualTo(new List<string> { "posts.id" }));
        });
    }

    [Test]
    [Description("MatchOn supports nested key fields such as data.id.")]
    public async Task TestMatchOnNestedKey()
    {
        var response = _factory.Render("Test/Page", new
        {
            Posts = _factory.Merge(new[] { 1 }).MatchOn("data.id")
        });

        var context = PrepareContext();

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.That(page?.MatchPropsOn, Is.EqualTo(new List<string> { "posts.data.id" }));
    }

    [Test]
    [Description("Append targets a nested path within the prop.")]
    public async Task TestAppendNestedPath()
    {
        var response = _factory.Render("Test/Page", new
        {
            Posts = _factory.Merge(new { data = new[] { 1 } }).Append("data")
        });

        var context = PrepareContext();

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.Multiple(() =>
        {
            Assert.That(page?.MergeProps, Is.EqualTo(new List<string> { "posts.data" }));
            Assert.That(page?.PrependProps, Is.Null);
        });
    }

    [Test]
    [Description("Metadata keys are camelCased the same way as prop names.")]
    public async Task TestMergeMetadataCamelCased()
    {
        var response = _factory.Render("Test/Page", new
        {
            TestMerge = _factory.Merge(1)
        });

        var context = PrepareContext();

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.Multiple(() =>
        {
            Assert.That(page?.Props.ContainsKey("testMerge"), Is.True);
            Assert.That(page?.MergeProps, Is.EqualTo(new List<string> { "testMerge" }));
        });
    }

    [Test]
    [Description("A partial reload requesting only other props does not list the merge prop.")]
    public async Task TestMergePropExcludedFromUnrelatedPartial()
    {
        var response = _factory.Render("Test/Page", new
        {
            Posts = _factory.Merge(new[] { 1 }),
            Other = "other"
        });

        var headers = new HeaderDictionary
        {
            { "X-Inertia-Partial-Data", "other" },
            { "X-Inertia-Partial-Component", "Test/Page" }
        };

        var context = PrepareContext(headers);

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.Multiple(() =>
        {
            Assert.That(page?.MergeProps, Is.Null);
            Assert.That(page?.Props.ContainsKey("posts"), Is.False);
            Assert.That(page?.Props["other"], Is.EqualTo("other"));
        });
    }

    [Test]
    [Description("A partial reload requesting the merge prop lists it.")]
    public async Task TestMergePropIncludedInRequestedPartial()
    {
        var response = _factory.Render("Test/Page", new
        {
            Posts = _factory.Merge(new[] { 1 }),
            Other = "other"
        });

        var headers = new HeaderDictionary
        {
            { "X-Inertia-Partial-Data", "posts" },
            { "X-Inertia-Partial-Component", "Test/Page" }
        };

        var context = PrepareContext(headers);

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.Multiple(() =>
        {
            Assert.That(page?.MergeProps, Is.EqualTo(new List<string> { "posts" }));
            Assert.That(page?.Props.ContainsKey("posts"), Is.True);
        });
    }

    [Test]
    [Description("X-Inertia-Reset removes the prop from mergeProps but keeps it in props.")]
    public async Task TestResetHeaderRemovesMergeMetadata()
    {
        var response = _factory.Render("Test/Page", new
        {
            Posts = _factory.Merge(new[] { 1 }),
            Other = "other"
        });

        var headers = new HeaderDictionary
        {
            { "X-Inertia-Reset", "posts" }
        };

        var context = PrepareContext(headers);

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.Multiple(() =>
        {
            Assert.That(page?.MergeProps, Is.Null);
            Assert.That(page?.Props.ContainsKey("posts"), Is.True);
        });
    }

    [Test]
    [Description("Defer(...).Merge() labels the prop as mergeable on a full visit.")]
    public async Task TestDeferredMergePropOnFullVisit()
    {
        var response = _factory.Render("Test/Page", new
        {
            Results = new DeferredProp(() => "deferred").Merge()
        });

        var context = PrepareContext();

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.Multiple(() =>
        {
            Assert.That(page?.MergeProps, Is.EqualTo(new List<string> { "results" }));
            Assert.That(page?.Props.ContainsKey("results"), Is.False);
        });
    }

    [Test]
    [Description("Merge supports an asynchronous factory.")]
    public async Task TestMergeAsyncFactory()
    {
        var response = _factory.Render("Test/Page", new
        {
            Data = _factory.Merge(() => Task.FromResult<object?>("async"))
        });

        var context = PrepareContext();

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.Multiple(() =>
        {
            Assert.That(page?.MergeProps, Is.EqualTo(new List<string> { "data" }));
            Assert.That(page?.Props["data"], Is.EqualTo("async"));
        });
    }
}
