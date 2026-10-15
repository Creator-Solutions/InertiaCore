using System.Text.Encodings.Web;
using System.Text.Json;
using InertiaCore;
using InertiaCore.Models;
using InertiaCore.Services;
using InertiaCore.Ssr;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace InertiaCoreTests;

/// <summary>
/// SC-07: the page object embedded in the initial HTML must be escaped for the
/// script context independently of the host's JSON encoder.
/// </summary>
[TestFixture]
public class UnitTestHtmlEscaping
{
    private static ResponseFactory CreateFactory(JavaScriptEncoder encoder)
    {
        var jsonOptions = new JsonOptions();
        jsonOptions.JsonSerializerOptions.Encoder = encoder;

        return new ResponseFactory(
            new HttpContextAccessor(),
            new NullGateway(),
            Options.Create(new InertiaOptions()),
            new InertiaState(),
            Options.Create(jsonOptions));
    }

    private static string ExtractJson(string html)
    {
        const string open = "<script data-page=\"app\" type=\"application/json\">";
        const string close = "</script>";
        var start = html.IndexOf(open, StringComparison.Ordinal) + open.Length;
        var end = html.IndexOf(close, start, StringComparison.Ordinal);
        return html.Substring(start, end - start);
    }

    [TestCase("</script>", TestName = "ScriptClose")]
    [TestCase("<!--<script>", TestName = "CommentScript")]
    [TestCase("<img onerror>", TestName = "ImgOnError")]
    [TestCase("a & b", TestName = "Ampersand")]
    [TestCase("line\u2028sep", TestName = "LineSeparator")]
    [TestCase("para\u2029sep", TestName = "ParagraphSeparator")]
    [Description("SC-07: hostile payloads are escaped even with UnsafeRelaxedJsonEscaping.")]
    public async Task Html_UnsafeEncoder_EscapesScriptContext(string payload)
    {
        var factory = CreateFactory(JavaScriptEncoder.UnsafeRelaxedJsonEscaping);

        var html = (await factory.Html(new { Test = payload })).ToString() ?? "";
        var json = ExtractJson(html);

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Not.Contain("<"));
            Assert.That(json, Does.Not.Contain(">"));
            Assert.That(json, Does.Not.Contain("&"));
            Assert.That(json, Does.Not.Contain("\u2028"));
            Assert.That(json, Does.Not.Contain("\u2029"));

            // The escaped payload is still valid JSON and round-trips to the original value.
            var document = JsonDocument.Parse(json);
            Assert.That(document.RootElement.GetProperty("test").GetString(), Is.EqualTo(payload));
        });
    }

    [Test]
    [Description("SC-07: a script-closing payload cannot terminate the script element.")]
    public async Task Html_ScriptClose_CannotBreakOut()
    {
        var factory = CreateFactory(JavaScriptEncoder.UnsafeRelaxedJsonEscaping);

        var html = (await factory.Html(new { Test = "</script><script>alert(1)</script>" })).ToString();

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Not.Contain("</script><script>"));
            Assert.That(html, Does.Not.Contain("<script>alert(1)"));
        });
    }

    [Test]
    [Description("SC-07: simple payloads keep their existing (default-encoder) output.")]
    public async Task Html_DefaultEncoder_SimplePayload_OutputUnchanged()
    {
        var factory = CreateFactory(JavaScriptEncoder.Default);

        var html = (await factory.Html(new { Test = "Test" })).ToString();

        Assert.That(html,
            Is.EqualTo("<script data-page=\"app\" type=\"application/json\">{\"test\":\"Test\"}</script>\n<div id=\"app\"></div>"));
    }

    private sealed class NullGateway : IGateway
    {
        public Task<SsrResponse?> Dispatch(object model, string url) => Task.FromResult<SsrResponse?>(null);
    }
}
