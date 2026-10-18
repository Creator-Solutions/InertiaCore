using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace InertiaCoreTests;

[TestFixture]
public class UnitTestValidationMinimalApi : WebApplicationFactory<Program>
{
    [SetUp]
    public void ResetProbe() => ValidationProbe.HandlerRuns = 0;

    private static HttpRequestMessage Post(string path, object body, bool inertia, string? errorBag = null, string? referer = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body)
        };

        if (inertia)
            request.Headers.Add("X-Inertia", "true");

        if (errorBag != null)
            request.Headers.Add("X-Inertia-Error-Bag", errorBag);

        if (referer != null)
            request.Headers.Add("Referer", referer);

        return request;
    }

    private static HttpRequestMessage Get(string path, string? errorBag = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Inertia", "true");

        if (errorBag != null)
            request.Headers.Add("X-Inertia-Error-Bag", errorBag);

        return request;
    }

    private static object InvalidInput() => new
    {
        name = "",
        address = new { street = "" },
        items = new[] { new { name = "", quantity = 0 } }
    };

    private static object ValidInput() => new
    {
        name = "Order",
        address = new { street = "1 Main St" },
        items = new[] { new { name = "Widget", quantity = 2 } }
    };

    [Test]
    [Description("SC: an invalid Inertia POST is redirected back with 303 and the handler does not run.")]
    public async Task InvalidInertia_Returns303_AndSkipsHandler()
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
    [Description("SC: errors flashed by the 303 are restored into the default bag on the next Inertia render.")]
    public async Task InvalidInertia_ErrorsSurviveRedirect_InDefaultBag()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await client.SendAsync(Post("/validation", InvalidInput(), inertia: true, referer: "/form"));
        var page = await client.SendAsync(Get("/validation/page"));
        var errors = (await page.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("props").GetProperty("errors");

        Assert.Multiple(() =>
        {
            Assert.That(errors.GetProperty("name")[0].GetString(), Is.Not.Empty);
            Assert.That(errors.GetProperty("address.street").GetArrayLength(), Is.EqualTo(1));
            Assert.That(errors.GetProperty("items.0.name").GetArrayLength(), Is.EqualTo(1));
            Assert.That(errors.TryGetProperty("inertiaValidationErrors", out _), Is.False);
        });
    }

    [Test]
    [Description("SC: with X-Inertia-Error-Bag set, errors are written to the named bag and rendered nested under it.")]
    public async Task InvalidInertia_NamedBag_RendersNestedUnderBag()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var submit = await client.SendAsync(Post("/validation", InvalidInput(), inertia: true, errorBag: "settings", referer: "/form"));
        var page = await client.SendAsync(Get("/validation/page"));
        var errors = (await page.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("props").GetProperty("errors");

        Assert.Multiple(() =>
        {
            Assert.That(submit.StatusCode, Is.EqualTo(HttpStatusCode.SeeOther));
            Assert.That(errors.GetProperty("settings").GetProperty("name").GetArrayLength(), Is.EqualTo(1));
            Assert.That(errors.GetProperty("settings").GetProperty("address.street").GetArrayLength(), Is.EqualTo(1));
        });
    }

    [Test]
    [Description("SC: a non-Inertia invalid request returns 422 with camelCase, dotted and indexed keys.")]
    public async Task NonInertiaInvalid_Returns422_WithDocumentedKeys()
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
            Assert.That(errors.TryGetProperty("items.0.quantity", out _), Is.True);
            Assert.That(ValidationProbe.HandlerRuns, Is.EqualTo(0));
        });
    }

    [Test]
    [Description("SC: a valid request reaches the handler and receives its response untouched.")]
    public async Task ValidRequest_RunsHandler()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.SendAsync(Post("/validation", ValidInput(), inertia: true, referer: "/form"));
        var text = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(text, Is.EqualTo("ok"));
            Assert.That(ValidationProbe.HandlerRuns, Is.EqualTo(1));
        });
    }

    [Test]
    [Description("SC: an endpoint in a route group opted in with WithInertiaValidation validates its endpoints.")]
    public async Task RouteGroup_OptIn_ValidatesEndpoints()
    {
        using var client = CreateClient();

        var response = await client.SendAsync(Post("/validation-group/submit", InvalidInput(), inertia: false));

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
            Assert.That(ValidationProbe.HandlerRuns, Is.EqualTo(0));
        });
    }

    [Test]
    [Description("SC: an endpoint without validation attributes is a no-op and the handler runs.")]
    public async Task NoValidationAttributes_IsNoOp()
    {
        using var client = CreateClient();

        var response = await client.SendAsync(Post("/validation-plain", new { name = "" }, inertia: false));

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(ValidationProbe.HandlerRuns, Is.EqualTo(1));
        });
    }
}
