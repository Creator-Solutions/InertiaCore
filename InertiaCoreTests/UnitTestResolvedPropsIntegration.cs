using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace InertiaCoreTests;

/// <summary>
/// Integration tests for DI-resolved props (IN-67) over HTTP: a prop class with
/// constructor-injected services resolves per request, and the lazy/deferred/once
/// modifiers behave like their lambda equivalents.
/// </summary>
public abstract class UnitTestResolvedPropsIntegrationBase : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("UseResolvedProps", "true");

    private static HttpRequestMessage Inertia(string path, params (string Key, string Value)[] headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Inertia", "true");
        foreach (var (key, value) in headers)
            request.Headers.Add(key, value);
        return request;
    }

    private static async Task<JsonNode> JsonAsync(HttpClient client, string path,
        params (string Key, string Value)[] headers)
    {
        var response = await client.SendAsync(Inertia(path, headers));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return (await response.Content.ReadFromJsonAsync<JsonNode>())!;
    }

    [Test]
    [Description("SC: a prop class with constructor-injected services resolves per request over HTTP.")]
    public async Task ResolvedProp_ResolvesOverHttp()
    {
        using var client = CreateClient();

        var json = await JsonAsync(client, "/resolved/prop");
        var users = json["props"]!["users"]!;

        Assert.Multiple(() =>
        {
            Assert.That(users["scopeId"]!.GetValue<string>(), Is.Not.Null.And.Not.Empty);
            Assert.That(users["users"]!.AsArray(), Has.Count.EqualTo(2));
        });
    }

    [Test]
    [Description("SC: the resolved prop comes from the current request scope, not a shared one.")]
    public async Task ResolvedProp_IsPerRequest()
    {
        using var client = CreateClient();

        var first = (await JsonAsync(client, "/resolved/prop"))["props"]!["users"]!["scopeId"]!.GetValue<string>();
        var second = (await JsonAsync(client, "/resolved/prop"))["props"]!["users"]!["scopeId"]!.GetValue<string>();

        Assert.That(first, Is.Not.EqualTo(second), "each request resolves its own scope");
    }

    [Test]
    [Description("SC: Inertia.Lazy<T>() is excluded on the first load and resolved on a partial visit.")]
    public async Task ResolvedLazy_OverHttp()
    {
        using var client = CreateClient();

        var first = await JsonAsync(client, "/resolved/lazy");
        Assert.That(first["props"]!.AsObject().ContainsKey("users"), Is.False);

        var partial = await JsonAsync(client, "/resolved/lazy",
            ("X-Inertia-Partial-Component", "Test/Page"),
            ("X-Inertia-Partial-Data", "users"));

        Assert.That(partial["props"]!["users"]!["users"]!.AsArray(), Has.Count.EqualTo(2));
    }

    [Test]
    [Description("SC: Inertia.Defer<T>(group) is announced in deferredProps and resolved on request.")]
    public async Task ResolvedDefer_OverHttp()
    {
        using var client = CreateClient();

        var first = await JsonAsync(client, "/resolved/deferred");
        Assert.Multiple(() =>
        {
            Assert.That(first["props"]!.AsObject().ContainsKey("users"), Is.False);
            Assert.That(first["deferredProps"]!["users"]!.AsArray().Select(e => e!.GetValue<string>()),
                Does.Contain("users"));
        });

        var partial = await JsonAsync(client, "/resolved/deferred",
            ("X-Inertia-Partial-Component", "Test/Page"),
            ("X-Inertia-Partial-Data", "users"));

        Assert.That(partial["props"]!["users"]!["users"]!.AsArray(), Has.Count.EqualTo(2));
    }

    [Test]
    [Description("SC: Inertia.Once<T>() is announced in onceProps.")]
    public async Task ResolvedOnce_OverHttp()
    {
        using var client = CreateClient();

        var json = await JsonAsync(client, "/resolved/once");

        Assert.That(json["onceProps"]!.AsObject().ContainsKey("users"), Is.True);
    }

    [Test]
    [Description("SC: nested invokable props inside a DI-resolved prop keep working.")]
    public async Task ResolvedNested_OverHttp()
    {
        using var client = CreateClient();

        // The nested lazy prop is resolved on a partial visit.
        var json = await JsonAsync(client, "/resolved/nested",
            ("X-Inertia-Partial-Component", "Test/Page"),
            ("X-Inertia-Partial-Data", "nested"));

        Assert.That(json["props"]!["nested"]!["inner"]!.GetValue<string>(), Is.EqualTo("inner-value"));
    }

    [Test]
    [Description("SC: sequential resolution by default; two DI props sharing a scoped service resolve in order.")]
    public async Task ResolvedSequential_OverHttp()
    {
        using var client = CreateClient();

        var json = await JsonAsync(client, "/resolved/sequential");

        Assert.Multiple(() =>
        {
            Assert.That(json["props"]!["first"]!["resolvedInScope"]!.GetValue<int>(), Is.EqualTo(1));
            Assert.That(json["props"]!["second"]!["resolvedInScope"]!.GetValue<int>(), Is.EqualTo(2));
            Assert.That(json["props"]!["first"]!["scopeId"]!.GetValue<string>(),
                Is.EqualTo(json["props"]!["second"]!["scopeId"]!.GetValue<string>()),
                "both props resolve from the same request scope");
        });
    }
}

/// <summary>Runs the DI-resolved prop integration tests against the default host.</summary>
[TestFixture]
public class UnitTestResolvedPropsIntegration : UnitTestResolvedPropsIntegrationBase
{
}

/// <summary>With parallel resolution opted in, DI props still resolve correctly.</summary>
[TestFixture]
public class UnitTestResolvedPropsParallel : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("UseResolvedProps", "true");
        builder.UseSetting("UseParallelPropResolution", "true");
    }

    [Test]
    [Description("SC: with parallel resolution, DI props still resolve from the request scope.")]
    public async Task Parallel_ResolvesScopedProps()
    {
        using var client = CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/resolved/sequential");
        request.Headers.Add("X-Inertia", "true");

        var response = await client.SendAsync(request);
        var json = (await response.Content.ReadFromJsonAsync<JsonNode>())!;

        Assert.Multiple(() =>
        {
            Assert.That(json["props"]!["first"]!["scopeId"]!.GetValue<string>(), Is.Not.Null.And.Not.Empty);
            Assert.That(json["props"]!["second"]!["scopeId"]!.GetValue<string>(), Is.Not.Null.And.Not.Empty);
        });
    }
}
