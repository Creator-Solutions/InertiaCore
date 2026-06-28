using InertiaCore.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;

namespace InertiaCoreTests;

[TestFixture]
public class UnitTestLocationResult
{
    private static ActionContext CreateContext()
    {
        var httpContext = new DefaultHttpContext();
        return new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
    }

    [Test]
    public void Sets409Conflict()
    {
        var result = new LocationResult("https://example.com");
        var context = CreateContext();

        result.ExecuteResultAsync(context);

        Assert.That(context.HttpContext.Response.StatusCode, Is.EqualTo(409));
    }

    [Test]
    public void SetsXInertiaLocationHeader()
    {
        var result = new LocationResult("https://example.com");
        var context = CreateContext();

        result.ExecuteResultAsync(context);

        Assert.That(
            context.HttpContext.Response.Headers["X-Inertia-Location"].ToString(),
            Is.EqualTo("https://example.com"));
    }

    [Test]
    public void ImplementsIActionResult()
    {
        var result = new LocationResult("/some-path");
        Assert.That(result, Is.InstanceOf<IActionResult>());
    }

    [Test]
    public void ImplementsIResult()
    {
        var result = new LocationResult("/some-path");
        Assert.That(result, Is.InstanceOf<IResult>());
    }
}
