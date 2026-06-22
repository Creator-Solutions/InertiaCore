using System.Net;
using InertiaCore.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace InertiaCoreTests;

public class UnitInertiaVersionConflict
{
    [Test]
    public async Task Middleware_Returns409_WhenClientVersionDiffers()
    {
        using var host = await new HostBuilder()
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
                        app.UseInertia(); // conflict middleware
                        app.Run(ctx => ctx.Response.WriteAsync("OK"));
                    });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/some-page");
        request.Headers.Add("X-Inertia", "true");
        request.Headers.Add("X-Inertia-Version", "client-v1");

        foreach (var value in request.Headers)
        {
            Console.WriteLine("Header: " + value.Key + ": " + value.Value.FirstOrDefault());
        }

        var response = await client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(response.Headers.Contains("X-Inertia-Location"), Is.True);
        Assert.That(
            response.Headers.GetValues("X-Inertia-Location").First(),
            Is.EqualTo("/some-page"));
    }

    [Test]
    public async Task Middleware_DoesNotConflict_WhenVersionsMatch()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddControllersWithViews();
                        services.AddInertia();
                        services.AddInertiaVersion("same-version");
                    })
                    .Configure(app =>
                    {
                        app.UseInertia();
                        app.Run(ctx => ctx.Response.WriteAsync("OK"));
                    });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/");
        request.Headers.Add("X-Inertia", "true");
        request.Headers.Add("X-Inertia-Version", "same-version");

        var response = await client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Is.EqualTo("OK"));
    }
}