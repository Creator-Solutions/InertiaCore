using System.Net;
using System.Net.Http.Json;
using InertiaCore;
using InertiaCore.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InertiaCoreTests;

/// <summary>
/// Integration tests for the Inertia anti-forgery bridge (IN-64): the XSRF cookie issued by
/// <c>UseInertiaAntiforgery()</c> lets a client that echoes it into the request header pass
/// token validation, while a client that does not is rejected.
/// </summary>
public abstract class UnitTestAntiforgeryBase : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("UseAntiforgery", "true");

    /// <summary>
    /// Copies <c>Set-Cookie</c> values into the next request's <c>Cookie</c> header, the way
    /// a browser would, so the token cookie issued on one response reaches the next request.
    /// </summary>
    protected sealed class CookieJar
    {
        private readonly HttpClient _client;
        private readonly Dictionary<string, string> _cookies = new(StringComparer.Ordinal);

        public CookieJar(HttpClient client) => _client = client;

        public IReadOnlyDictionary<string, string> Cookies => _cookies;

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            if (_cookies.Count > 0)
                request.Headers.Add("Cookie", string.Join("; ", _cookies.Select(p => $"{p.Key}={p.Value}")));

            var response = await _client.SendAsync(request);

            if (response.Headers.TryGetValues("Set-Cookie", out var values))
            {
                foreach (var value in values)
                {
                    var pair = value.Split(';')[0];
                    var idx = pair.IndexOf('=');
                    if (idx > 0)
                        _cookies[pair[..idx]] = pair[(idx + 1)..];
                }
            }

            return response;
        }

        public void SetCookie(string name, string value) => _cookies[name] = value;

        public void Clear() => _cookies.Clear();
    }

    private static HttpRequestMessage InertiaGet(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Inertia", "true");
        return request;
    }

    private static HttpRequestMessage Post(string path, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(new { })
        };

        if (token != null)
            request.Headers.Add("X-XSRF-TOKEN", token);

        return request;
    }

    [Test]
    [Description("SC: a client that echoes the XSRF cookie into the header can POST successfully.")]
    public async Task WithToken_Succeeds()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var jar = new CookieJar(client);

        // A page render issues the token cookie.
        await jar.SendAsync(InertiaGet("/csrf/page"));
        var token = jar.Cookies[InertiaAntiforgeryOptions.DefaultCookieName];

        var response = await jar.SendAsync(Post("/csrf", token));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    [Description("SC: a POST without the token header is rejected with 400.")]
    public async Task WithoutHeader_Returns400()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var jar = new CookieJar(client);

        await jar.SendAsync(InertiaGet("/csrf/page"));

        var response = await jar.SendAsync(Post("/csrf"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    [Description("The MVC AutoValidateAntiforgeryToken path also fails without the header and passes with it.")]
    public async Task Mvc_ValidatesToken()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var jar = new CookieJar(client);

        await jar.SendAsync(InertiaGet("/csrf/page"));
        var token = jar.Cookies[InertiaAntiforgeryOptions.DefaultCookieName];

        var missing = await jar.SendAsync(Post("/csrf-mvc"));
        var present = await jar.SendAsync(Post("/csrf-mvc", token));

        Assert.Multiple(() =>
        {
            Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(present.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });
    }

    [Test]
    [Description("The issued cookie is JavaScript-readable, SameSite=Lax, and not Secure over plain HTTP.")]
    public async Task Cookie_HasExpectedAttributes()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.SendAsync(InertiaGet("/csrf/page"));
        var setCookie = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith(InertiaAntiforgeryOptions.DefaultCookieName + "=", StringComparison.Ordinal));

        Assert.Multiple(() =>
        {
            Assert.That(setCookie, Does.Not.Contain("httponly"), "the client has to read the cookie");
            Assert.That(setCookie, Does.Contain("samesite=lax"));
            Assert.That(setCookie, Does.Not.Contain("secure"), "HTTP requests do not set Secure under SameAsRequest");
            Assert.That(setCookie, Does.Contain("path=/"));
        });
    }

    [Test]
    [Description("A request that does not need a token (plain GET, no page render) is not given the cookie.")]
    public async Task PlainGet_NoCookie()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/users");

        var hasXsrf = response.Headers.TryGetValues("Set-Cookie", out var values)
            && values.Any(value => value.StartsWith(InertiaAntiforgeryOptions.DefaultCookieName + "=", StringComparison.Ordinal));

        Assert.That(hasXsrf, Is.False);
    }

    [Test]
    [Description("RotateAntiforgery replaces the token cookie value.")]
    public async Task RotateAntiforgery_ChangesToken()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var jar = new CookieJar(client);

        await jar.SendAsync(InertiaGet("/csrf/page"));
        var before = jar.Cookies[InertiaAntiforgeryOptions.DefaultCookieName];

        var response = await jar.SendAsync(Post("/csrf-signin", before));
        var after = jar.Cookies[InertiaAntiforgeryOptions.DefaultCookieName];

        // The rotated token is itself valid for later requests.
        var followUp = await jar.SendAsync(Post("/csrf-mvc", after));

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(after, Is.Not.EqualTo(before), "sign-in must rotate the token");
            Assert.That(followUp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });
    }
}

/// <summary>Runs <see cref="UnitTestAntiforgeryBase"/> against the default WebApplicationFactory host.</summary>
[TestFixture]
public class UnitTestAntiforgery : UnitTestAntiforgeryBase
{
}

/// <summary>The bridge is a no-op when it is not enabled.</summary>
[TestFixture]
public class UnitTestAntiforgeryDisabled : WebApplicationFactory<Program>
{
    [Test]
    [Description("With the bridge disabled, even a mutating request gets no XSRF cookie.")]
    public async Task Disabled_NoCookie()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var request = new HttpRequestMessage(HttpMethod.Post, "/csrf")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new { })
        };
        var response = await client.SendAsync(request);

        var hasXsrf = response.Headers.TryGetValues("Set-Cookie", out var values)
            && values.Any(value => value.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));

        Assert.That(hasXsrf, Is.False);
    }
}
