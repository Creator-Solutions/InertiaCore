using System.Net;
using InertiaCore;
using InertiaCore.Extensions;
using InertiaCore.Models;
using InertiaCore.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InertiaCoreTests;

public partial class Tests
{
    [Test]
    [Description("GET with version mismatch returns 409 Conflict with X-Inertia-Location header.")]
    public async Task TestVersionConflictOnGet()
    {
        _factory.Version("server-version");

        var headers = new HeaderDictionary
        {
            { "X-Inertia", "true" },
            { "X-Inertia-Version", "old-version" }
        };

        var context = PrepareContext(headers);
        context.HttpContext.Request.Method = "GET";

        var calledNext = false;
        await RunVersionCheck(context, () => calledNext = true, _factory.GetVersion);

        Assert.Multiple(() =>
        {
            Assert.That(context.HttpContext.Response.StatusCode, Is.EqualTo((int)HttpStatusCode.Conflict));
            Assert.That(context.HttpContext.Response.Headers["X-Inertia-Location"].ToString(), Is.Not.Empty);
            Assert.That(calledNext, Is.False);
        });
    }

    [TestCase("POST")]
    [TestCase("PUT")]
    [TestCase("PATCH")]
    [TestCase("DELETE")]
    [Description("Non-GET requests never receive a version-mismatch 409 (protocol requirement).")]
    public async Task TestNoVersionConflictOnNonGet(string method)
    {
        _factory.Version("server-version");

        var headers = new HeaderDictionary
        {
            { "X-Inertia", "true" },
            { "X-Inertia-Version", "old-version" }
        };

        var context = PrepareContext(headers);
        context.HttpContext.Request.Method = method;

        var calledNext = false;
        await RunVersionCheck(context, () => calledNext = true, _factory.GetVersion);

        Assert.Multiple(() =>
        {
            Assert.That(context.HttpContext.Response.StatusCode, Is.Not.EqualTo((int)HttpStatusCode.Conflict));
            Assert.That(calledNext, Is.True);
        });
    }

    [Test]
    [Description("Request with matching version does NOT return 409.")]
    public async Task TestNoConflictOnMatchingVersion()
    {
        _factory.Version("test-version");

        var headers = new HeaderDictionary
        {
            { "X-Inertia", "true" },
            { "X-Inertia-Version", "test-version" }
        };

        var context = PrepareContext(headers);
        context.HttpContext.Request.Method = "GET";

        var calledNext = false;
        await RunVersionCheck(context, () => calledNext = true, _factory.GetVersion);

        Assert.Multiple(() =>
        {
            Assert.That(context.HttpContext.Response.StatusCode, Is.Not.EqualTo((int)HttpStatusCode.Conflict));
            Assert.That(calledNext, Is.True);
        });
    }

    [Test]
    [Description("Non-Inertia request with version mismatch does NOT return 409.")]
    public async Task TestNoConflictOnNonInertiaRequest()
    {
        _factory.Version("server-version");

        var headers = new HeaderDictionary
        {
            { "X-Inertia-Version", "old-version" }
        };

        var context = PrepareContext(headers);
        context.HttpContext.Request.Method = "GET";

        var calledNext = false;
        await RunVersionCheck(context, () => calledNext = true, _factory.GetVersion);

        Assert.That(calledNext, Is.True);
    }

    [Test]
    [Description("When no version is configured, the version check is skipped.")]
    public async Task TestNoConflictWhenNoVersionConfigured()
    {
        _factory.Version((string?)null);

        var headers = new HeaderDictionary
        {
            { "X-Inertia", "true" },
            { "X-Inertia-Version", "client-version" }
        };

        var context = PrepareContext(headers);
        context.HttpContext.Request.Method = "GET";

        var calledNext = false;
        await RunVersionCheck(context, () => calledNext = true, _factory.GetVersion);

        Assert.That(calledNext, Is.True);
    }

    /// <summary>
    /// Simulates the Inertia version-check middleware logic directly.
    /// </summary>
    private static async Task RunVersionCheck(ActionContext actionContext, Action next, Func<string?> getVersion)
    {
        var context = actionContext.HttpContext;

        var serverVersion = getVersion();
        if (!string.IsNullOrEmpty(serverVersion)
            && context.IsInertiaRequest()
            && HttpMethods.IsGet(context.Request.Method)
            && context.Request.Headers[InertiaHeader.Version] != serverVersion)
        {
            context.Response.Headers.Override(InertiaHeader.Location, context.RequestedUri());
            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
            await context.Response.CompleteAsync();
            return;
        }

        next();
        await Task.CompletedTask;
    }
}
