using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace InertiaCoreTests;

/// <summary>
/// Precognition and validation on one endpoint. The filter order must not change the result.
/// </summary>
[TestFixture]
public class UnitTestValidationPrecognitionComposition : WebApplicationFactory<Program>
{
    [SetUp]
    public void ResetProbe() => ValidationProbe.HandlerRuns = 0;

    [TestCase("/precognition-validation")]
    [TestCase("/precognition-validation-reversed")]
    [Description("SC: a precognitive invalid request gets the 422 Precognition response, in either filter order, and the handler does not run.")]
    public async Task PrecognitiveInvalid_Returns422_InAnyOrder(string path)
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(new { name = "", address = new { street = "" }, items = Array.Empty<object>() })
        };
        request.Headers.Add("X-Inertia", "true");
        request.Headers.Add("Precognition", "true");
        request.Headers.Add("Referer", "/form");

        var response = await client.SendAsync(request);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
            Assert.That(response.Headers.GetValues("Precognition").Single(), Is.EqualTo("true"));
            Assert.That(errors.TryGetProperty("name", out _), Is.True);
            Assert.That(ValidationProbe.HandlerRuns, Is.EqualTo(0));
        });
    }

    [TestCase("/precognition-validation")]
    [TestCase("/precognition-validation-reversed")]
    [Description("SC: a precognitive valid request gets 204 in either filter order and the handler does not run.")]
    public async Task PrecognitiveValid_Returns204_InAnyOrder(string path)
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(new { name = "Order", address = new { street = "1 Main" }, items = Array.Empty<object>() })
        };
        request.Headers.Add("X-Inertia", "true");
        request.Headers.Add("Precognition", "true");

        var response = await client.SendAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(ValidationProbe.HandlerRuns, Is.EqualTo(0));
        });
    }
}
