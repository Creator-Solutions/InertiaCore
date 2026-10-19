using InertiaCore.Extensions;
using InertiaCore.Middleware;
using InertiaCore.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InertiaCoreTests;

/// <summary>
/// Unit tests for the Inertia anti-forgery bridge options, middleware helpers and service
/// registration (IN-64).
/// </summary>
[TestFixture]
public class UnitTestAntiforgeryOptions
{
    [Test]
    [Description("Options default to the Inertia client's XSRF cookie and header names, disabled.")]
    public void Defaults_MatchInertiaClient()
    {
        var options = new InertiaAntiforgeryOptions();

        Assert.Multiple(() =>
        {
            Assert.That(options.Enabled, Is.False);
            Assert.That(options.CookieName, Is.EqualTo("XSRF-TOKEN"));
            Assert.That(options.HeaderName, Is.EqualTo("X-XSRF-TOKEN"));
            Assert.That(options.SameSite, Is.EqualTo(SameSiteMode.Lax));
            Assert.That(options.SecurePolicy, Is.EqualTo(CookieSecurePolicy.SameAsRequest));
            Assert.That(options.CookiePath, Is.EqualTo("/"));
        });
    }

    [Test]
    [Description("InertiaOptions carries a non-null antiforgery options instance.")]
    public void InertiaOptions_HasAntiforgeryDefaults()
    {
        Assert.That(new InertiaOptions().Antiforgery, Is.Not.Null);
    }

    [Test]
    [Description("A safe request without the Inertia header does not need a token.")]
    public void NeedsToken_PlainGet_IsFalse()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;

        Assert.That(InertiaAntiforgeryMiddleware.NeedsToken(context), Is.False);
    }

    [Test]
    [Description("A safe Inertia page render needs a token.")]
    public void NeedsToken_InertiaGet_IsTrue()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Headers["X-Inertia"] = "true";

        Assert.That(InertiaAntiforgeryMiddleware.NeedsToken(context), Is.True);
    }

    [Test]
    [Description("Any mutating request needs a token.")]
    public void NeedsToken_Mutating_IsTrue()
    {
        foreach (var method in new[] { HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete })
        {
            var context = new DefaultHttpContext();
            context.Request.Method = method;
            Assert.That(InertiaAntiforgeryMiddleware.NeedsToken(context), Is.True, method);
        }
    }

    [Test]
    [Description("AddInertia registers anti-forgery and aligns the header when the bridge is enabled.")]
    public void AddInertia_EnablesHeaderAlignment()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInertia(options => options.Antiforgery.Enabled = true);

        using var provider = services.BuildServiceProvider();
        var antiforgery = provider.GetRequiredService<IOptions<Microsoft.AspNetCore.Antiforgery.AntiforgeryOptions>>().Value;

        Assert.That(antiforgery.HeaderName, Is.EqualTo("X-XSRF-TOKEN"));
    }

    [Test]
    [Description("When the bridge is disabled, the anti-forgery header keeps its default.")]
    public void AddInertia_Disabled_KeepsDefaultHeader()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInertia();

        using var provider = services.BuildServiceProvider();
        var antiforgery = provider.GetRequiredService<IOptions<Microsoft.AspNetCore.Antiforgery.AntiforgeryOptions>>().Value;

        Assert.That(antiforgery.HeaderName, Is.EqualTo("RequestVerificationToken"));
    }

    [Test]
    [Description("ScheduleCookie stores the latest pending token, so a rotation replaces the value.")]
    public void ScheduleCookie_KeepsLatestPendingToken()
    {
        var context = new DefaultHttpContext();
        var settings = new InertiaAntiforgeryOptions();

        InertiaAntiforgeryMiddleware.ScheduleCookie(context, settings, "first");
        InertiaAntiforgeryMiddleware.ScheduleCookie(context, settings, "rotated");

        Assert.That(context.Items[InertiaAntiforgeryMiddleware.PendingTokenKey], Is.EqualTo("rotated"));
    }

    [Test]
    [Description("WriteCookie writes a JavaScript-readable cookie with SameSite=Lax and no Secure over HTTP.")]
    public void WriteCookie_Attributes_OverHttp()
    {
        var context = new DefaultHttpContext();
        var settings = new InertiaAntiforgeryOptions();

        InertiaAntiforgeryMiddleware.WriteCookie(context, settings, "token");

        var setCookie = context.Response.Headers.SetCookie.ToString();
        Assert.Multiple(() =>
        {
            Assert.That(setCookie, Does.Contain("XSRF-TOKEN=token"));
            Assert.That(setCookie, Does.Not.Contain("httponly"));
            Assert.That(setCookie, Does.Contain("samesite=lax"));
            Assert.That(setCookie, Does.Not.Contain("secure"));
        });
    }

    [Test]
    [Description("SecurePolicy.Always sets Secure even over plain HTTP.")]
    public void WriteCookie_SecureAlways()
    {
        var context = new DefaultHttpContext();
        var settings = new InertiaAntiforgeryOptions { SecurePolicy = CookieSecurePolicy.Always };

        InertiaAntiforgeryMiddleware.WriteCookie(context, settings, "token");

        Assert.That(context.Response.Headers.SetCookie.ToString(), Does.Contain("secure"));
    }
}
