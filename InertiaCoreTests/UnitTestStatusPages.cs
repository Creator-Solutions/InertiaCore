using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InertiaCore.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace InertiaCoreTests;

/// <summary>
/// Integration tests for Inertia error pages (IN-65): an Inertia request that resolves to a
/// handled status code is rendered through the configured error component, while a
/// non-Inertia request is left on the host's default status handling.
/// </summary>
public abstract class UnitTestStatusPagesBase : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("UseStatusPages", "true");
        builder.UseSetting("UseAuth", "true");
    }

    private static HttpRequestMessage Inertia(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Inertia", "true");
        return request;
    }

    private static JsonElement Props(JsonElement json) => json.GetProperty("props");

    [Test]
    [Description("SC: an Inertia 404 renders the Error component with { status, message } and keeps the 404 status.")]
    public async Task Inertia404_RendersErrorComponent()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.SendAsync(Inertia("/status/404"));
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var props = Props(json);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(json.GetProperty("component").GetString(), Is.EqualTo("Error"));
            Assert.That(props.GetProperty("status").GetInt32(), Is.EqualTo(404));
            Assert.That(props.GetProperty("message").GetString(), Is.EqualTo("Not Found"));
            Assert.That(response.Headers.Contains("X-Inertia"), Is.True);
            Assert.That(response.Headers.GetValues("X-Inertia").Single(), Is.EqualTo("true"));
        });
    }

    [Test]
    [Description("Each handled status renders through Inertia with its own status and message.")]
    public async Task HandledStatuses_RenderThroughInertia()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var cases = new (string Path, int Status, string Message)[]
        {
            ("/status/403", 403, "Forbidden"),
            ("/status/419", 419, "Page Expired"),
            ("/status/503", 503, "Service Unavailable"),
        };

        foreach (var (path, status, message) in cases)
        {
            var response = await client.SendAsync(Inertia(path));
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            var props = Props(json);

            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo((HttpStatusCode)status), path);
                Assert.That(json.GetProperty("component").GetString(), Is.EqualTo("Error"), path);
                Assert.That(props.GetProperty("status").GetInt32(), Is.EqualTo(status), path);
                Assert.That(props.GetProperty("message").GetString(), Is.EqualTo(message), path);
            });
        }
    }

    [Test]
    [Description("A non-Inertia request to a handled status is not rewritten and carries no body.")]
    public async Task NonInertia404_IsUntouched()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/status/404");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(response.Headers.Contains("X-Inertia"), Is.False);
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.Not.EqualTo("application/json"));
        });
    }

    [Test]
    [Description("A normal Inertia page render is unaffected by the status-page middleware.")]
    public async Task NormalPage_Unaffected()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.SendAsync(Inertia("/status/page"));
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(json.GetProperty("component").GetString(), Is.EqualTo("Test/Page"));
        });
    }

    [Test]
    [Description("An unauthenticated Inertia request to an [Authorize] endpoint becomes 409 + X-Inertia-Location.")]
    public async Task Challenge_Inertia_Returns409Location()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.SendAsync(Inertia("/auth/secure"));

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(response.Headers.GetValues("X-Inertia-Location").Single(), Does.Contain("/auth/login"));
            Assert.That(response.Headers.Contains("Location"), Is.False);
            Assert.That(response.Headers.Contains("X-Inertia"), Is.False, "a control 409 carries no X-Inertia header");
        });
    }

    [Test]
    [Description("An unauthenticated non-Inertia request keeps the ordinary 302 redirect to login.")]
    public async Task Challenge_NonInertia_KeepsRedirect()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/auth/secure");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Found));
            Assert.That(response.Headers.Location!.ToString(), Does.Contain("/auth/login"));
            Assert.That(response.Headers.Contains("X-Inertia-Location"), Is.False);
        });
    }

    [Test]
    [Description("A forbidden Inertia request renders the 403 error component.")]
    public async Task Forbid_Inertia_Renders403Page()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/auth/signin"));

        var response = await client.SendAsync(Inertia("/auth/forbid"));
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var props = Props(json);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(json.GetProperty("component").GetString(), Is.EqualTo("Error"));
            Assert.That(props.GetProperty("status").GetInt32(), Is.EqualTo(403));
        });
    }
}

/// <summary>Runs the status-page integration tests against the default host.</summary>
[TestFixture]
public class UnitTestStatusPages : UnitTestStatusPagesBase
{
}

/// <summary>
/// In Production the developer exception page does not run, so a 500 is rendered through
/// the Inertia error component, and custom components/messages are honoured.
/// </summary>
[TestFixture]
public class UnitTestStatusPagesProduction : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("UseStatusPages", "true");
        builder.ConfigureServices(services =>
            services.Configure<InertiaOptions>(options =>
            {
                options.StatusPages.Components[503] = "Maintenance";
                options.StatusPages.Messages[503] = "Down for maintenance";
            }));
    }

    [Test]
    [Description("In Production an Inertia 500 is rendered through the Error component.")]
    public async Task Production_Inertia500_RendersErrorComponent()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var request = new HttpRequestMessage(HttpMethod.Get, "/status/500");
        request.Headers.Add("X-Inertia", "true");

        var response = await client.SendAsync(request);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
            Assert.That(json.GetProperty("component").GetString(), Is.EqualTo("Error"));
            Assert.That(json.GetProperty("props").GetProperty("message").GetString(), Is.EqualTo("Server Error"));
        });
    }

    [Test]
    [Description("A per-status component and message override are used.")]
    public async Task Production_CustomComponentAndMessage()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var request = new HttpRequestMessage(HttpMethod.Get, "/status/503");
        request.Headers.Add("X-Inertia", "true");

        var response = await client.SendAsync(request);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
            Assert.That(json.GetProperty("component").GetString(), Is.EqualTo("Maintenance"));
            Assert.That(json.GetProperty("props").GetProperty("message").GetString(), Is.EqualTo("Down for maintenance"));
        });
    }
}

/// <summary>A message factory takes precedence over the per-status overrides.</summary>
[TestFixture]
public class UnitTestStatusPagesMessageFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("UseStatusPages", "true");
        builder.ConfigureServices(services =>
            services.Configure<InertiaOptions>(options =>
                options.StatusPages.MessageFactory = (_, status) =>
                    status == 500 ? "Something broke" : InertiaStatusPagesOptions.DefaultMessage(status)));
    }

    [Test]
    [Description("The message factory resolves the message shown for the status.")]
    public async Task MessageFactory_IsUsed()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var request = new HttpRequestMessage(HttpMethod.Get, "/status/500");
        request.Headers.Add("X-Inertia", "true");

        var response = await client.SendAsync(request);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.That(json.GetProperty("props").GetProperty("message").GetString(), Is.EqualTo("Something broke"));
    }
}

/// <summary>Status pages are a no-op when they are not enabled.</summary>
[TestFixture]
public class UnitTestStatusPagesDisabled : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("UseAuth", "true");

    [Test]
    [Description("With status pages disabled, an Inertia 404 keeps the raw status and no error page.")]
    public async Task Disabled_DoesNotRender()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var request = new HttpRequestMessage(HttpMethod.Get, "/status/404");
        request.Headers.Add("X-Inertia", "true");

        var response = await client.SendAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.Not.EqualTo("application/json"));
        });
    }

    [Test]
    [Description("With status pages disabled, an unauthenticated Inertia request keeps the ordinary 302.")]
    public async Task Disabled_DoesNotConvertChallenge()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var request = new HttpRequestMessage(HttpMethod.Get, "/auth/secure");
        request.Headers.Add("X-Inertia", "true");

        var response = await client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Found));
    }
}
