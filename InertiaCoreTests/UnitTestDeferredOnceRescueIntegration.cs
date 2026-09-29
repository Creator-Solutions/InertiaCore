using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace InertiaCoreTests;

[TestFixture]
public class UnitTestDeferredOnceRescueIntegration : WebApplicationFactory<Program>
{
    private static async Task<(HttpStatusCode Status, JsonElement Root)> GetAsync(
        HttpClient client, string path, params (string Name, string Value)[] headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Inertia", "true");
        foreach (var (name, value) in headers)
            request.Headers.Add(name, value);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(body);
        return (response.StatusCode, document.RootElement.Clone());
    }

    [Test]
    [Description("A full visit over HTTP emits grouped deferredProps and omits the deferred values.")]
    public async Task DeferredVisit_EmitsGroupedMetadata_OverHttp()
    {
        using var client = CreateClient();
        var (status, root) = await GetAsync(client, "/deferred");

        var deferred = root.GetProperty("deferredProps");
        var props = root.GetProperty("props");

        Assert.Multiple(() =>
        {
            Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(
                deferred.GetProperty("default").EnumerateArray().Select(e => e.GetString()),
                Is.EqualTo(new[] { "permissions" }));
            Assert.That(
                deferred.GetProperty("attributes").EnumerateArray().Select(e => e.GetString()),
                Is.EqualTo(new[] { "teams", "projects" }));
            Assert.That(props.TryGetProperty("permissions", out _), Is.False);
            Assert.That(props.TryGetProperty("teams", out _), Is.False);
        });
    }

    [Test]
    [Description("A partial reload for a group over HTTP returns only that group's props.")]
    public async Task DeferredGroupPartial_OverHttp()
    {
        using var client = CreateClient();
        var (_, root) = await GetAsync(
            client,
            "/deferred",
            ("X-Inertia-Partial-Data", "teams,projects"),
            ("X-Inertia-Partial-Component", "Test/Page"));

        var props = root.GetProperty("props");

        Assert.Multiple(() =>
        {
            Assert.That(root.TryGetProperty("deferredProps", out _), Is.False);
            Assert.That(props.GetProperty("teams").GetString(), Is.EqualTo("teams"));
            Assert.That(props.GetProperty("projects").GetString(), Is.EqualTo("projects"));
            Assert.That(props.TryGetProperty("permissions", out _), Is.False);
        });
    }

    [Test]
    [Description("A once prop over HTTP emits onceProps and resolves on the first visit.")]
    public async Task OnceVisit_EmitsMetadata_OverHttp()
    {
        using var client = CreateClient();
        var (status, root) = await GetAsync(client, "/once");

        Assert.Multiple(() =>
        {
            Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(root.GetProperty("onceProps").GetProperty("plans").GetProperty("prop").GetString(), Is.EqualTo("plans"));
            Assert.That(root.GetProperty("props").TryGetProperty("plans", out _), Is.True);
        });
    }

    [Test]
    [Description("A once prop already held by the client is skipped over HTTP but still announced.")]
    public async Task OnceVisit_SkippedWhenClientHoldsIt_OverHttp()
    {
        using var client = CreateClient();
        var (_, root) = await GetAsync(client, "/once", ("X-Inertia-Except-Once-Props", "plans"));

        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("props").TryGetProperty("plans", out _), Is.False);
            Assert.That(root.GetProperty("onceProps").TryGetProperty("plans", out _), Is.True);
        });
    }

    [Test]
    [Description("A rescued deferred prop over HTTP returns 200, omits the value and lists rescuedProps.")]
    public async Task RescueVisit_OverHttp()
    {
        using var client = CreateClient();
        var (status, root) = await GetAsync(
            client,
            "/rescue",
            ("X-Inertia-Partial-Data", "permissions,other"),
            ("X-Inertia-Partial-Component", "Test/Page"));

        Assert.Multiple(() =>
        {
            Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(
                root.GetProperty("rescuedProps").EnumerateArray().Select(e => e.GetString()),
                Is.EqualTo(new[] { "permissions" }));
            Assert.That(root.GetProperty("props").TryGetProperty("permissions", out _), Is.False);
            Assert.That(root.GetProperty("props").GetProperty("other").GetString(), Is.EqualTo("ok"));
        });
    }
}
