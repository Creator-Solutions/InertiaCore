using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InertiaCore.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace InertiaCoreTests;

/// <summary>
/// Integration tests for the cross-request flash system (IN-63): a flashed value survives the
/// redirect, is exposed once under the <c>flash</c> prop, then disappears.
/// </summary>
[TestFixture]
public class UnitTestFlash : WebApplicationFactory<Program>
{
    private static HttpRequestMessage Post(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("X-Inertia", "true");
        return request;
    }

    private static HttpRequestMessage Get(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Inertia", "true");
        return request;
    }

    private static JsonElement Props(JsonElement json) => json.GetProperty("props");

    [Test]
    [Description("SC: static Inertia.Flash survives the redirect and is exposed once under the flash prop.")]
    public async Task StaticFlash_IsShownOnce()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var post = await client.SendAsync(Post("/flash"));

        Assert.Multiple(() =>
        {
            Assert.That(post.StatusCode, Is.EqualTo(HttpStatusCode.SeeOther));
            Assert.That(post.Headers.Location?.ToString(), Is.EqualTo("/flash/page"));
        });

        var first = await client.SendAsync(Get("/flash/page"));
        var flash = Props(await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("flash");

        Assert.That(flash.GetProperty("success").GetString(), Is.EqualTo("Saved"));

        var second = await client.SendAsync(Get("/flash/page"));
        var secondProps = Props(await second.Content.ReadFromJsonAsync<JsonElement>());

        Assert.That(secondProps.TryGetProperty("flash", out _), Is.False, "flash is consumed by the first render");
    }

    [Test]
    [Description("IInertia.Flash places the value in the same single flash prop.")]
    public async Task InertiaServiceFlash_IsExposed()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await client.SendAsync(Post("/flash-service"));
        var page = await client.SendAsync(Get("/flash/page"));
        var flash = Props(await page.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("flash");

        Assert.That(flash.GetProperty("notice").GetString(), Is.EqualTo("From IInertia"));
    }

    [Test]
    [Description("A payload above the cookie limit throws the documented exception on the default provider.")]
    public async Task OversizedFlash_OnCookieProvider_Throws()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var post = await client.SendAsync(Post("/flash-large"));
        Assert.That(post.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
    }
}

/// <summary>
/// The same flash flow with the opt-in distributed cache provider registered, which accepts
/// payloads larger than the cookie cap.
/// </summary>
[TestFixture]
public class UnitTestFlashDistributed : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("UseDistributedFlash", "true");

    [Test]
    [Description("SC: with the distributed cache provider an over-cap payload round-trips through the flash prop.")]
    public async Task LargeFlash_RoundTrips()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var post = new HttpRequestMessage(HttpMethod.Post, "/flash-large");
        post.Headers.Add("X-Inertia", "true");
        var response = await client.SendAsync(post);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.SeeOther));

        var page = new HttpRequestMessage(HttpMethod.Get, "/flash/page");
        page.Headers.Add("X-Inertia", "true");
        var rendered = await client.SendAsync(page);
        var json = await rendered.Content.ReadFromJsonAsync<JsonElement>();
        var payload = json.GetProperty("props").GetProperty("flash").GetProperty("payload").GetString();

        Assert.That(payload, Has.Length.EqualTo(8000));
    }
}
