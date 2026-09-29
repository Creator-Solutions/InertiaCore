using System.Text.Json;
using InertiaCore.Models;
using InertiaCore.Props;
using Microsoft.AspNetCore.Http;

namespace InertiaCoreTests;

public partial class Tests
{
    private static ScrollMetadata ScrollMeta() => new("page", 1, 3, 2);

    [Test]
    [Description("A scroll prop emits pageName, previousPage, nextPage and currentPage.")]
    public async Task TestScrollPropEmitsMetadata()
    {
        var response = _factory.Render("Test/Page", new
        {
            Posts = _factory.Scroll(new { data = new[] { 1, 2 } }, ScrollMeta())
        });

        var context = PrepareContext();

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.That(page?.ScrollProps, Is.Not.Null);
        var entry = page!.ScrollProps!["posts"];

        Assert.Multiple(() =>
        {
            Assert.That(entry.PageName, Is.EqualTo("page"));
            Assert.That(entry.PreviousPage, Is.EqualTo(1));
            Assert.That(entry.NextPage, Is.EqualTo(3));
            Assert.That(entry.CurrentPage, Is.EqualTo(2));
        });
    }

    [Test]
    [Description("Null previous/next tokens serialize as null rather than being omitted.")]
    public async Task TestScrollPropNullTokensSerializeAsNull()
    {
        var response = _factory.Render("Test/Page", new
        {
            Posts = _factory.Scroll(new { data = Array.Empty<int>() }, new ScrollMetadata("page", null, null, 1))
        });

        var context = PrepareContext();

        response.SetContext(context);
        await response.ProcessResponse();

        var json = JsonSerializer.Serialize(response.GetJson().Value, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"scrollProps\""));
            Assert.That(json, Does.Contain("\"previousPage\":null"));
            Assert.That(json, Does.Contain("\"nextPage\":null"));
            Assert.That(json, Does.Contain("\"currentPage\":1"));
        });
    }

    [Test]
    [Description("Loading forward (no intent header) marks the inner array for append merge.")]
    public async Task TestScrollNextPageMarksAppend()
    {
        var response = _factory.Render("Test/Page", new
        {
            Posts = _factory.Scroll(new { data = new[] { 1 } }, ScrollMeta())
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
    [Description("Loading backward (prepend intent) marks the inner array for prepend merge.")]
    public async Task TestScrollPreviousPageMarksPrepend()
    {
        var response = _factory.Render("Test/Page", new
        {
            Posts = _factory.Scroll(new { data = new[] { 1 } }, ScrollMeta())
        });

        var headers = new HeaderDictionary
        {
            { "X-Inertia-Infinite-Scroll-Merge-Intent", "prepend" }
        };

        var context = PrepareContext(headers);

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.Multiple(() =>
        {
            Assert.That(page?.PrependProps, Is.EqualTo(new List<string> { "posts.data" }));
            Assert.That(page?.MergeProps, Is.Null);
        });
    }

    [Test]
    [Description("X-Inertia-Reset marks the scroll entry as reset and drops its merge label.")]
    public async Task TestScrollResetHeader()
    {
        var response = _factory.Render("Test/Page", new
        {
            Posts = _factory.Scroll(new { data = new[] { 1 } }, ScrollMeta())
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
            Assert.That(page?.ScrollProps!["posts"].Reset, Is.True);
            Assert.That(page?.MergeProps, Is.Null);
            Assert.That(page?.PrependProps, Is.Null);
            Assert.That(page?.Props.ContainsKey("posts"), Is.True);
        });
    }

    [Test]
    [Description("A partial reload requesting only other props does not emit scroll metadata.")]
    public async Task TestScrollPropExcludedFromUnrelatedPartial()
    {
        var response = _factory.Render("Test/Page", new
        {
            Posts = _factory.Scroll(new { data = new[] { 1 } }, ScrollMeta()),
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
            Assert.That(page?.ScrollProps, Is.Null);
            Assert.That(page?.Props.ContainsKey("posts"), Is.False);
        });
    }

    [Test]
    [Description("A custom wrapper changes the merged path.")]
    public async Task TestScrollWrapperOverride()
    {
        var response = _factory.Render("Test/Page", new
        {
            Posts = _factory.Scroll(new { items = new[] { 1 } }, ScrollMeta(), "items")
        });

        var context = PrepareContext();

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.That(page?.MergeProps, Is.EqualTo(new List<string> { "posts.items" }));
    }

    [Test]
    [Description("A scroll prop supports an asynchronous factory.")]
    public async Task TestScrollAsyncFactory()
    {
        var response = _factory.Render("Test/Page", new
        {
            Posts = _factory.Scroll(() => Task.FromResult<object?>(new { data = new[] { 1 } }), ScrollMeta())
        });

        var context = PrepareContext();

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.Multiple(() =>
        {
            Assert.That(page?.ScrollProps!["posts"].CurrentPage, Is.EqualTo(2));
            Assert.That(page?.MergeProps, Is.EqualTo(new List<string> { "posts.data" }));
        });
    }
}
