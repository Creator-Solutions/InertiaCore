using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using InertiaCore.Models;
using InertiaCore.Utils;
using Microsoft.Extensions.Options;
using Moq;

namespace InertiaCoreTests;

/// <summary>
/// SC-06: Vite tag attribute values and the prefetch event must be encoded for the
/// context they are written into.
/// </summary>
[TestFixture]
public class UnitTestViteEncoding
{
    private static ViteBuilder CreateBuilder(ViteOptions options, params (string Path, string Content)[] files)
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { @"/wwwroot/build/manifest.json", new MockFileData(
                "{\"app.tsx\": {\"file\": \"assets/main.js\",\"dynamicImports\": [\"_page.js\"]}," +
                "\"_page.js\": {\"file\": \"assets/page.js\"}}") }
        });

        foreach (var (path, content) in files)
            fileSystem.AddFile(path, new MockFileData(content));

        var optionsMock = new Mock<IOptions<ViteOptions>>();
        optionsMock.SetupGet(x => x.Value).Returns(options);

        var builder = new Mock<ViteBuilder>(optionsMock.Object);
        builder.Object.UseFileSystem(fileSystem);
        return builder.Object;
    }

    [Test]
    [Description("SC-06: a nonce containing quotes/angle brackets is HTML-attribute-encoded.")]
    public async Task Nonce_IsAttributeEncoded()
    {
        var builder = CreateBuilder(new ViteOptions { CspNonce = "abc\" onload=\"alert(1)" });

        var html = (await builder.InputAsync("app.tsx")).ToString() ?? "";

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Not.Contain("onload=\"alert(1)\""));
            Assert.That(html, Does.Contain("nonce=\"abc&quot; onload=&quot;alert(1)\""));
        });
    }

    [Test]
    [Description("SC-06: a custom script attribute value is HTML-attribute-encoded.")]
    public async Task CustomAttributeValue_IsAttributeEncoded()
    {
        var options = new ViteOptions();
        options.ScriptTagAttributes.Add((_, _, _) => new Dictionary<string, object?>
        {
            ["data-x"] = "\" onload=\"alert(1)"
        });

        var builder = CreateBuilder(options);

        var html = (await builder.InputAsync("app.tsx")).ToString() ?? "";

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Not.Contain("onload=\"alert(1)\""));
            Assert.That(html, Does.Contain("data-x=\"&quot; onload=&quot;alert(1)\""));
        });
    }

    [Test]
    [Description("SC-06: the React refresh nonce is HTML-attribute-encoded.")]
    public void ReactRefresh_Nonce_IsAttributeEncoded()
    {
        var builder = CreateBuilder(
            new ViteOptions { CspNonce = "\"><script>alert(1)</script>" },
            (@"/wwwroot/hot", "http://127.0.0.1:5174"));

        var html = builder.ReactRefresh().ToString() ?? "";

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Not.Contain("\"><script>"));
            Assert.That(html, Does.Contain("nonce=\"&quot;&gt;&lt;script&gt;alert(1)&lt;/script&gt;\""));
        });
    }

    [Test]
    [Description("SC-06: the prefetch event is emitted as a serialized (escaped) string literal.")]
    public async Task PrefetchEvent_IsSerialized()
    {
        const string prefetchEvent = "load');document.body.innerHTML='pwned';//";

        var builder = CreateBuilder(new ViteOptions
        {
            PrefetchStrategy = PrefetchStrategy.Aggressive,
            PrefetchEvent = prefetchEvent
        });

        var html = (await builder.InputAsync("app.tsx")).ToString() ?? "";

        Assert.That(html, Does.Contain($"window.addEventListener({JsonSerializer.Serialize(prefetchEvent)}"));
    }
}
