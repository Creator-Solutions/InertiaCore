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
}
