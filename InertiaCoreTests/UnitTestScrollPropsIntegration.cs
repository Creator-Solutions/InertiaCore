using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace InertiaCoreTests;

[TestFixture]
public class UnitTestScrollPropsIntegration : WebApplicationFactory<Program>
{
    private static async Task<(HttpStatusCode Status, JsonElement Root)> GetAsync(
        HttpClient client, string? mergeIntent = null, string? reset = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/scroll");
        request.Headers.Add("X-Inertia", "true");
        if (mergeIntent != null)
            request.Headers.Add("X-Inertia-Infinite-Scroll-Merge-Intent", mergeIntent);
        if (reset != null)
            request.Headers.Add("X-Inertia-Reset", reset);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(body);
        return (response.StatusCode, document.RootElement.Clone());
    }

    [Test]
    [Description("A full scroll visit over HTTP emits the scrollProps cursor and append merge label.")]
    public async Task ScrollVisit_EmitsScrollProps_OverHttp()
    {
        using var client = CreateClient();
        var (status, root) = await GetAsync(client);

        var scroll = root.GetProperty("scrollProps").GetProperty("posts");

        Assert.Multiple(() =>
        {
            Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(scroll.GetProperty("pageName").GetString(), Is.EqualTo("page"));
            Assert.That(scroll.GetProperty("previousPage").GetInt32(), Is.EqualTo(1));
            Assert.That(scroll.GetProperty("nextPage").GetInt32(), Is.EqualTo(3));
            Assert.That(scroll.GetProperty("currentPage").GetInt32(), Is.EqualTo(2));
            Assert.That(scroll.GetProperty("reset").GetBoolean(), Is.False);
            Assert.That(
                root.GetProperty("mergeProps").EnumerateArray().Select(e => e.GetString()),
                Is.EqualTo(new[] { "posts.data" }));
        });
    }

    [Test]
    [Description("The prepend merge-intent header flips the scroll prop to prependProps over HTTP.")]
    public async Task ScrollVisit_PrependIntent_OverHttp()
    {
        using var client = CreateClient();
        var (_, root) = await GetAsync(client, mergeIntent: "prepend");

        Assert.Multiple(() =>
        {
            Assert.That(
                root.GetProperty("prependProps").EnumerateArray().Select(e => e.GetString()),
                Is.EqualTo(new[] { "posts.data" }));
            Assert.That(root.TryGetProperty("mergeProps", out _), Is.False);
        });
    }

    [Test]
    [Description("X-Inertia-Reset marks the scroll entry as reset and removes the merge label over HTTP.")]
    public async Task ScrollVisit_Reset_OverHttp()
    {
        using var client = CreateClient();
        var (_, root) = await GetAsync(client, reset: "posts");

        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("scrollProps").GetProperty("posts").GetProperty("reset").GetBoolean(), Is.True);
            Assert.That(root.TryGetProperty("mergeProps", out _), Is.False);
            Assert.That(root.GetProperty("props").TryGetProperty("posts", out _), Is.True);
        });
    }
}
