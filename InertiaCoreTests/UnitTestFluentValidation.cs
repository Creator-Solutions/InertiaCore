using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace InertiaCoreTests;

/// <summary>
/// Integration tests for the FluentValidation adapter (IN-62): the Minimal API endpoint filter
/// and the MVC attribute both route failures through the Inertia error bags.
/// </summary>
[TestFixture]
public class UnitTestFluentValidation : WebApplicationFactory<Program>
{
    [SetUp]
    public void ResetProbe() => FluentProbe.HandlerRuns = 0;

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

    private static object InvalidOrder() => new
    {
        customerName = "",
        address = new { postalCode = "" }
    };

    private static object ValidOrder() => new
    {
        customerName = "Ada",
        address = new { postalCode = "SW1A" },
        notes = "leave at door"
    };

    [Test]
    [Description("SC: an invalid Inertia POST is redirected back with 303 and the validator short-circuits the handler.")]
    public async Task InvalidInertia_Returns303_AndSkipsHandler()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.SendAsync(Post("/fluent", InvalidOrder(), inertia: true, referer: "/form"));

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.SeeOther));
            Assert.That(response.Headers.Location?.ToString(), Is.EqualTo("/form"));
            Assert.That(FluentProbe.HandlerRuns, Is.EqualTo(0));
        });
    }

    [Test]
    [Description("SC: FluentValidation failures use the documented key format, including nested properties.")]
    public async Task InvalidInertia_ErrorsUseDocumentedKeys()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await client.SendAsync(Post("/fluent", InvalidOrder(), inertia: true, referer: "/form"));
        var page = await client.SendAsync(Get("/validation/page"));
        var errors = (await page.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("props").GetProperty("errors");

        Assert.Multiple(() =>
        {
            Assert.That(errors.GetProperty("customerName").GetArrayLength(), Is.EqualTo(1));
            Assert.That(errors.GetProperty("address.postalCode").GetArrayLength(), Is.EqualTo(1));
            Assert.That(errors.TryGetProperty("inertiaValidationErrors", out _), Is.False);
        });
    }

    [Test]
    [Description("SC: a class-level failure (a null nested object) maps to the empty key.")]
    public async Task InvalidInertia_ClassLevelFailure_UsesEmptyKey()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await client.SendAsync(Post("/fluent", new { customerName = "Ada" }, inertia: true, referer: "/form"));
        var page = await client.SendAsync(Get("/validation/page"));
        var errors = (await page.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("props").GetProperty("errors");

        Assert.That(errors.GetProperty("address").GetArrayLength(), Is.EqualTo(1));
    }

    [Test]
    [Description("SC: errors are written to the bag named by X-Inertia-Error-Bag.")]
    public async Task InvalidInertia_NamedBag_RendersNestedUnderBag()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await client.SendAsync(Post("/fluent", InvalidOrder(), inertia: true, errorBag: "settings", referer: "/form"));
        var page = await client.SendAsync(Get("/validation/page"));
        var errors = (await page.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("props").GetProperty("errors");

        Assert.Multiple(() =>
        {
            Assert.That(errors.GetProperty("settings").GetProperty("customerName").GetArrayLength(), Is.EqualTo(1));
            Assert.That(errors.GetProperty("settings").GetProperty("address.postalCode").GetArrayLength(), Is.EqualTo(1));
        });
    }

    [Test]
    [Description("SC: a non-Inertia invalid request returns 422 with the documented keys.")]
    public async Task NonInertiaInvalid_Returns422_WithDocumentedKeys()
    {
        using var client = CreateClient();

        var response = await client.SendAsync(Post("/fluent", InvalidOrder(), inertia: false));
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
            Assert.That(errors.TryGetProperty("customerName", out _), Is.True);
            Assert.That(errors.TryGetProperty("address.postalCode", out _), Is.True);
            Assert.That(FluentProbe.HandlerRuns, Is.EqualTo(0));
        });
    }

    [Test]
    [Description("SC: a valid request reaches the handler and receives its response untouched.")]
    public async Task ValidRequest_RunsHandler()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.SendAsync(Post("/fluent", ValidOrder(), inertia: true, referer: "/form"));

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content.ReadAsStringAsync().Result, Is.EqualTo("ok"));
            Assert.That(FluentProbe.HandlerRuns, Is.EqualTo(1));
        });
    }

    [Test]
    [Description("SC: every endpoint in a route group opted in with WithInertiaFluentValidation validates.")]
    public async Task RouteGroup_OptIn_ValidatesEndpoints()
    {
        using var client = CreateClient();

        var response = await client.SendAsync(Post("/fluent-group/submit", InvalidOrder(), inertia: false));

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
            Assert.That(FluentProbe.HandlerRuns, Is.EqualTo(0));
        });
    }

    [Test]
    [Description("SC: extra rule sets run only when requested, so the default endpoint skips them.")]
    public async Task DefaultEndpoint_DoesNotRunExtraRuleSets()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.SendAsync(Post("/fluent",
            new { customerName = "Ada", address = new { postalCode = "SW1A" } },
            inertia: true, referer: "/form"));

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(FluentProbe.HandlerRuns, Is.EqualTo(1));
        });
    }

    [Test]
    [Description("SC: the strict rule set makes Notes required and its failure is reported.")]
    public async Task StrictRuleSet_ReportsNotesFailure()
    {
        using var client = CreateClient();

        var response = await client.SendAsync(Post("/fluent-strict",
            new { customerName = "Ada", address = new { postalCode = "SW1A" } },
            inertia: false));
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
            Assert.That(errors.TryGetProperty("notes", out _), Is.True);
            Assert.That(FluentProbe.HandlerRuns, Is.EqualTo(0));
        });
    }

    [Test]
    [Description("SC: a precognitive invalid request gets the 422 Precognition response, not a redirect.")]
    public async Task PrecognitiveInvalid_Returns422_NotRedirect()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var request = Post("/fluent-precognition", InvalidOrder(), inertia: true, referer: "/form");
        request.Headers.Add("Precognition", "true");

        var response = await client.SendAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
            Assert.That(response.Headers.Location, Is.Null);
            Assert.That(response.Headers.GetValues("Precognition").Single(), Is.EqualTo("true"));
            Assert.That(FluentProbe.HandlerRuns, Is.EqualTo(0));
        });
    }

    [Test]
    [Description("SC: Precognition-Validate-Only limits which failures are reported.")]
    public async Task PrecognitiveValidateOnly_FiltersErrors()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var request = Post("/fluent-precognition", InvalidOrder(), inertia: true, referer: "/form");
        request.Headers.Add("Precognition", "true");
        request.Headers.Add("Precognition-Validate-Only", "customerName");

        var response = await client.SendAsync(request);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
            Assert.That(errors.TryGetProperty("customerName", out _), Is.True);
            Assert.That(errors.TryGetProperty("address.postalCode", out _), Is.False);
        });
    }

    [Test]
    [Description("SC: a valid precognitive request gets a 204 with the Precognition-Success header.")]
    public async Task PrecognitiveValid_Returns204()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var request = Post("/fluent-precognition", ValidOrder(), inertia: true, referer: "/form");
        request.Headers.Add("Precognition", "true");

        var response = await client.SendAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(response.Headers.GetValues("Precognition-Success").Single(), Is.EqualTo("true"));
            Assert.That(FluentProbe.HandlerRuns, Is.EqualTo(0));
        });
    }

    [Test]
    [Description("SC: the MVC attribute validates the action argument and reports failures in the bag.")]
    public async Task MvcAttribute_InvalidInertia_Returns303()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await client.SendAsync(Post("/fluent-mvc", InvalidOrder(), inertia: true, referer: "/form"));
        var page = await client.SendAsync(Get("/validation/page"));
        var errors = (await page.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("props").GetProperty("errors");

        Assert.Multiple(() =>
        {
            Assert.That(errors.GetProperty("customerName").GetArrayLength(), Is.EqualTo(1));
            Assert.That(errors.GetProperty("address.postalCode").GetArrayLength(), Is.EqualTo(1));
        });
    }

    [Test]
    [Description("SC: the MVC attribute honours rule sets, so the strict action requires Notes.")]
    public async Task MvcAttribute_StrictRuleSet_ReportsNotesFailure()
    {
        using var client = CreateClient();

        var response = await client.SendAsync(Post("/fluent-mvc-strict",
            new { customerName = "Ada", address = new { postalCode = "SW1A" } },
            inertia: false));
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
            Assert.That(errors.TryGetProperty("notes", out _), Is.True);
        });
    }

    [Test]
    [Description("SC: the MVC attribute lets a valid request reach the action.")]
    public async Task MvcAttribute_ValidRequest_ReachesAction()
    {
        using var client = CreateClient();

        var response = await client.SendAsync(Post("/fluent-mvc", ValidOrder(), inertia: false));

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(FluentProbe.HandlerRuns, Is.EqualTo(1));
        });
    }
}

/// <summary>
/// The missing-validator case: building the endpoint must fail with a clear message.
/// </summary>
[TestFixture]
public class UnitTestFluentValidationMissing : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("MissingFluentValidator", "true");

    [Test]
    [Description("SC: an endpoint without a registered validator throws a message naming the type and how to register it.")]
    public void MissingValidator_ThrowsClearError()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => CreateClient());

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain("FluentUnregistered"));
            Assert.That(ex.Message, Does.Contain("IValidator<FluentUnregistered>"));
        });
    }
}
