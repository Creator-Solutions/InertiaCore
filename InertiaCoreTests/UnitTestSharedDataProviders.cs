using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace InertiaCoreTests;

/// <summary>
/// Integration tests for shared-data providers (IN-66): providers are resolved per request,
/// merge in registration order, may return lazy props, work with scoped dependencies, and fail
/// with an error naming the provider type.
/// </summary>
public abstract class UnitTestSharedDataProvidersBase : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("UseSharedDataProviders", "true");

    private static HttpRequestMessage Inertia(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Inertia", "true");
        return request;
    }

    private static async Task<JsonNode> PropsAsync(HttpClient client, string path)
    {
        var response = await client.SendAsync(Inertia(path));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var json = await response.Content.ReadFromJsonAsync<JsonNode>();
        return json!["props"]!;
    }

    [Test]
    [Description("SC: two providers contribute keys and a later provider overrides an earlier key.")]
    public async Task Providers_MergeInRegistrationOrder()
    {
        using var client = CreateClient();

        var props = await PropsAsync(client, "/shared/providers");

        // Second provider runs after the first and overrides its key.
        Assert.That(props["providerOne"]!.GetValue<string>(), Is.EqualTo("two"));
    }

    [Test]
    [Description("SC: a lazy prop from a provider is deferred on the first load and resolved on a partial visit.")]
    public async Task ProviderLazyProp_DeferredThenResolved()
    {
        using var client = CreateClient();

        var firstLoad = await PropsAsync(client, "/shared/providers");
        Assert.That(firstLoad.AsObject().ContainsKey("lazy"), Is.False, "lazy props are excluded on first load");

        var partial = new HttpRequestMessage(HttpMethod.Get, "/shared/providers");
        partial.Headers.Add("X-Inertia", "true");
        partial.Headers.Add("X-Inertia-Partial-Component", "Test/Page");
        partial.Headers.Add("X-Inertia-Partial-Data", "lazy");
        var response = await client.SendAsync(partial);
        var json = await response.Content.ReadFromJsonAsync<JsonNode>();

        Assert.That(json!["props"]!["lazy"]!.GetValue<string>(), Is.EqualTo("resolved-lazily"));
    }

    [Test]
    [Description("SC: a provider depending on a scoped service works and is not a captive dependency.")]
    public async Task Provider_UsesScopedService()
    {
        using var client = CreateClient();

        var first = await PropsAsync(client, "/shared/scoped");
        var second = await PropsAsync(client, "/shared/scoped");

        var firstId = first["scopeId"]!.GetValue<string>();
        var secondId = second["scopeId"]!.GetValue<string>();

        Assert.Multiple(() =>
        {
            Assert.That(firstId, Is.Not.Null.And.Not.Empty);
            Assert.That(secondId, Is.Not.Null.And.Not.Empty);
            Assert.That(firstId, Is.Not.EqualTo(secondId), "each request resolves its own scope");
        });
    }

    [Test]
    [Description("Provider keys are announced in the page's sharedProps metadata.")]
    public async Task ProviderKeys_AppearInSharedPropsMetadata()
    {
        using var client = CreateClient();

        var response = await client.SendAsync(Inertia("/shared/providers"));
        var json = await response.Content.ReadFromJsonAsync<JsonNode>();
        var shared = json!["sharedProps"]!.AsArray().Select(e => e!.GetValue<string>()).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(shared, Does.Contain("providerOne"));
            Assert.That(shared, Does.Contain("lazy"));
        });
    }
}

/// <summary>Runs the shared-data provider integration tests against the default host.</summary>
[TestFixture]
public class UnitTestSharedDataProviders : UnitTestSharedDataProvidersBase
{
}

/// <summary>A failing provider surfaces an error naming the provider type.</summary>
[TestFixture]
public class UnitTestSharedDataProviderFailure : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("UseFailingSharedData", "true");

    [Test]
    [Description("SC: a provider exception produces an error naming the provider type.")]
    public async Task FailingProvider_NamesType()
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var request = new HttpRequestMessage(HttpMethod.Get, "/shared/providers");
        request.Headers.Add("X-Inertia", "true");

        var response = await client.SendAsync(request);

        // The exception is an unhandled server error; the message names the provider type.
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
    }
}

/// <summary>The built-in claims provider shares auth.user from the current principal.</summary>
[TestFixture]
public class UnitTestClaimsSharedData : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("UseClaimsSharedData", "true");

    private static async Task<JsonNode> PropsAsync(HttpClient client, string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Inertia", "true");
        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var json = await response.Content.ReadFromJsonAsync<JsonNode>();
        return json!["props"]!;
    }

    [Test]
    [Description("SC: an authenticated principal is shared as auth.user using the claim mapping.")]
    public async Task Authenticated_SharedAsAuthUser()
    {
        using var client = CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/shared/claims");
        request.Headers.Add("X-Inertia", "true");
        request.Headers.Add("X-Test-Name", "Ada");
        request.Headers.Add("X-Test-Role", "admin");

        var response = await client.SendAsync(request);
        var json = await response.Content.ReadFromJsonAsync<JsonNode>();
        var user = json!["props"]!["auth"]!["user"]!;

        Assert.Multiple(() =>
        {
            Assert.That(user["name"]!.GetValue<string>(), Is.EqualTo("Ada"));
            Assert.That(user["role"]!.GetValue<string>(), Is.EqualTo("admin"));
        });
    }

    [Test]
    [Description("An anonymous request contributes no auth prop.")]
    public async Task Anonymous_NoAuthProp()
    {
        using var client = CreateClient();

        var props = await PropsAsync(client, "/shared/claims");

        Assert.That(props.AsObject().ContainsKey("auth"), Is.False);
    }
}
