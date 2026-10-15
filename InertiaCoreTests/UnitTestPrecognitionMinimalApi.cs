using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace InertiaCoreTests;

[TestFixture]
public class UnitTestPrecognitionMinimalApi : WebApplicationFactory<Program>
{
    [SetUp]
    public void ResetProbe() => PrecognitionProbe.HandlerRuns = 0;

    private static HttpRequestMessage Post(object body, bool precognition, string? validateOnly = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/precognition")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("X-Inertia", "true");

        if (precognition)
            request.Headers.Add("Precognition", "true");

        if (validateOnly != null)
            request.Headers.Add("Precognition-Validate-Only", validateOnly);

        return request;
    }

    [Test]
    [Description("SC: a valid precognitive request returns 204 + Precognition-Success and skips the handler.")]
    public async Task Valid_Returns204_AndSkipsHandler()
    {
        using var client = CreateClient();

        var response = await client.SendAsync(Post(new { name = "Jane", age = 30, email = "jane@example.com" }, precognition: true));

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(response.Headers.GetValues("Precognition-Success").Single(), Is.EqualTo("true"));
            Assert.That(PrecognitionProbe.HandlerRuns, Is.EqualTo(0), "the handler must not run for a precognitive request");
        });
    }

    [Test]
    [Description("SC: an invalid precognitive request returns 422 with the errors and skips the handler.")]
    public async Task Invalid_Returns422_AndSkipsHandler()
    {
        using var client = CreateClient();

        var response = await client.SendAsync(Post(new { name = "", age = 5 }, precognition: true));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
            Assert.That(PrecognitionProbe.HandlerRuns, Is.EqualTo(0));
            Assert.That(body.GetProperty("errors").TryGetProperty("name", out _), Is.True);
        });
    }

    [Test]
    [Description("SC: Validate-Only restricts the reported errors to the requested fields.")]
    public async Task ValidateOnly_ReturnsOnlyRequestedField()
    {
        using var client = CreateClient();

        var response = await client.SendAsync(
            Post(new { name = "", age = 5, email = "not-an-email" }, precognition: true, validateOnly: "name"));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errors = body.GetProperty("errors");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
            Assert.That(errors.TryGetProperty("name", out _), Is.True);
            Assert.That(errors.TryGetProperty("email", out _), Is.False);
            Assert.That(errors.TryGetProperty("age", out _), Is.False);
        });
    }

    [Test]
    [Description("SC: Validate-Only for a valid field succeeds even when other fields are invalid.")]
    public async Task ValidateOnly_ValidField_Succeeds()
    {
        using var client = CreateClient();

        var response = await client.SendAsync(
            Post(new { name = "Jane", age = 30, email = "not-an-email" }, precognition: true, validateOnly: "name"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    [Description("SC: a non-precognitive request runs the handler exactly as before.")]
    public async Task NonPrecognition_RunsHandler()
    {
        using var client = CreateClient();

        var response = await client.SendAsync(Post(new { name = "", age = 5 }, precognition: false));

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(PrecognitionProbe.HandlerRuns, Is.EqualTo(1));
        });
    }
}
