using InertiaCore.Extensions;
using InertiaCore.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace InertiaCoreTests;

[TestFixture]
public class UnitEmptyResponse
{
    [Test]
    [TestCase("GET", 302)]
    [TestCase("POST", 303)]
    [TestCase("PUT", 303)]
    [TestCase("DELETE", 303)]
    public async Task Middleware_ConvertsEmpty204_ToRedirect(string method, int expectedStatusCode)
    {
        // Arrange
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services => 
                    { 
                        services.AddInertiaVersion("test-123"); 
                    })
                    .Configure(app =>
                    {
                        app.UseMiddleware<InertiaMiddleware>();
                        app.Run(async ctx =>
                        {
                            ctx.Response.StatusCode = 204;
                            await Task.CompletedTask;
                        });
                    });
            })
            .StartAsync();

        var client = host.GetTestClient();

        // Act
        var request = new HttpRequestMessage(new HttpMethod(method), "/");
        request.Headers.Add("X-Inertia", "true");
        request.Headers.Add("Referer", "/previous-page");

        var response = await client.SendAsync(request);

        // Assert
        Assert.That((int)response.StatusCode, Is.EqualTo(expectedStatusCode));
        Assert.IsTrue(response.Headers.Contains("Location"));
        var locationHeader = response.Headers.GetValues("Location").First();
        Assert.That(locationHeader, Is.EqualTo("/previous-page"));
    }

    [Test]
    [TestCase("GET", 302)]
    [TestCase("POST", 303)]
    public async Task Middleware_ConvertsEmpty200_ToRedirect(string method, int expectedStatusCode)
    {
        // Arrange
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services => 
                    { 
                        services.AddInertiaVersion("test-123"); 
                    })
                    .Configure(app =>
                    {
                        app.UseMiddleware<InertiaMiddleware>();
                        app.Run(async ctx =>
                        {
                            ctx.Response.StatusCode = 200;
                            // Empty response, no content-type or body written
                            await Task.CompletedTask;
                        });
                    });
            })
            .StartAsync();

        var client = host.GetTestClient();

        // Act
        var request = new HttpRequestMessage(new HttpMethod(method), "/");
        request.Headers.Add("X-Inertia", "true");
        request.Headers.Add("Referer", "/previous-page");

        var response = await client.SendAsync(request);

        // Assert
        Assert.That((int)response.StatusCode, Is.EqualTo(expectedStatusCode));
        Assert.IsTrue(response.Headers.Contains("Location"));
        var locationHeader = response.Headers.GetValues("Location").First();
        Assert.That(locationHeader, Is.EqualTo("/previous-page"));
    }

    [Test]
    public async Task Middleware_DoesNotRedirect_NonInertiaRequest()
    {
        // Arrange
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services => 
                    { 
                        services.AddInertiaVersion("test-123"); 
                    })
                    .Configure(app =>
                    {
                        app.UseMiddleware<InertiaMiddleware>();
                        app.Run(async ctx =>
                        {
                            ctx.Response.StatusCode = 204;
                            await Task.CompletedTask;
                        });
                    });
            })
            .StartAsync();

        var client = host.GetTestClient();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, "/");
        // No X-Inertia header

        var response = await client.SendAsync(request);

        // Assert
        Assert.That((int)response.StatusCode, Is.EqualTo(204));
        Assert.IsFalse(response.Headers.Contains("Location"));
    }

    [Test]
    public async Task Middleware_DoesNotRedirect_ValidInertiaResponse()
    {
        // Arrange
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services => 
                    { 
                        services.AddInertiaVersion("test-123"); 
                    })
                    .Configure(app =>
                    {
                        app.UseMiddleware<InertiaMiddleware>();
                        app.Run(async ctx =>
                        {
                            ctx.Response.StatusCode = 200;
                            ctx.Items["InertiaResponse"] = true; // Mark as valid Inertia response
                            ctx.Response.ContentType = "application/json";
                            await ctx.Response.WriteAsync("{\"component\":\"Test\"}");
                        });
                    });
            })
            .StartAsync();

        var client = host.GetTestClient();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, "/");
        request.Headers.Add("X-Inertia", "true");

        var response = await client.SendAsync(request);

        // Assert
        Assert.That((int)response.StatusCode, Is.EqualTo(200));
        Assert.IsFalse(response.Headers.Contains("Location"));
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Is.EqualTo("{\"component\":\"Test\"}"));
    }
}
