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
                            // Empty response: no content-type, no body, explicit zero length
                            ctx.Response.ContentLength = 0;
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
    public async Task Middleware_ConvertsEmpty200_ToRedirect_WhenContentLengthNull(string method, int expectedStatusCode)
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
                            // Empty response: no content-type and no explicit content length
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
    [Description("An external Referer must not be used as the redirect target (open redirect).")]
    public async Task Middleware_RejectsExternalReferer()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder => webBuilder
                .UseTestServer()
                .ConfigureServices(services => services.AddInertiaVersion("test-123"))
                .Configure(app =>
                {
                    app.UseMiddleware<InertiaMiddleware>();
                    app.Run(ctx =>
                    {
                        ctx.Response.StatusCode = 204;
                        return Task.CompletedTask;
                    });
                }))
            .StartAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/dashboard");
        request.Headers.Add("X-Inertia", "true");
        request.Headers.Add("Referer", "https://evil.example/phish");

        var response = await host.GetTestClient().SendAsync(request);

        Assert.That(response.Headers.Contains("Location"), Is.True);
        Assert.That(response.Headers.GetValues("Location").First(), Is.EqualTo("/dashboard"));
    }

    [Test]
    [Description("A protocol-relative Referer must not be used as the redirect target.")]
    public async Task Middleware_RejectsProtocolRelativeReferer()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder => webBuilder
                .UseTestServer()
                .ConfigureServices(services => services.AddInertiaVersion("test-123"))
                .Configure(app =>
                {
                    app.UseMiddleware<InertiaMiddleware>();
                    app.Run(ctx =>
                    {
                        ctx.Response.StatusCode = 204;
                        return Task.CompletedTask;
                    });
                }))
            .StartAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/dashboard");
        request.Headers.Add("X-Inertia", "true");
        request.Headers.Add("Referer", "//evil.example/phish");

        var response = await host.GetTestClient().SendAsync(request);

        Assert.That(response.Headers.Contains("Location"), Is.True);
        Assert.That(response.Headers.GetValues("Location").First(), Is.EqualTo("/dashboard"));
    }

    [Test]
    [Description("A same-host Referer using a different scheme is rejected.")]
    public async Task Middleware_RejectsCrossSchemeReferer()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder => webBuilder
                .UseTestServer()
                .ConfigureServices(services => services.AddInertiaVersion("test-123"))
                .Configure(app =>
                {
                    app.UseMiddleware<InertiaMiddleware>();
                    app.Run(ctx =>
                    {
                        ctx.Response.StatusCode = 204;
                        return Task.CompletedTask;
                    });
                }))
            .StartAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/dashboard");
        request.Headers.Add("X-Inertia", "true");
        request.Headers.Add("Referer", "https://localhost/previous");

        var response = await host.GetTestClient().SendAsync(request);

        Assert.That(response.Headers.Contains("Location"), Is.True);
        Assert.That(response.Headers.GetValues("Location").First(), Is.EqualTo("/dashboard"));
    }

    [Test]
    [Description("A same-host Referer using a different port is rejected.")]
    public async Task Middleware_RejectsCrossPortReferer()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder => webBuilder
                .UseTestServer()
                .ConfigureServices(services => services.AddInertiaVersion("test-123"))
                .Configure(app =>
                {
                    app.UseMiddleware<InertiaMiddleware>();
                    app.Run(ctx =>
                    {
                        ctx.Response.StatusCode = 204;
                        return Task.CompletedTask;
                    });
                }))
            .StartAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/dashboard");
        request.Headers.Add("X-Inertia", "true");
        request.Headers.Add("Referer", "http://localhost:9999/previous");

        var response = await host.GetTestClient().SendAsync(request);

        Assert.That(response.Headers.Contains("Location"), Is.True);
        Assert.That(response.Headers.GetValues("Location").First(), Is.EqualTo("/dashboard"));
    }

    [Test]
    [Description("A same-host absolute Referer is honoured as the redirect target.")]
    public async Task Middleware_AcceptsSameHostReferer()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder => webBuilder
                .UseTestServer()
                .ConfigureServices(services => services.AddInertiaVersion("test-123"))
                .Configure(app =>
                {
                    app.UseMiddleware<InertiaMiddleware>();
                    app.Run(ctx =>
                    {
                        ctx.Response.StatusCode = 204;
                        return Task.CompletedTask;
                    });
                }))
            .StartAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/dashboard");
        request.Headers.Add("X-Inertia", "true");
        request.Headers.Add("Referer", "http://localhost/previous?tab=1");

        var response = await host.GetTestClient().SendAsync(request);

        Assert.That(response.Headers.Contains("Location"), Is.True);
        Assert.That(response.Headers.GetValues("Location").First(), Is.EqualTo("/previous?tab=1"));
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
