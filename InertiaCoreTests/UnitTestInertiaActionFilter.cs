using InertiaCore.Extensions;
using InertiaCore.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Moq;

namespace InertiaCoreTests;

[TestFixture]
public class UnitTestInertiaActionFilter
{
    private InertiaActionFilter CreateFilter()
    {
        var urlHelperFactory = new Mock<IUrlHelperFactory>();
        return new InertiaActionFilter(urlHelperFactory.Object);
    }

    private static ActionExecutedContext CreateContext(string method, IActionResult result,
        bool isInertiaRequest = true)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;
        if (isInertiaRequest)
            httpContext.Request.Headers["X-Inertia"] = "true";

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        return new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), controller: null!)
        {
            Result = result
        };
    }

    [Test]
    public void POST_RedirectResult_ConvertsTo303()
    {
        var filter = CreateFilter();
        var context = CreateContext("POST", new RedirectResult("/success"));

        filter.OnActionExecuted(context);

        Assert.Multiple(() =>
        {
            Assert.That(context.Result, Is.TypeOf<StatusCodeResult>());
            Assert.That(((StatusCodeResult)context.Result!).StatusCode, Is.EqualTo(303));
            Assert.That(context.HttpContext.Response.Headers["Location"].ToString(), Is.EqualTo("/success"));
        });
    }

    [Test]
    public void PUT_RedirectResult_ConvertsTo303()
    {
        var filter = CreateFilter();
        var context = CreateContext("PUT", new RedirectResult("/success"));

        filter.OnActionExecuted(context);

        Assert.Multiple(() =>
        {
            Assert.That(context.Result, Is.TypeOf<StatusCodeResult>());
            Assert.That(((StatusCodeResult)context.Result!).StatusCode, Is.EqualTo(303));
            Assert.That(context.HttpContext.Response.Headers["Location"].ToString(), Is.EqualTo("/success"));
        });
    }

    [Test]
    public void PATCH_RedirectResult_ConvertsTo303()
    {
        var filter = CreateFilter();
        var context = CreateContext("PATCH", new RedirectResult("/success"));

        filter.OnActionExecuted(context);

        Assert.Multiple(() =>
        {
            Assert.That(context.Result, Is.TypeOf<StatusCodeResult>());
            Assert.That(((StatusCodeResult)context.Result!).StatusCode, Is.EqualTo(303));
            Assert.That(context.HttpContext.Response.Headers["Location"].ToString(), Is.EqualTo("/success"));
        });
    }

    [Test]
    public void DELETE_RedirectResult_ConvertsTo303()
    {
        var filter = CreateFilter();
        var context = CreateContext("DELETE", new RedirectResult("/success"));

        filter.OnActionExecuted(context);

        Assert.Multiple(() =>
        {
            Assert.That(context.Result, Is.TypeOf<StatusCodeResult>());
            Assert.That(((StatusCodeResult)context.Result!).StatusCode, Is.EqualTo(303));
            Assert.That(context.HttpContext.Response.Headers["Location"].ToString(), Is.EqualTo("/success"));
        });
    }

    [Test]
    public void GET_RedirectResult_PassesThrough()
    {
        var filter = CreateFilter();
        var result = new RedirectResult("/success");
        var context = CreateContext("GET", result);

        filter.OnActionExecuted(context);

        Assert.That(context.Result, Is.SameAs(result));
    }

    [Test]
    public void POST_NonRedirect_PassesThrough()
    {
        var filter = CreateFilter();
        var result = new OkResult();
        var context = CreateContext("POST", result);

        filter.OnActionExecuted(context);

        Assert.That(context.Result, Is.SameAs(result));
    }

    [Test]
    public void POST_WithoutInertiaHeader_PassesThrough()
    {
        var filter = CreateFilter();
        var result = new RedirectResult("/success");
        var context = CreateContext("POST", result, isInertiaRequest: false);

        filter.OnActionExecuted(context);

        Assert.That(context.Result, Is.SameAs(result));
    }
}
