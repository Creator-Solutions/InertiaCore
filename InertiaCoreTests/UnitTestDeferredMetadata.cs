using InertiaCore.Models;
using InertiaCore.Props;
using Microsoft.AspNetCore.Http;

namespace InertiaCoreTests;

public partial class Tests
{
    [Test]
    [Description("Deferred props are announced in deferredProps grouped by group, with no values.")]
    public async Task TestDeferredMetadataGrouped()
    {
        var response = _factory.Render("Test/Page", new
        {
            Test = "Test",
            Permissions = new DeferredProp(() =>
            {
                Assert.Fail("Deferred props must not resolve on the initial visit.");
                return "permissions";
            }),
            Teams = new DeferredProp(() =>
            {
                Assert.Fail("Deferred props must not resolve on the initial visit.");
                return "teams";
            }, "attributes"),
            Projects = new DeferredProp(() =>
            {
                Assert.Fail("Deferred props must not resolve on the initial visit.");
                return "projects";
            }, "attributes")
        });

        var context = PrepareContext();

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.Multiple(() =>
        {
            Assert.That(page?.DeferredProps, Is.Not.Null);
            Assert.That(page!.DeferredProps!["default"], Is.EqualTo(new List<string> { "permissions" }));
            Assert.That(page.DeferredProps["attributes"], Is.EqualTo(new List<string> { "teams", "projects" }));
            Assert.That(page.Props.ContainsKey("permissions"), Is.False);
            Assert.That(page.Props.ContainsKey("teams"), Is.False);
            Assert.That(page.Props.ContainsKey("projects"), Is.False);
        });
    }

    [Test]
    [Description("A partial reload for a group returns only that group's props.")]
    public async Task TestDeferredGroupPartial()
    {
        var response = _factory.Render("Test/Page", new
        {
            Permissions = new DeferredProp(() => "permissions"),
            Teams = new DeferredProp(() => "teams", "attributes"),
            Projects = new DeferredProp(() => "projects", "attributes")
        });

        var headers = new HeaderDictionary
        {
            { "X-Inertia-Partial-Data", "teams,projects" },
            { "X-Inertia-Partial-Component", "Test/Page" }
        };

        var context = PrepareContext(headers);

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.Multiple(() =>
        {
            Assert.That(page?.DeferredProps, Is.Null);
            Assert.That(page?.Props.ContainsKey("teams"), Is.True);
            Assert.That(page?.Props.ContainsKey("projects"), Is.True);
            Assert.That(page?.Props.ContainsKey("permissions"), Is.False);
        });
    }
}
