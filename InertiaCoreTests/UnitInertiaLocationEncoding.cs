using System.Net;
using InertiaCore.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace InertiaCoreTests;

/// <summary>
/// SC-03: verifies the X-Inertia-Location header written on a version conflict is
/// the encoded request URI and cannot be used for header injection.
/// </summary>
[TestFixture]
public class UnitInertiaLocationEncoding
{
    private static async Task<IHost> StartHostAsync()
    {
        return await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddControllersWithViews();
                        services.AddInertia();
                        services.AddInertiaVersion("server-v2");
                    })
                    .Configure(app =>
                    {
                        app.UseInertia();
                        app.Run(ctx =>
                        {
                            ctx.Response.ContentType = "text/plain";
                            return ctx.Response.WriteAsync("OK");
                        });
                    });
            })
            .StartAsync();
    }

    [Test]
    [Description("SC-03: a version conflict for an encoded CRLF path does not inject a response header.")]
    public async Task VersionConflict_EncodedCrLf_DoesNotInjectHeader()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();

        var request = new HttpRequestMessage(
            HttpMethod.Get,
            "http://localhost/some%0d%0aX-Injected:%20yes");
        request.Headers.Add("X-Inertia", "true");
        request.Headers.Add("X-Inertia-Version", "client-v1");

        var response = await client.SendAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(response.Headers.Contains("X-Injected"), Is.False, "CRLF produced an injected header");

            var location = response.Headers.GetValues("X-Inertia-Location").First();
            Assert.That(location, Does.Not.Contain("\r"));
            Assert.That(location, Does.Not.Contain("\n"));
        });
    }

    [Test]
    [Description("SC-03: the location preserves the encoded path and query verbatim.")]
    public async Task VersionConflict_PreservesEncodedPathAndQuery()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/some%20page?a=b%2Fc");
        request.Headers.Add("X-Inertia", "true");
        request.Headers.Add("X-Inertia-Version", "client-v1");

        var response = await client.SendAsync(request);
        var location = response.Headers.GetValues("X-Inertia-Location").First();

        Assert.That(location, Is.EqualTo("/some%20page?a=b%2Fc"));
    }
}
