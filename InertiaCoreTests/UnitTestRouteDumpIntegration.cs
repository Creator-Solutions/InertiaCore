using System.Linq;
using System.Net;
using System.Text.Json;
using InertiaCore.TypeGen;
using Microsoft.AspNetCore.Mvc.Testing;

namespace InertiaCoreTests;

/// <summary>
/// End-to-end test of the route dump: the Development-only <c>/_inertia/routes</c> endpoint
/// serializes the named Minimal API routes, and the TypeGen <c>routes</c> generator turns
/// that metadata into a typed <c>routes.ts</c> module.
/// </summary>
[TestFixture]
public class UnitTestRouteDumpIntegration : WebApplicationFactory<Program>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private async Task<InertiaRoutesDocument> DumpRoutesAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.GetAsync("/_inertia/routes");
        string payload = await response.Content.ReadAsStringAsync();
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), payload);
        return JsonSerializer.Deserialize<InertiaRoutesDocument>(payload, JsonOptions)
               ?? new InertiaRoutesDocument();
    }

    [Test]
    [Description("The Development route endpoint dumps the named Minimal API routes.")]
    public async Task DumpsNamedRoutes()
    {
        using var client = CreateClient();

        InertiaRoutesDocument document = await DumpRoutesAsync(client);
        var names = document.Routes.Select(static r => r.Name).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(
                names,
                Is.EquivalentTo(new[] { "users.index", "users.show", "users.update", "posts.show", "search" }));
            Assert.That(document.Routes.All(static r => r.HttpMethods.Count > 0), Is.True);

            InertiaRouteDto show = document.Routes.Single(static r => r.Name == "users.show");
            Assert.That(show.Pattern, Is.EqualTo("/api/users/{id}"));
            Assert.That(show.Parameters.Single().Constraint, Is.EqualTo("int"));

            InertiaRouteDto search = document.Routes.Single(static r => r.Name == "search");
            Assert.That(search.Parameters.Single().IsCatchAll, Is.True);
        });
    }

    [Test]
    [Description("The dumped routes match the checked-in golden routes.ts.")]
    public async Task Routes_MatchGoldenFile()
    {
        using var client = CreateClient();

        InertiaRoutesDocument document = await DumpRoutesAsync(client);
        string actual = new InertiaRouteScriptGenerator().Generate(document.Routes);

        string goldenPath = Path.Combine(AppContext.BaseDirectory, "Golden", "routes.sample.ts");
        Assert.That(File.Exists(goldenPath), Is.True, $"Golden file not found at {goldenPath}.");

        Assert.That(actual, Is.EqualTo(File.ReadAllText(goldenPath)));
    }

    [Test]
    [Description("The committed route dump JSON still matches the live Development endpoint.")]
    public async Task Routes_MatchSampleJson()
    {
        using var client = CreateClient();

        InertiaRoutesDocument live = await DumpRoutesAsync(client);

        string samplePath = Path.Combine(AppContext.BaseDirectory, "Golden", "routes.sample.json");
        Assert.That(File.Exists(samplePath), Is.True, $"Sample JSON not found at {samplePath}.");
        InertiaRoutesDocument committed =
            JsonSerializer.Deserialize<InertiaRoutesDocument>(File.ReadAllText(samplePath), JsonOptions)
            ?? new InertiaRoutesDocument();

        Assert.That(
            JsonSerializer.Serialize(live, JsonOptions),
            Is.EqualTo(JsonSerializer.Serialize(committed, JsonOptions)));
    }
}
