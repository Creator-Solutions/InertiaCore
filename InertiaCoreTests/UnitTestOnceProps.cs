using System.Text.Json;
using InertiaCore.Models;
using Microsoft.AspNetCore.Http;

namespace InertiaCoreTests;

public partial class Tests
{
    [Test]
    [Description("A once prop emits onceProps metadata and resolves normally.")]
    public async Task TestOnceMetadata()
    {
        var response = _factory.Render("Test/Page", new
        {
            Plans = _factory.Once(() => new[] { 1, 2 })
        });

        var context = PrepareContext();

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.Multiple(() =>
        {
            Assert.That(page?.OnceProps, Is.Not.Null);
            Assert.That(page!.OnceProps!["plans"].Prop, Is.EqualTo("plans"));
            Assert.That(page.OnceProps["plans"].ExpiresAt, Is.Null);
            Assert.That(page.Props.ContainsKey("plans"), Is.True);
        });
    }

    [Test]
    [Description("A once prop already held by the client is skipped and its callback is not invoked.")]
    public async Task TestOnceSkippedWhenClientHoldsIt()
    {
        var invoked = false;
        var response = _factory.Render("Test/Page", new
        {
            Plans = _factory.Once(() =>
            {
                invoked = true;
                return new[] { 1 };
            })
        });

        var headers = new HeaderDictionary
        {
            { "X-Inertia-Except-Once-Props", "plans" }
        };

        var context = PrepareContext(headers);

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.Multiple(() =>
        {
            Assert.That(invoked, Is.False);
            Assert.That(page?.Props.ContainsKey("plans"), Is.False);
            Assert.That(page?.OnceProps!["plans"].Prop, Is.EqualTo("plans"));
        });
    }

    [Test]
    [Description("A partial reload requesting a once prop resolves it even when except-once is present.")]
    public async Task TestOnceResolvedOnPartialEvenIfExceptOnce()
    {
        var invoked = false;
        var response = _factory.Render("Test/Page", new
        {
            Plans = _factory.Once(() =>
            {
                invoked = true;
                return new[] { 1 };
            })
        });

        var headers = new HeaderDictionary
        {
            { "X-Inertia-Except-Once-Props", "plans" },
            { "X-Inertia-Partial-Data", "plans" },
            { "X-Inertia-Partial-Component", "Test/Page" }
        };

        var context = PrepareContext(headers);

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.Multiple(() =>
        {
            Assert.That(invoked, Is.True);
            Assert.That(page?.Props.ContainsKey("plans"), Is.True);
        });
    }

    [Test]
    [Description("A once prop supports a custom key and expiry.")]
    public async Task TestOnceCustomKeyAndExpiry()
    {
        var response = _factory.Render("Test/Page", new
        {
            MemberRoles = _factory.Once(() => new[] { 1 }).As("roles").Until(TimeSpan.FromHours(1))
        });

        var context = PrepareContext();

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.Multiple(() =>
        {
            Assert.That(page?.OnceProps!.ContainsKey("roles"), Is.True);
            Assert.That(page!.OnceProps!["roles"].Prop, Is.EqualTo("memberRoles"));
            Assert.That(page.OnceProps["roles"].ExpiresAt, Is.Not.Null);
        });
    }

    [Test]
    [Description("onceProps expiresAt is emitted as null when unset.")]
    public async Task TestOnceExpiresAtNullSerialized()
    {
        var response = _factory.Render("Test/Page", new
        {
            Plans = _factory.Once(() => new[] { 1 })
        });

        var context = PrepareContext(new HeaderDictionary { { "X-Inertia", "true" } });

        response.SetContext(context);
        await response.ProcessResponse();

        var json = JsonSerializer.Serialize(response.GetJson().Value, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"onceProps\""));
            Assert.That(json, Does.Contain("\"expiresAt\":null"));
        });
    }
}
