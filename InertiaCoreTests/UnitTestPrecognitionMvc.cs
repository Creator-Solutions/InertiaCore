using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InertiaCore.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace InertiaCoreTests;

/// <summary>
/// Precognition over HTTP for MVC controllers, including the interaction with
/// <c>[ApiController]</c>'s automatic 400-on-invalid-model behaviour.
/// </summary>
[TestFixture]
public class UnitTestPrecognitionMvc
{
    private static async Task<IHost> StartHostAsync()
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddControllersWithViews()
                            .AddApplicationPart(typeof(PrecognitionTestController).Assembly);
                        services.AddInertia();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseInertia();
                        app.UseEndpoints(endpoints => endpoints.MapControllers());
                    });
            })
            .StartAsync();

        PrecognitionTestController.ActionRuns = 0;
        return host;
    }

    private static HttpRequestMessage Post(object body, bool precognition)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mvc-precognition/create")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("X-Inertia", "true");

        if (precognition)
            request.Headers.Add("Precognition", "true");

        return request;
    }

    [Test]
    [Description("SC: valid MVC precognition returns 204 + Precognition-Success without running the action.")]
    public async Task Valid_Returns204_AndSkipsAction()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();

        var response = await client.SendAsync(Post(new { name = "Jane", age = 30 }, precognition: true));

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(response.Headers.GetValues("Precognition-Success").Single(), Is.EqualTo("true"));
            Assert.That(PrecognitionTestController.ActionRuns, Is.EqualTo(0));
        });
    }

    [Test]
    [Description("SC: invalid MVC precognition returns 422 (not the automatic 400) without running the action.")]
    public async Task Invalid_Returns422_NotAutomatic400()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();

        var response = await client.SendAsync(Post(new { name = "", age = 5 }, precognition: true));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
            Assert.That(body.GetProperty("errors").TryGetProperty("name", out _), Is.True);
            Assert.That(PrecognitionTestController.ActionRuns, Is.EqualTo(0));
        });
    }

    [Test]
    [Description("SC: a non-precognitive MVC request runs the action exactly as before.")]
    public async Task NonPrecognition_RunsAction()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();

        var response = await client.SendAsync(Post(new { name = "Jane", age = 30 }, precognition: false));

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(PrecognitionTestController.ActionRuns, Is.EqualTo(1));
        });
    }

    [Test]
    [Description("SC: the empty precognition 204 is not converted into an empty-response redirect.")]
    public async Task Valid_204_IsNotRedirected()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();

        var response = await client.SendAsync(Post(new { name = "Jane", age = 30 }, precognition: true));

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(response.Headers.Contains("Location"), Is.False);
        });
    }
}
