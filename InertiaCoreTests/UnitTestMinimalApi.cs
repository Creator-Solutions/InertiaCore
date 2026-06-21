using System.Text.Json;
using InertiaCore.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;

namespace InertiaCoreTests;

public partial class Tests
{
    private static ActionContext PrepareMinimalApiContext(HeaderDictionary? headers = null, string path = "/")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;

        if (headers != null)
            foreach (var (key, value) in headers)
                httpContext.Request.Headers[key] = value;

        return new ActionContext(
            httpContext,
            httpContext.GetRouteData() ?? new RouteData(),
            new ActionDescriptor(),
            new ModelStateDictionary()
        );
    }

    [Test]
    [Description("Minimal API path: Inertia request returns a JsonResult with the correct page model.")]
    public async Task TestMinimalApiInertiaRequestReturnsJson()
    {
        var response = _factory.Render("Test/Page", new { Test = "Test" });

        var context = PrepareMinimalApiContext(new HeaderDictionary
        {
            { "X-Inertia", "true" }
        });

        response.SetContext(context);
        await response.ProcessResponse();

        var result = response.GetResult();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.InstanceOf<JsonResult>());

            var page = (result as JsonResult)?.Value as Page;
            Assert.That(page, Is.Not.Null);
            Assert.That(page!.Component, Is.EqualTo("Test/Page"));
            Assert.That(page.Props, Is.EqualTo(new Dictionary<string, object?>
            {
                { "test", "Test" },
                { "errors", new Dictionary<string, string>(0) }
            }));
        });
    }

    [Test]
    [Description("Minimal API path: non-Inertia request (full page load) returns a ViewResult.")]
    public async Task TestMinimalApiNonInertiaRequestReturnsView()
    {
        var response = _factory.Render("Test/Page", new { Test = "Test" });


        var context = PrepareMinimalApiContext();

        response.SetContext(context);
        await response.ProcessResponse();

        var result = response.GetResult();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.InstanceOf<ViewResult>());
            Assert.That((result as ViewResult)?.ViewName, Is.EqualTo("~/Views/App.cshtml"));

            var page = (result as ViewResult)?.Model as Page;
            Assert.That(page, Is.Not.Null);
            Assert.That(page!.Component, Is.EqualTo("Test/Page"));
        });
    }

    [Test]
    [Description("Minimal API path: shared props are merged into the page model.")]
    public async Task TestMinimalApiSharedPropsAreResolved()
    {
        _factory.Share("sharedKey", "sharedValue");

        var response = _factory.Render("Test/Page", new { Test = "Test" });

        var context = PrepareMinimalApiContext(new HeaderDictionary
        {
            { "X-Inertia", "true" }
        });

        response.SetContext(context);
        await response.ProcessResponse();

        var page = (response.GetResult() as JsonResult)?.Value as Page;

        Assert.Multiple(() =>
        {
            Assert.That(page?.Props, Contains.Key("sharedKey"));
            Assert.That(page?.Props["sharedKey"], Is.EqualTo("sharedValue"));
        });
    }

    [Test]
    [Description("Minimal API path: component props take precedence over shared props with the same key.")]
    public async Task TestMinimalApiComponentPropsOverrideSharedProps()
    {
        _factory.Share("test", "shared-value");

        var response = _factory.Render("Test/Page", new { Test = "component-value" });

        var context = PrepareMinimalApiContext(new HeaderDictionary
        {
            { "X-Inertia", "true" }
        });

        response.SetContext(context);
        await response.ProcessResponse();

        var page = (response.GetResult() as JsonResult)?.Value as Page;

        Assert.That(page?.Props["test"], Is.EqualTo("component-value"));
    }

    [Test]
    [Description("Minimal API path: Request.Path is used as the page URL.")]
    public async Task TestMinimalApiUrlIsPopulatedFromRequestPath()
    {
        var response = _factory.Render("Test/Page");

        var context = PrepareMinimalApiContext(
            headers: new HeaderDictionary { { "X-Inertia", "true" } },
            path: "/dashboard"
        );

        response.SetContext(context);
        await response.ProcessResponse();

        var page = (response.GetResult() as JsonResult)?.Value as Page;

        Assert.That(page?.Url, Is.EqualTo("/dashboard"));
    }

    [Test]
    [Description("Minimal API path: X-Inertia response header is set on Inertia requests.")]
    public async Task TestMinimalApiInertiaHeaderIsSet()
    {
        var response = _factory.Render("Test/Page");

        var context = PrepareMinimalApiContext(new HeaderDictionary
        {
            { "X-Inertia", "true" }
        });

        response.SetContext(context);
        await response.ProcessResponse();


        response.GetJson();

        Assert.That(
            context.HttpContext.Response.Headers["X-Inertia"].ToString(),
            Is.EqualTo("true")
        );
    }

    [Test]
    [Description("Minimal API path: IResult.ExecuteAsync writes JSON to the response body for Inertia requests.")]
    public async Task TestMinimalApiExecuteAsyncWritesJsonBody()
    {
        var response = _factory.Render("Test/Page", new { Test = "Test" });

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Inertia"] = "true";
        httpContext.Response.Body = new MemoryStream();

        await ((IResult)response).ExecuteAsync(httpContext);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();
        var page = JsonSerializer.Deserialize<Page>(body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.Multiple(() =>
        {
            Assert.That(httpContext.Response.ContentType, Does.Contain("application/json"));
            Assert.That(page?.Component, Is.EqualTo("Test/Page"));
        });
    }
}