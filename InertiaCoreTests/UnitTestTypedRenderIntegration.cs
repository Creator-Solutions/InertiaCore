using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace InertiaCoreTests;

/// <summary>
/// End-to-end test of typed rendering: the source generator emits the
/// type-to-component mapping, the module initializer registers it, and
/// <c>Render&lt;T&gt;</c> resolves the component over HTTP.
/// </summary>
[TestFixture]
public class UnitTestTypedRenderIntegration : WebApplicationFactory<Program>
{
    [Test]
    [Description("Render<T> resolves the component from the generated type map over HTTP.")]
    public async Task TypedRender_OverHttp_ResolvesComponent()
    {
        using var client = CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/typed");
        request.Headers.Add("X-Inertia", "true");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body.GetProperty("component").GetString(), Is.EqualTo("Users/Index"));
            Assert.That(body.GetProperty("props").GetProperty("users").GetArrayLength(), Is.EqualTo(2));
        });
    }

    [Test]
    [Description("The TypeGen sample endpoint serializes props matching the generated declarations.")]
    public async Task TypedSample_OverHttp_MatchesGeneratedShape()
    {
        using var client = CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/typed-sample");
        request.Headers.Add("X-Inertia", "true");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var props = body.GetProperty("props");
        var user = props.GetProperty("users")[0];

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body.GetProperty("component").GetString(), Is.EqualTo("TypedSample/Index"));
            Assert.That(props.GetProperty("total_count").GetInt32(), Is.EqualTo(2));
            Assert.That(user.GetProperty("id").GetString(), Is.EqualTo(Guid.Empty.ToString()));
            Assert.That(user.GetProperty("role").GetInt32(), Is.EqualTo(2));
            Assert.That(user.GetProperty("status").GetString(), Is.EqualTo("Active"));
            Assert.That(user.GetProperty("address").GetProperty("street").GetString(), Is.EqualTo("1 Analytical Engine Way"));
            Assert.That(user.GetProperty("scores").GetProperty("logic").GetInt32(), Is.EqualTo(10));
        });
    }
}
