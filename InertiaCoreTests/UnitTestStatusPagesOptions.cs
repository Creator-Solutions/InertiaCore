using InertiaCore.Models;
using Microsoft.AspNetCore.Http;

namespace InertiaCoreTests;

/// <summary>
/// Unit tests for <see cref="InertiaStatusPagesOptions"/>: default messages, per-status
/// overrides, custom components and the message factory.
/// </summary>
[TestFixture]
public class UnitTestStatusPagesOptions
{
    [Test]
    [Description("Defaults target the built-in Error component and the six handled statuses.")]
    public void Defaults()
    {
        var options = new InertiaStatusPagesOptions();

        Assert.Multiple(() =>
        {
            Assert.That(options.Enabled, Is.False, "opt-in by default");
            Assert.That(options.Component, Is.EqualTo("Error"));
            Assert.That(options.StatusCodes, Is.EquivalentTo(new[] { 401, 403, 404, 419, 500, 503 }));
            Assert.That(options.RespectDeveloperExceptionPage, Is.True);
        });
    }

    [Test]
    [Description("Each handled status has a built-in message.")]
    public void DefaultMessages()
    {
        Assert.Multiple(() =>
        {
            Assert.That(InertiaStatusPagesOptions.DefaultMessage(401), Is.EqualTo("Unauthorized"));
            Assert.That(InertiaStatusPagesOptions.DefaultMessage(403), Is.EqualTo("Forbidden"));
            Assert.That(InertiaStatusPagesOptions.DefaultMessage(404), Is.EqualTo("Not Found"));
            Assert.That(InertiaStatusPagesOptions.DefaultMessage(419), Is.EqualTo("Page Expired"));
            Assert.That(InertiaStatusPagesOptions.DefaultMessage(500), Is.EqualTo("Server Error"));
            Assert.That(InertiaStatusPagesOptions.DefaultMessage(503), Is.EqualTo("Service Unavailable"));
        });
    }

    [Test]
    [Description("ResolveMessage applies the defaults, then per-status overrides.")]
    public void ResolveMessage_PrefersOverride()
    {
        var options = new InertiaStatusPagesOptions();
        Assert.That(options.ResolveMessage(new DefaultHttpContext(), 404), Is.EqualTo("Not Found"));

        options.Messages[404] = "Gone fishing";

        Assert.Multiple(() =>
        {
            Assert.That(options.ResolveMessage(new DefaultHttpContext(), 404), Is.EqualTo("Gone fishing"));
            Assert.That(options.ResolveMessage(new DefaultHttpContext(), 403), Is.EqualTo("Forbidden"));
        });
    }

    [Test]
    [Description("MessageFactory takes precedence over per-status overrides.")]
    public void ResolveMessage_MessageFactoryWins()
    {
        var options = new InertiaStatusPagesOptions
        {
            Messages = { [404] = "override" },
            MessageFactory = (_, status) => $"status {status}",
        };

        Assert.That(options.ResolveMessage(new DefaultHttpContext(), 404), Is.EqualTo("status 404"));
    }

    [Test]
    [Description("ResolveComponent applies the default component, then per-status overrides.")]
    public void ResolveComponent_PrefersOverride()
    {
        var options = new InertiaStatusPagesOptions();
        Assert.That(options.ResolveComponent(404), Is.EqualTo("Error"));

        options.Components[503] = "Maintenance";

        Assert.Multiple(() =>
        {
            Assert.That(options.ResolveComponent(503), Is.EqualTo("Maintenance"));
            Assert.That(options.ResolveComponent(404), Is.EqualTo("Error"));
        });
    }

    [Test]
    [Description("An empty per-status component falls back to the default rather than rendering nothing.")]
    public void ResolveComponent_EmptyOverrideFallsBack()
    {
        var options = new InertiaStatusPagesOptions { Components = { [404] = "" } };

        Assert.That(options.ResolveComponent(404), Is.EqualTo("Error"));
    }
}
