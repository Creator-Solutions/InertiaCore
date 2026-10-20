using System.Net;
using System.Text;
using InertiaCore;
using InertiaCore.Models;
using InertiaCore.Services;
using InertiaCore.Ssr;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InertiaCoreTests;

[TestFixture]
public class UnitTestSsrGateway
{
    private static Gateway CreateGateway(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler,
        Action<InertiaOptions>? configure = null)
    {
        var options = new InertiaOptions();
        configure?.Invoke(options);

        return new Gateway(
            new InertiaSsrClient(new HttpClient(new StubHandler(handler), disposeHandler: false)),
            Options.Create(new JsonOptions()),
            Options.Create(options),
            NullLogger<Gateway>.Instance);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Test]
    [Description("SC-01: a non-2xx SSR response is treated as a failure and returns null.")]
    public async Task Dispatch_NonSuccessStatus_ReturnsNull()
    {
        var gateway = CreateGateway((_, _) =>
            Task.FromResult(Json(HttpStatusCode.InternalServerError, "{\"head\":[],\"body\":\"<p>err</p>\"}")));

        var result = await gateway.Dispatch(new { Test = "Test" }, "http://127.0.0.1:13714/render");

        Assert.That(result, Is.Null);
    }

    [Test]
    [Description("SC-01: an SSR request that exceeds the timeout is cancelled and returns null.")]
    public async Task Dispatch_Timeout_ReturnsNull()
    {
        var gateway = CreateGateway(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return Json(HttpStatusCode.OK, "{\"head\":[],\"body\":\"<p>late</p>\"}");
        }, o => o.SsrTimeout = TimeSpan.FromMilliseconds(50));

        var result = await gateway.Dispatch(new { Test = "Test" }, "http://127.0.0.1:13714/render");

        Assert.That(result, Is.Null);
    }

    [Test]
    [Description("SC-01: a non-http(s) SSR URL is rejected and returns null.")]
    public async Task Dispatch_InvalidScheme_ReturnsNull()
    {
        var gateway = CreateGateway((_, _) =>
            Task.FromResult(Json(HttpStatusCode.OK, "{\"head\":[],\"body\":\"<p>x</p>\"}")));

        var result = await gateway.Dispatch(new { Test = "Test" }, "file:///etc/passwd");

        Assert.That(result, Is.Null);
    }

    [Test]
    [Description("SC-01: an SSR response larger than the configured cap is rejected and returns null.")]
    public async Task Dispatch_OversizedBody_ReturnsNull()
    {
        var huge = "{\"head\":[],\"body\":\"" + new string('a', 500) + "\"}";
        var gateway = CreateGateway((_, _) => Task.FromResult(Json(HttpStatusCode.OK, huge)),
            o => o.SsrMaxResponseBytes = 64);

        var result = await gateway.Dispatch(new { Test = "Test" }, "http://127.0.0.1:13714/render");

        Assert.That(result, Is.Null);
    }

    [Test]
    [Description("SC-01/SC-14: an SSR response with a null body is treated as a failure.")]
    public async Task Dispatch_NullBody_ReturnsNull()
    {
        var gateway = CreateGateway((_, _) =>
            Task.FromResult(Json(HttpStatusCode.OK, "{\"head\":[],\"body\":null}")));

        var result = await gateway.Dispatch(new { Test = "Test" }, "http://127.0.0.1:13714/render");

        Assert.That(result, Is.Null);
    }

    [Test]
    [Description("SC-01: a transport-level failure returns null instead of throwing.")]
    public async Task Dispatch_TransportError_ReturnsNull()
    {
        var gateway = CreateGateway((_, _) => throw new HttpRequestException("connection refused"));

        var result = await gateway.Dispatch(new { Test = "Test" }, "http://127.0.0.1:13714/render");

        Assert.That(result, Is.Null);
    }

    [Test]
    [Description("SC-01: a malformed JSON body returns null instead of throwing.")]
    public async Task Dispatch_MalformedJson_ReturnsNull()
    {
        var gateway = CreateGateway((_, _) =>
            Task.FromResult(Json(HttpStatusCode.OK, "<html>not json</html>")));

        var result = await gateway.Dispatch(new { Test = "Test" }, "http://127.0.0.1:13714/render");

        Assert.That(result, Is.Null);
    }

    [Test]
    [Description("SC-01: a valid SSR response is returned unchanged.")]
    public async Task Dispatch_ValidResponse_ReturnsResponse()
    {
        var gateway = CreateGateway((_, _) =>
            Task.FromResult(Json(HttpStatusCode.OK, "{\"head\":[\"<title>x</title>\"],\"body\":\"<p>ok</p>\"}")));

        var result = await gateway.Dispatch(new { Test = "Test" }, "http://127.0.0.1:13714/render");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Body, Is.EqualTo("<p>ok</p>"));
            Assert.That(result.Head, Is.EqualTo(new List<string> { "<title>x</title>" }));
        });
    }

    [Test]
    [Description("SC-01: a failed SSR dispatch makes Html() fall back to the client-side script tag.")]
    public async Task Html_SsrFails_FallsBackToClientSideScript()
    {
        var options = new InertiaOptions { SsrEnabled = true };
        var factory = new ResponseFactory(
            new HttpContextAccessor(),
            new NullGateway(),
            Options.Create(options),
            new InertiaState(),
            Options.Create(new JsonOptions()));

        var html = await factory.Html(new { Test = "Test" });

        Assert.That(html.ToString(),
            Is.EqualTo("<script data-page=\"app\" type=\"application/json\">{\"test\":\"Test\"}</script>\n<div id=\"app\"></div>"));
    }

    [Test]
    [Description("SC-14: SsrResponse guards null Head and Body.")]
    public void SsrResponse_NullMembers_DoNotThrow()
    {
        var response = new SsrResponse { Head = null!, Body = null! };

        Assert.Multiple(() =>
        {
            Assert.That(response.GetHead().ToString(), Is.EqualTo(string.Empty));
            Assert.That(response.GetBody().ToString(), Is.EqualTo(string.Empty));
        });
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) =>
            _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _handler(request, cancellationToken);
    }

    private sealed class NullGateway : IGateway
    {
        public Task<SsrResponse?> Dispatch(object model, string url) => Task.FromResult<SsrResponse?>(null);
    }
}
