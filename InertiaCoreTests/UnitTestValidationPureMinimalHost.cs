using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InertiaCore.Contracts;
using InertiaCore.Extensions;
using InertiaCore.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace InertiaCoreTests;

/// <summary>
/// Validation on a host that does not call AddControllers/AddControllersWithViews. These
/// hosts have no MVC TempData, so the flash must persist through the Inertia default path.
/// </summary>
[TestFixture]
public class UnitTestValidationPureMinimalHost
{
    private static async Task<WebApplication> StartHostAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddInertia();

        var app = builder.Build();
        app.UseInertia();

        app.MapPost("/validation", (ValidationInput input) => Results.Text("ok"))
            .WithInertiaValidation();

        app.MapGet("/validation/page", async (IInertia inertia) =>
            (IResult)await inertia.Render("Test/Page", new { ok = true }));

        await app.StartAsync();
        return app;
    }

    /// <summary>
    /// Sends a request and copies any Set-Cookie values into the Cookie header, the way a
    /// browser would, so a flash cookie written on the 303 reaches the follow-up GET.
    /// </summary>
    private sealed class CookieJar
    {
        private readonly HttpClient _client;
        private readonly Dictionary<string, string> _cookies = new(StringComparer.Ordinal);

        public CookieJar(HttpClient client) => _client = client;

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            if (_cookies.Count > 0)
                request.Headers.Add("Cookie", string.Join("; ", _cookies.Select(p => $"{p.Key}={p.Value}")));

            var response = await _client.SendAsync(request);

            if (response.Headers.TryGetValues("Set-Cookie", out var values))
            {
                foreach (var value in values)
                {
                    var pair = value.Split(';')[0];
                    var idx = pair.IndexOf('=');
                    if (idx > 0)
                        _cookies[pair[..idx]] = pair[(idx + 1)..];
                }
            }

            return response;
        }
    }

    [Test]
    [Description("SC: on a host without MVC, validation errors survive the 303 redirect and render on the next page.")]
    public async Task NoMvc_ErrorsSurviveRedirect()
    {
        await using var app = await StartHostAsync();
        var client = app.GetTestClient();
        var jar = new CookieJar(client);

        var post = new HttpRequestMessage(HttpMethod.Post, "/validation")
        {
            Content = JsonContent.Create(new { name = "", address = new { street = "" }, items = Array.Empty<object>() })
        };
        post.Headers.Add("X-Inertia", "true");
        post.Headers.Add("Referer", "/form");

        var submit = await jar.SendAsync(post);

        var get = new HttpRequestMessage(HttpMethod.Get, "/validation/page");
        get.Headers.Add("X-Inertia", "true");
        var page = await jar.SendAsync(get);
        var errors = (await page.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("props").GetProperty("errors");

        Assert.Multiple(() =>
        {
            Assert.That(submit.StatusCode, Is.EqualTo(HttpStatusCode.SeeOther));
            Assert.That(errors.TryGetProperty("name", out _), Is.True, "the flashed errors must reach the next page");
            Assert.That(errors.TryGetProperty("address.street", out _), Is.True);
        });
    }
}
