using InertiaCore;
using InertiaCore.Extensions;
using InertiaCore.TagHelpers;
using InertiaCore.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace InertiaCoreTests;

[TestFixture]
public class UnitTestTagHelpers
{
    private class FakeViteBuilder : IViteBuilder
    {
        public HtmlString ReactRefresh() => new HtmlString("<!--refresh-->");
        public Task<HtmlString> ReactRefreshAsync() => Task.FromResult(ReactRefresh());
        public HtmlString Input(params string[] entryPoints) => new HtmlString($"<!--input:{string.Join(",", entryPoints)}-->");
        public Task<HtmlString> InputAsync(params string[] entryPoints) => Task.FromResult(Input(entryPoints));
        public string? GetManifest() => null;
        public string? GetManifestHash() => null;
        public bool IsRunningHot() => false;
        public string Asset(string entryPoint) => entryPoint;
        public string Content(string entryPoint) => entryPoint;
    }

    private ServiceProvider? _provider;
    private IServiceScope? _scope;

    [SetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();
        services.AddInertia();
        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();

        var ctx = new DefaultHttpContext { RequestServices = _scope.ServiceProvider };
        Inertia.UseContextAccessor(new HttpContextAccessor { HttpContext = ctx });

        Vite.UseBuilder(new FakeViteBuilder());
    }

    [TearDown]
    public void TearDown()
    {
        Inertia.UseContextAccessor(null);
        _scope?.Dispose();
        _provider?.Dispose();
    }

    private static TagHelperOutput Run(TagHelper helper, string tagName)
    {
        var context = new TagHelperContext(
            new TagHelperAttributeList(),
            new Dictionary<object, object>(),
            Guid.NewGuid().ToString("N"));

        var output = new TagHelperOutput(
            tagName,
            new TagHelperAttributeList(),
            (useCachedResult, encoder) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));

        helper.Process(context, output);
        return output;
    }

    [Test]
    public async Task InertiaTagHelper_RendersPage()
    {
        var helper = new InertiaTagHelper
        {
            ViewContext = new ViewContext { ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = new { Title = "Hi" } } }
        };

        var context = new TagHelperContext(new TagHelperAttributeList(), new Dictionary<object, object>(), Guid.NewGuid().ToString("N"));
        var output = new TagHelperOutput("inertia", new TagHelperAttributeList(), (u, e) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));

        await helper.ProcessAsync(context, output);

        Assert.That(output.TagName, Is.Null);
        Assert.That(output.Content.GetContent(), Does.Contain("Hi").Or.Contain("title"));
    }

    [Test]
    public void ViteInputTagHelper_RendersInput()
    {
        var helper = new ViteInputTagHelper { Src = "src/main.ts" };
        var output = Run(helper, "vite-input");

        Assert.That(output.TagName, Is.Null);
        Assert.That(output.Content.GetContent(), Does.Contain("src/main.ts"));
    }

    [Test]
    public void ViteReactRefreshTagHelper_RendersRefresh()
    {
        var helper = new ViteReactRefreshTagHelper();
        var output = Run(helper, "vite-react-refresh");

        Assert.That(output.TagName, Is.Null);
        Assert.That(output.Content.GetContent(), Does.Contain("refresh"));
    }
}
