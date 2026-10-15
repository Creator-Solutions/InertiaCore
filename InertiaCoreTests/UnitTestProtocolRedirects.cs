using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace InertiaCoreTests;

[TestFixture]
public class UnitTestProtocolRedirects : WebApplicationFactory<Program>
{
    private static HttpRequestMessage Get(string path, bool inertia = true, bool prefetch = false)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (inertia)
            request.Headers.Add("X-Inertia", "true");
        if (prefetch)
            request.Headers.Add("Purpose", "prefetch");

        return request;
    }

    [Test]
    [Description("A non-prefetch fragment redirect becomes a 409 with X-Inertia-Redirect and no Location/X-Inertia.")]
    public async Task FragmentRedirect_NonPrefetch_Returns409()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.SendAsync(Get("/protocol/redirect-fragment"));

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(response.Headers.GetValues("X-Inertia-Redirect").Single(), Is.EqualTo("/protocol/target#section"));
            Assert.That(response.Headers.Contains("Location"), Is.False);
            Assert.That(response.Headers.Contains("X-Inertia"), Is.False, "a control 409 carries no X-Inertia header");
        });
    }

    [Test]
    [Description("A prefetch fragment redirect passes through as an ordinary redirect.")]
    public async Task FragmentRedirect_Prefetch_PassesThrough()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.SendAsync(Get("/protocol/redirect-fragment", prefetch: true));

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Found));
            Assert.That(response.Headers.Location!.ToString(), Is.EqualTo("/protocol/target#section"));
            Assert.That(response.Headers.Contains("X-Inertia-Redirect"), Is.False);
        });
    }

    [Test]
    [Description("preserveFragment set on a redirect is emitted on the following page object.")]
    public async Task PreserveFragment_EmittedOnTargetPage()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var first = await client.SendAsync(Get("/protocol/redirect-preserve"));
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Found));

        var second = await client.SendAsync(Get("/protocol/target"));
        var body = await second.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Multiple(() =>
        {
            Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body.TryGetProperty("preserveFragment", out var flag), Is.True);
            Assert.That(flag.GetBoolean(), Is.True);
        });
    }
}
