using InertiaCore.Extensions;
using InertiaCore.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace InertiaCoreTests;

/// <summary>
/// SC-04: the empty-response back redirect must never be turned into an open redirect
/// through the Referer header.
/// <para>
/// The transport-level tests exercise the cases the in-process test host can deliver.
/// A literal tab/newline in the Referer is percent-encoded to <c>%09</c>/<c>%0A</c> by
/// <see cref="System.Net.Http.HttpClient"/> before it reaches the server, so the
/// control-character paths are covered by the direct <see cref="InertiaMiddleware.GetSafeBackUrl"/>
/// tests below instead.
/// </para>
/// </summary>
[TestFixture]
public class UnitBackRedirect
{
    private static async Task<IHost> StartHostAsync()
    {
        return await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services => { services.AddInertia(); })
                    .Configure(app =>
                    {
                        app.UseInertia();
                        app.Run(ctx =>
                        {
                            ctx.Response.StatusCode = 204;
                            return Task.CompletedTask;
                        });
                    });
            })
            .StartAsync();
    }

    private static async Task<string> LocationForRefererAsync(string? referer)
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/current");
        request.Headers.Add("X-Inertia", "true");
        if (referer != null)
            request.Headers.TryAddWithoutValidation("Referer", referer);

        var response = await client.SendAsync(request);
        return response.Headers.TryGetValues("Location", out var values) ? values.First() : "";
    }

    [Test]
    [Description("SC-04: a protocol-relative Referer is rejected.")]
    public async Task Referer_ProtocolRelative_IsRejected()
    {
        var location = await LocationForRefererAsync("//evil.example/path");

        Assert.That(location, Is.EqualTo("/current"));
    }

    [Test]
    [Description("SC-04: a same-origin absolute Referer is preserved.")]
    public async Task Referer_SameOriginAbsolute_IsPreserved()
    {
        var location = await LocationForRefererAsync("http://localhost/current?tab=1");

        Assert.That(location, Is.EqualTo("/current?tab=1"));
    }

    [Test]
    [Description("SC-04: an off-origin absolute Referer falls back to the request path.")]
    public async Task Referer_OffOrigin_IsRejected()
    {
        var location = await LocationForRefererAsync("https://evil.example/steal");

        Assert.That(location, Is.EqualTo("/current"));
    }

    [Test]
    [Description("SC-04: an ordinary relative Referer is preserved.")]
    public async Task Referer_OrdinaryRelative_IsPreserved()
    {
        var location = await LocationForRefererAsync("/settings");

        Assert.That(location, Is.EqualTo("/settings"));
    }

    [TestCase("/\t/evil.example", TestName = "Tab")]
    [TestCase("/\n/evil.example", TestName = "Newline")]
    [TestCase("/\r/evil.example", TestName = "CarriageReturn")]
    [TestCase("/ /evil.example", TestName = "Space")]
    [TestCase("/\u000b/evil.example", TestName = "VerticalTab")]
    [Description("SC-04: relative Referer values containing control characters or whitespace are rejected.")]
    public void GetSafeBackUrl_ControlOrWhitespace_IsRejected(string referer)
    {
        var context = CreateContext();
        context.Request.Headers["Referer"] = referer;

        Assert.That(InertiaMiddleware.GetSafeBackUrl(context), Is.EqualTo("/current"));
    }

    [TestCase("//evil.example", TestName = "ProtocolRelative")]
    [TestCase("/\\evil.example", TestName = "SlashBackslash")]
    [TestCase("\\evil.example", TestName = "LeadingBackslash")]
    [Description("SC-04: values that can normalize to a protocol-relative URL are rejected.")]
    public void GetSafeBackUrl_NormalizableToProtocolRelative_IsRejected(string referer)
    {
        var context = CreateContext();
        context.Request.Headers["Referer"] = referer;

        Assert.That(InertiaMiddleware.GetSafeBackUrl(context), Is.EqualTo("/current"));
    }

    [Test]
    [Description("SC-04: an ordinary relative value is preserved.")]
    public void GetSafeBackUrl_OrdinaryRelative_IsPreserved()
    {
        var context = CreateContext();
        context.Request.Headers["Referer"] = "/settings?tab=2";

        Assert.That(InertiaMiddleware.GetSafeBackUrl(context), Is.EqualTo("/settings?tab=2"));
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost");
        context.Request.Path = "/current";
        return context;
    }
}
