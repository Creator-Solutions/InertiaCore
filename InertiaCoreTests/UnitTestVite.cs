using System.IO.Abstractions.TestingHelpers;
using InertiaCore.Extensions;
using InertiaCore.Models;
using InertiaCore.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;

namespace InertiaCoreTests;

public partial class Tests
{
    [Test]
    [Description("Test if the Vite configuration registers properly the service.")]
    public void TestViteConfiguration()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddInertia();

        Assert.Multiple(() =>
        {
            Assert.DoesNotThrow(() => builder.Services.AddViteHelper());
            Assert.That(builder.Services.Any(s => s.ServiceType == typeof(IViteBuilder)), Is.True);
        });

        var app = builder.Build();
        Assert.DoesNotThrow(() => app.UseInertia());

        Assert.DoesNotThrow(() => Vite.ReactRefresh());
    }

    [Test]
    [Description("Test if the Vite Helper handles hot module reloading properly.")]
    public async Task TestHot()
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { @"/wwwroot/hot", new MockFileData("http://127.0.0.1:5174") },
        });
        var options = new Mock<IOptions<ViteOptions>>();
        options.SetupGet(x => x.Value).Returns(new ViteOptions());

        var mock = new Mock<ViteBuilder>(options.Object);
        mock.Object.UseFileSystem(fileSystem);

        var htmlResult = await mock.Object.ReactRefreshAsync();

        const string inner =
            "<script type=\"module\">import RefreshRuntime from 'http://127.0.0.1:5174/@react-refresh';" +
            "RefreshRuntime.injectIntoGlobalHook(window);" +
            "window.$RefreshReg$ = () => { };" +
            "window.$RefreshSig$ = () => (type) => type;" +
            "window.__vite_plugin_react_preamble_installed__ = true;</script>";

        Assert.That(htmlResult.ToString(), Is.EqualTo(inner));
    }

    [Test]
    [Description("Test if the Vite Helper handles HMR disabled properly.")]
    public async Task TestNotHot()
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>());
        var options = new Mock<IOptions<ViteOptions>>();
        options.SetupGet(x => x.Value).Returns(new ViteOptions());

        var mock = new Mock<ViteBuilder>(options.Object);
        mock.Object.UseFileSystem(fileSystem);

        Assert.That((await mock.Object.ReactRefreshAsync()).ToString(), Is.EqualTo("<!-- no hot -->"));
    }

    [Test]
    [Description(
        "Test if the Vite Helper handles generating HTML tags for both JS and CSS from HMR and the manifest properly.")]
    public async Task TestViteInput()
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>());
        var options = new Mock<IOptions<ViteOptions>>();
        options.SetupGet(x => x.Value).Returns(new ViteOptions());

        var mock = new Mock<ViteBuilder>(options.Object);
        mock.Object.UseFileSystem(fileSystem);

        // Missing manifest exception
        Assert.ThrowsAsync<Exception>(() => mock.Object.InputAsync("app.tsx"));

        fileSystem.AddFile(@"/wwwroot/build/manifest.json", new MockFileData("null"));

        // Null manifest exception
        Assert.ThrowsAsync<Exception>(() => mock.Object.InputAsync("app.tsx"));

        // Missing info exception
        fileSystem.AddFile(@"/wwwroot/build/manifest.json", new MockFileData("{\"main.tsx\": {}}"));

        Assert.ThrowsAsync<Exception>(() => mock.Object.InputAsync("app.tsx"));

        // Basic JS File
        fileSystem.AddFile(@"/wwwroot/build/manifest.json",
            new MockFileData("{\"app.tsx\": {\"file\": \"assets/main-19038c6a.js\"}}"));
        mock.Object.ClearManifestCache();

        var result = await mock.Object.InputAsync("app.tsx");
        Assert.That(result.ToString(),
            Is.EqualTo(
                "<link rel=\"modulepreload\" as=\"script\" href=\"/build/assets/main-19038c6a.js\" />\n\t" +
                "<script type=\"module\" src=\"/build/assets/main-19038c6a.js\"></script>\n\t"));

        // Basic JS File with CSS import
        fileSystem.AddFile(@"/wwwroot/build/manifest.json",
            new MockFileData("{\"app.tsx\": {\"file\": \"assets/main.js\",\"css\": [\"assets/index.css\"]}}"));
        mock.Object.ClearManifestCache();

        result = await mock.Object.InputAsync("app.tsx");
        Assert.That(result.ToString(),
            Is.EqualTo(
                "<link rel=\"preload\" as=\"style\" href=\"/build/assets/index.css\" />\n\t" +
                "<link rel=\"modulepreload\" as=\"script\" href=\"/build/assets/main.js\" />\n\t" +
                "<link rel=\"stylesheet\" href=\"/build/assets/index.css\" />\n\t" +
                "<script type=\"module\" src=\"/build/assets/main.js\"></script>\n\t"));

        // JS File that imports another chunk which has its own CSS
        fileSystem.AddFile(@"/wwwroot/build/manifest.json",
            new MockFileData(
                "{\"app.tsx\": {\"file\": \"assets/main.js\",\"imports\": [\"_vendor.js\"]}," +
                "\"_vendor.js\": {\"file\": \"assets/vendor.js\",\"css\": [\"assets/vendor.css\"]}}"));
        mock.Object.ClearManifestCache();

        result = await mock.Object.InputAsync("app.tsx");
        Assert.That(result.ToString(),
            Is.EqualTo(
                "<link rel=\"preload\" as=\"style\" href=\"/build/assets/vendor.css\" />\n\t" +
                "<link rel=\"modulepreload\" as=\"script\" href=\"/build/assets/main.js\" />\n\t" +
                "<link rel=\"modulepreload\" as=\"script\" href=\"/build/assets/vendor.js\" />\n\t" +
                "<link rel=\"stylesheet\" href=\"/build/assets/vendor.css\" />\n\t" +
                "<script type=\"module\" src=\"/build/assets/main.js\"></script>\n\t"));

        // Basic CSS file
        fileSystem.AddFile(@"/wwwroot/build/manifest.json",
            new MockFileData("{\"index.scss\": {\"file\": \"assets/index.css\"}}"));
        mock.Object.ClearManifestCache();

        result = await mock.Object.InputAsync("index.scss");
        Assert.That(result.ToString(),
            Is.EqualTo(
                "<link rel=\"preload\" as=\"style\" href=\"/build/assets/index.css\" />\n\t" +
                "<link rel=\"stylesheet\" href=\"/build/assets/index.css\" />\n\t"));

        // Basic CSS file with custom builder dir
        fileSystem.AddFile(@"/wwwroot/manifest.json",
            new MockFileData("{\"index.scss\": {\"file\": \"assets/index.css\"}}"));
        mock.Object.ClearManifestCache();
        options.SetupGet(x => x.Value).Returns(new ViteOptions
        {
            BuildDirectory = null
        });
        result = await mock.Object.InputAsync("index.scss");
        Assert.That(result.ToString(),
            Is.EqualTo(
                "<link rel=\"preload\" as=\"style\" href=\"/assets/index.css\" />\n\t" +
                "<link rel=\"stylesheet\" href=\"/assets/index.css\" />\n\t"));

        // Hot file with css import
        options.SetupGet(x => x.Value).Returns(new ViteOptions
        {
            BuildDirectory = "build"
        });
        fileSystem.AddFile(@"/wwwroot/hot", new MockFileData("http://127.0.0.1:5174"));

        result = await mock.Object.InputAsync("index.scss");
        Assert.That(result.ToString(), Is.EqualTo(
            "<script type=\"module\" src=\"http://127.0.0.1:5174/@vite/client\"></script>\n\t" +
            "<script type=\"module\" src=\"http://127.0.0.1:5174/index.scss\"></script>\n\t"));

        // Test null build directory
        fileSystem.AddFile(@"/wwwroot/hot", new MockFileData("http://127.0.0.1:5174"));
        options.SetupGet(x => x.Value).Returns(new ViteOptions
        {
            BuildDirectory = null
        });
        result = await mock.Object.InputAsync("index.scss");
        Assert.That(result.ToString(), Is.EqualTo(
            "<script type=\"module\" src=\"http://127.0.0.1:5174/@vite/client\"></script>\n\t" +
            "<script type=\"module\" src=\"http://127.0.0.1:5174/index.scss\"></script>\n\t"));

        // Test empty build directory
        options.SetupGet(x => x.Value).Returns(new ViteOptions
        {
            BuildDirectory = ""
        });
        result = await mock.Object.InputAsync("index.scss");
        Assert.That(result.ToString(), Is.EqualTo(
            "<script type=\"module\" src=\"http://127.0.0.1:5174/@vite/client\"></script>\n\t" +
            "<script type=\"module\" src=\"http://127.0.0.1:5174/index.scss\"></script>\n\t"));

        // Basic JS File via hot
        result = await mock.Object.InputAsync("app.tsx");
        Assert.That(result.ToString(), Is.EqualTo(
            "<script type=\"module\" src=\"http://127.0.0.1:5174/@vite/client\"></script>\n\t" +
            "<script type=\"module\" src=\"http://127.0.0.1:5174/app.tsx\"></script>\n\t"));
    }

    [Test]
    [Description("Test that multiple entry points are rendered together.")]
    public async Task TestViteMultipleEntryPoints()
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            {
                @"/wwwroot/build/manifest.json",
                new MockFileData(
                    "{\"app.tsx\": {\"file\": \"assets/app.js\"}," +
                    "\"admin.tsx\": {\"file\": \"assets/admin.js\"}}")
            }
        });
        var options = new Mock<IOptions<ViteOptions>>();
        options.SetupGet(x => x.Value).Returns(new ViteOptions());

        var mock = new Mock<ViteBuilder>(options.Object);
        mock.Object.UseFileSystem(fileSystem);

        var result = await mock.Object.InputAsync("app.tsx", "admin.tsx");

        Assert.That(result.ToString(), Is.EqualTo(
            "<link rel=\"modulepreload\" as=\"script\" href=\"/build/assets/app.js\" />\n\t" +
            "<link rel=\"modulepreload\" as=\"script\" href=\"/build/assets/admin.js\" />\n\t" +
            "<script type=\"module\" src=\"/build/assets/app.js\"></script>\n\t" +
            "<script type=\"module\" src=\"/build/assets/admin.js\"></script>\n\t"));
    }

    [Test]
    [Description("Test that a CSP nonce is applied to every generated tag.")]
    public async Task TestViteNonce()
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            {
                @"/wwwroot/build/manifest.json",
                new MockFileData("{\"app.tsx\": {\"file\": \"assets/main.js\",\"css\": [\"assets/index.css\"]}}")
            }
        });
        var options = new Mock<IOptions<ViteOptions>>();
        options.SetupGet(x => x.Value).Returns(new ViteOptions { CspNonce = "abc123" });

        var mock = new Mock<ViteBuilder>(options.Object);
        mock.Object.UseFileSystem(fileSystem);

        var result = await mock.Object.InputAsync("app.tsx");

        Assert.That(result.ToString(), Is.EqualTo(
            "<link rel=\"preload\" as=\"style\" href=\"/build/assets/index.css\" nonce=\"abc123\" />\n\t" +
            "<link rel=\"modulepreload\" as=\"script\" href=\"/build/assets/main.js\" nonce=\"abc123\" />\n\t" +
            "<link rel=\"stylesheet\" href=\"/build/assets/index.css\" nonce=\"abc123\" />\n\t" +
            "<script type=\"module\" src=\"/build/assets/main.js\" nonce=\"abc123\"></script>\n\t"));
    }

    [Test]
    [Description("Test that nonce resolvers and custom tag attributes are honoured.")]
    public async Task TestViteAttributes()
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { @"/wwwroot/build/manifest.json", new MockFileData("{\"app.tsx\": {\"file\": \"assets/main.js\"}}") }
        });
        var options = new Mock<IOptions<ViteOptions>>();
        options.SetupGet(x => x.Value).Returns(new ViteOptions
        {
            NonceResolver = () => "resolved",
            IntegrityKey = "integrity",
            ScriptTagAttributes =
            {
                (_, _, _) => new Dictionary<string, object?> { ["defer"] = true, ["data-role"] = "app" }
            }
        });

        var mock = new Mock<ViteBuilder>(options.Object);
        mock.Object.UseFileSystem(fileSystem);

        var result = await mock.Object.InputAsync("app.tsx");

        Assert.That(result.ToString(), Is.EqualTo(
            "<link rel=\"modulepreload\" as=\"script\" href=\"/build/assets/main.js\" nonce=\"resolved\" />\n\t" +
            "<script type=\"module\" src=\"/build/assets/main.js\" nonce=\"resolved\" defer data-role=\"app\"></script>\n\t"));
    }

    [Test]
    [Description("Test that the manifest integrity hash is emitted as a tag attribute.")]
    public async Task TestViteIntegrity()
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            {
                @"/wwwroot/build/manifest.json",
                new MockFileData(
                    "{\"app.tsx\": {\"file\": \"assets/main.js\",\"integrity\": \"sha384-xyz\"}}")
            }
        });
        var options = new Mock<IOptions<ViteOptions>>();
        options.SetupGet(x => x.Value).Returns(new ViteOptions());

        var mock = new Mock<ViteBuilder>(options.Object);
        mock.Object.UseFileSystem(fileSystem);

        var result = await mock.Object.InputAsync("app.tsx");

        Assert.That(result.ToString(), Is.EqualTo(
            "<link rel=\"modulepreload\" as=\"script\" href=\"/build/assets/main.js\" integrity=\"sha384-xyz\" />\n\t" +
            "<script type=\"module\" src=\"/build/assets/main.js\" integrity=\"sha384-xyz\"></script>\n\t"));
    }

    [Test]
    [Description("Test the aggressive prefetch strategy emits a prefetch script for dynamic imports.")]
    public async Task TestVitePrefetchAggressive()
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            {
                @"/wwwroot/build/manifest.json",
                new MockFileData(
                    "{\"app.tsx\": {\"file\": \"assets/app.js\",\"dynamicImports\": [\"_page.js\"]}," +
                    "\"_page.js\": {\"file\": \"assets/page-abc.js\",\"css\": [\"assets/page.css\"]}," +
                    "\"assets/page.css\": {\"file\": \"assets/page.css\"}}")
            }
        });
        var options = new Mock<IOptions<ViteOptions>>();
        options.SetupGet(x => x.Value).Returns(new ViteOptions
        {
            PrefetchStrategy = PrefetchStrategy.Aggressive
        });

        var mock = new Mock<ViteBuilder>(options.Object);
        mock.Object.UseFileSystem(fileSystem);

        var result = (await mock.Object.InputAsync("app.tsx")).ToString();

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain("window.addEventListener(\"load\""));
            Assert.That(result, Does.Contain("\"rel\":\"prefetch\""));
            Assert.That(result, Does.Contain("\"href\":\"/build/assets/page-abc.js\""));
            Assert.That(result, Does.Contain("\"href\":\"/build/assets/page.css\""));
            Assert.That(result, Does.Not.Contain("loadNext"));
        });
    }

    [Test]
    [Description("Test the waterfall prefetch strategy loads assets concurrently.")]
    public async Task TestVitePrefetchWaterfall()
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            {
                @"/wwwroot/build/manifest.json",
                new MockFileData(
                    "{\"app.tsx\": {\"file\": \"assets/app.js\",\"dynamicImports\": [\"_page.js\"]}," +
                    "\"_page.js\": {\"file\": \"assets/page-abc.js\"}}")
            }
        });
        var options = new Mock<IOptions<ViteOptions>>();
        options.SetupGet(x => x.Value).Returns(new ViteOptions
        {
            PrefetchStrategy = PrefetchStrategy.Waterfall,
            PrefetchConcurrency = 2,
            PrefetchEvent = "DOMContentLoaded"
        });

        var mock = new Mock<ViteBuilder>(options.Object);
        mock.Object.UseFileSystem(fileSystem);

        var result = (await mock.Object.InputAsync("app.tsx")).ToString();

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain("window.addEventListener(\"DOMContentLoaded\""));
            Assert.That(result, Does.Contain("loadNext("));
            Assert.That(result, Does.Contain(", 2)"));
        });
    }

    [Test]
    [Description("Test the asset, content and hot helpers.")]
    public async Task TestViteAssetHelpers()
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { @"/wwwroot/build/manifest.json", new MockFileData("{\"app.tsx\": {\"file\": \"assets/main.js\"}}") },
            { @"/wwwroot/build/assets/main.js", new MockFileData("console.log(1);") }
        });
        var options = new Mock<IOptions<ViteOptions>>();
        options.SetupGet(x => x.Value).Returns(new ViteOptions());

        var mock = new Mock<ViteBuilder>(options.Object);
        mock.Object.UseFileSystem(fileSystem);

        Assert.Multiple(() =>
        {
            Assert.That(mock.Object.IsRunningHot(), Is.False);
            Assert.That(mock.Object.Asset("app.tsx"), Is.EqualTo("/build/assets/main.js"));
            Assert.That(mock.Object.Content("app.tsx"), Is.EqualTo("console.log(1);"));
            Assert.That(mock.Object.GetManifestHash(), Is.Not.Null);
        });

        fileSystem.AddFile(@"/wwwroot/hot", new MockFileData("http://127.0.0.1:5174"));

        Assert.Multiple(() =>
        {
            Assert.That(mock.Object.IsRunningHot(), Is.True);
            Assert.That(mock.Object.Asset("app.tsx"), Is.EqualTo("http://127.0.0.1:5174/app.tsx"));
            Assert.That(mock.Object.GetManifestHash(), Is.Null);
        });
    }

    [Test]
    [Description("Test if the Vite Facade behaves correctly with different builder configurations.")]
    public void TestViteFacade()
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>());
        var options = new Mock<IOptions<ViteOptions>>();
        options.SetupGet(x => x.Value).Returns(new ViteOptions
        {
            BuildDirectory = "build2",
            ManifestFilename = "manifest-test.json",
            HotFile = "cold",
            PublicDirectory = "public"
        });

        var mock = new Mock<ViteBuilder>(options.Object);
        mock.Object.UseFileSystem(fileSystem);

        Vite.UseBuilder(mock.Object);

        // Basic Manifest with CSS file
        fileSystem.AddFile(@"/public/build2/manifest-test.json",
            new MockFileData("{\"index.scss\": {\"file\": \"assets/index.css\"}}"));
        var result = Vite.Input("index.scss");
        Assert.That(result.ToString(),
            Is.EqualTo(
                "<link rel=\"preload\" as=\"style\" href=\"/build2/assets/index.css\" />\n\t" +
                "<link rel=\"stylesheet\" href=\"/build2/assets/index.css\" />\n\t"));

        // Hot file with css import
        options.SetupGet(x => x.Value).Returns(new ViteOptions
        {
            BuildDirectory = null,
            ManifestFilename = "manifest-test.json",
            HotFile = "cold",
            PublicDirectory = "public"
        });
        fileSystem.AddFile(@"/public/cold", new MockFileData("http://127.0.0.1:5174"));
        result = Vite.Input("index.scss");
        Assert.That(result.ToString(), Is.EqualTo(
            "<script type=\"module\" src=\"http://127.0.0.1:5174/@vite/client\"></script>\n\t" +
            "<script type=\"module\" src=\"http://127.0.0.1:5174/index.scss\"></script>\n\t"));
    }

    [Test]
    [Description("Test if the vite version is read properly.")]
    public async Task TestViteVersion()
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>());
        var options = new Mock<IOptions<ViteOptions>>();
        options.SetupGet(x => x.Value).Returns(new ViteOptions());

        var mock = new Mock<ViteBuilder>(options.Object);
        mock.Object.UseFileSystem(fileSystem);
        Vite.UseBuilder(mock.Object);

        fileSystem.AddFile(@"/wwwroot/build/manifest.json",
            new MockFileData("{\"app.tsx\": {\"file\": \"assets/main-19038c6a.js\"}}"));

        _factory.Version(Vite.GetManifestHash);

        var response = _factory.Render("Test/Page", new
        {
            Test = "Test"
        });

        var headers = new HeaderDictionary
        {
            { "X-Inertia", "true" }
        };

        var context = PrepareContext(headers);

        response.SetContext(context);
        await response.ProcessResponse();

        var result = response.GetResult();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.InstanceOf<JsonResult>());

            var json = (result as JsonResult)?.Value;
            Assert.That((json as Page)?.Version, Is.EqualTo("bba1afd1066309f4a69430e0c446ba8d"));
        });
    }
}
