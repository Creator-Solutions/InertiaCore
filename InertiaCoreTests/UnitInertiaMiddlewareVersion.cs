using InertiaCore.Extensions;
using InertiaCore.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace InertiaCoreTests;

[TestFixture]
public class UnitInertiaMiddlewareVersion
{
    [Test]
    public async Task Middleware_Sets_XInertiaVersion_Header()
    {
        // Arrange
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services => { services.AddInertiaVersion("test-123"); })
                    .Configure(app =>
                    {
                        app.UseMiddleware<InertiaMiddleware>();
                        // FIX: Use app.Run() instead of app.Use()
                        app.Run(async ctx =>
                        {
                            // Simulate an Inertia response
                            ctx.Items["InertiaResponse"] = true;
                            ctx.Response.ContentType = "text/html";
                            await ctx.Response.WriteAsync("Hello");
                        });
                    });
            })
            .StartAsync();

        var client = host.GetTestClient();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        var response = await client.SendAsync(request);

        // Assert
        Assert.IsTrue(response.Headers.Contains("X-Inertia-Version"));
        var versionHeader = response.Headers.GetValues("X-Inertia-Version").First();
        // FIX: Use NUnit 4 syntax
        Assert.That(versionHeader, Is.EqualTo("test-123"));
    }

    [Test]
    public async Task Middleware_StoresVersion_InHttpContextItems()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services => { services.AddInertiaVersion("context-version"); })
                    .Configure(app =>
                    {
                        app.UseMiddleware<InertiaMiddleware>();
                        // Fixed: use app.Run instead of app.Use
                        app.Run(async ctx =>
                        {
                            var version = ctx.Items["InertiaVersion"] as string;
                            await ctx.Response.WriteAsync(version ?? "missing");
                        });
                    });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var response = await client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        Assert.That(content, Is.EqualTo("context-version"));
    }

    [Test]
    public async Task Middleware_IncludesVersion_InPageData_WhenPresent()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services => { services.AddInertiaVersion("page-data-version"); })
                    .Configure(app =>
                    {
                        // 👇 1. Set page data BEFORE InertiaMiddleware
                        app.Use(async (ctx, next) =>
                        {
                            ctx.Items["InertiaPageData"] = new Dictionary<string, object> { { "foo", "bar" } };
                            await next();
                        });

                        // 👇 2. InertiaMiddleware runs – now it sees the dictionary
                        app.UseMiddleware<InertiaMiddleware>();

                        // 👇 3. Terminal middleware reads the updated dictionary
                        app.Run(async ctx =>
                        {
                            var pageData = ctx.Items["InertiaPageData"] as IDictionary<string, object>;
                            var version = pageData?["version"] as string ?? "none";
                            await ctx.Response.WriteAsync(version);
                        });
                    });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var response = await client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        Assert.That(content, Is.EqualTo("page-data-version"));
    }

    [Test]
    public async Task Middleware_Sets_XInertiaHeader_And_VersionHeader_ForInertiaRequest()
    {
        // Arrange
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services => services.AddInertiaVersion("abc123"))
                    .Configure(app =>
                    {
                        app.UseMiddleware<InertiaMiddleware>();
                        app.Run(ctx => ctx.Response.WriteAsync("OK"));
                    });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Inertia", "true"); // simulate Inertia request

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.IsTrue(response.Headers.Contains("X-Inertia"));
        Assert.That(response.Headers.GetValues("X-Inertia").First(), Is.EqualTo("true"));
        Assert.IsTrue(response.Headers.Contains("X-Inertia-Version"));
        Assert.That(response.Headers.GetValues("X-Inertia-Version").First(), Is.EqualTo("abc123"));
    }

    [Test]
    public async Task Middleware_DoesNotAddInertiaHeaders_ForNonInertiaRequest()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services => { services.AddInertiaVersion("some-version"); })
                    .Configure(app =>
                    {
                        app.UseMiddleware<InertiaMiddleware>();
                        app.Run(ctx => ctx.Response.WriteAsync("OK"));
                    });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/");

        var response = await client.SendAsync(request);

        Assert.That(response.Headers.Contains("X-Inertia"), Is.False);
        Assert.That(response.Headers.Contains("X-Inertia-Version"), Is.False);
    }
}