#if NET10_0_OR_GREATER
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace InertiaCoreTests;

/// <summary>
/// Minimal API validation on .NET 10 with <c>AddValidation()</c> active. The framework
/// validates, and the Inertia problem-details writer answers the failure.
/// </summary>
[TestFixture]
public class UnitTestValidationBuiltIn : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("UseBuiltInValidation", "true");

    [SetUp]
    public void ResetProbe() => ValidationProbe.HandlerRuns = 0;

    private static HttpRequestMessage Post(string path, object body, bool inertia, string? errorBag = null, string? referer = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };

        if (inertia)
            request.Headers.Add("X-Inertia", "true");

        if (errorBag != null)
            request.Headers.Add("X-Inertia-Error-Bag", errorBag);

        if (referer != null)
            request.Headers.Add("Referer", referer);

        return request;
    }

    private static HttpRequestMessage Get(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Inertia", "true");
        return request;
    }

    private static object InvalidInput() => new
    {
        name = "",
        address = new { street = "" },
        items = new[] { new { name = "", quantity = 0 } }
    };

    [Test]
    [Description("SC: on .NET 10 with AddValidation, an invalid Inertia POST is redirected 303 and the handler does not run.")]
    public async Task BuiltIn_InvalidInertia_Returns303_AndSkipsHandler()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.SendAsync(Post("/validation", InvalidInput(), inertia: true, referer: "/form"));

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.SeeOther));
            Assert.That(response.Headers.Location?.ToString(), Is.EqualTo("/form"));
            Assert.That(ValidationProbe.HandlerRuns, Is.EqualTo(0));
        });
    }

    [Test]
    [Description("SC: framework validation errors are restored into the bag with the documented key format.")]
    public async Task BuiltIn_InvalidInertia_ErrorsUseDocumentedKeys()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await client.SendAsync(Post("/validation", InvalidInput(), inertia: true, referer: "/form"));
        var page = await client.SendAsync(Get("/validation/page"));
        var errors = (await page.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("props").GetProperty("errors");

        Assert.Multiple(() =>
        {
            Assert.That(errors.TryGetProperty("name", out _), Is.True);
            Assert.That(errors.TryGetProperty("address.street", out _), Is.True);
            Assert.That(errors.TryGetProperty("items.0.name", out _), Is.True);
            Assert.That(errors.TryGetProperty("items.0.quantity", out _), Is.True);
        });
    }

    [Test]
    [Description("SC: on .NET 10 with AddValidation, a non-Inertia invalid request returns 422 with the documented keys.")]
    public async Task BuiltIn_NonInertiaInvalid_Returns422_WithDocumentedKeys()
    {
        using var client = CreateClient();

        var response = await client.SendAsync(Post("/validation", InvalidInput(), inertia: false));
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
            Assert.That(errors.TryGetProperty("name", out _), Is.True);
            Assert.That(errors.TryGetProperty("address.street", out _), Is.True);
            Assert.That(errors.TryGetProperty("items.0.name", out _), Is.True);
            Assert.That(ValidationProbe.HandlerRuns, Is.EqualTo(0));
        });
    }

    [Test]
    [Description("SC: on .NET 10 with AddValidation, a valid request reaches the handler.")]
    public async Task BuiltIn_ValidRequest_RunsHandler()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.SendAsync(Post("/validation",
            new { name = "Order", address = new { street = "1 Main" }, items = new[] { new { name = "Widget", quantity = 2 } } },
            inertia: true, referer: "/form"));

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(ValidationProbe.HandlerRuns, Is.EqualTo(1));
        });
    }

    [Test]
    [Description("SC: a route-group endpoint without DisableValidation keeps the built-in path.")]
    public async Task BuiltIn_RouteGroup_NonInertiaInvalid_Returns422()
    {
        using var client = CreateClient();

        var response = await client.SendAsync(Post("/validation-group/submit", InvalidInput(), inertia: false));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
    }
    [Test]
    [Description("SC: on .NET 10 with AddValidation, a precognitive invalid request gets the 422 Precognition response, not a redirect.")]
    public async Task BuiltIn_PrecognitiveInvalid_Returns422_NotRedirect()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var request = Post("/precognition-validation", InvalidInput(), inertia: true, referer: "/form");
        request.Headers.Add("Precognition", "true");

        var response = await client.SendAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
            Assert.That(response.Headers.Location, Is.Null);
            Assert.That(response.Headers.GetValues("Precognition").Single(), Is.EqualTo("true"));
            Assert.That(ValidationProbe.HandlerRuns, Is.EqualTo(0));
        });
    }
}
#endif
