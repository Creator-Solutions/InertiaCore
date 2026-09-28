using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace InertiaCoreTests;

[TestFixture]
public class UnitTestMergePropsIntegration : WebApplicationFactory<Program>
{
    [Test]
    [Description("A full Inertia visit over HTTP emits mergeProps for the merge prop.")]
    public async Task FullVisit_EmitsMergeMetadata_OverHttp()
    {
        using var client = CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/merge");
        request.Headers.Add("X-Inertia", "true");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(
                root.GetProperty("mergeProps").EnumerateArray().Select(e => e.GetString()),
                Is.EqualTo(new[] { "posts" }));
        });
    }

    [Test]
    [Description("X-Inertia-Reset over HTTP removes the prop from mergeProps but keeps it in props.")]
    public async Task ResetHeader_RemovesMergeMetadata_OverHttp()
    {
        using var client = CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/merge");
        request.Headers.Add("X-Inertia", "true");
        request.Headers.Add("X-Inertia-Reset", "posts");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(root.TryGetProperty("mergeProps", out _), Is.False);
            Assert.That(root.GetProperty("props").TryGetProperty("posts", out _), Is.True);
        });
    }
}
