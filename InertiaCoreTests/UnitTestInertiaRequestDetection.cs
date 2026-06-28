using InertiaCore.Extensions;
using Microsoft.AspNetCore.Http;

namespace InertiaCoreTests;

[TestFixture]
public class UnitTestInertiaRequestDetection
{
    private static HttpContext CreateContext(string? headerValue)
    {
        var context = new DefaultHttpContext();
        if (headerValue != null)
            context.Request.Headers["X-Inertia"] = headerValue;
        return context;
    }

    [Test]
    public void ReturnsTrue_WhenHeaderIsTrue()
    {
        var context = CreateContext("true");
        Assert.That(context.IsInertiaRequest(), Is.True);
    }

    [Test]
    public void ReturnsFalse_WhenHeaderIsFalse()
    {
        var context = CreateContext("false");
        Assert.That(context.IsInertiaRequest(), Is.False);
    }

    [Test]
    public void ReturnsFalse_WhenHeaderIsMissing()
    {
        var context = CreateContext(null);
        Assert.That(context.IsInertiaRequest(), Is.False);
    }

    [Test]
    public void ReturnsFalse_WhenHeaderIsInvalidInteger()
    {
        var context = CreateContext("1");
        Assert.That(context.IsInertiaRequest(), Is.False);
    }

    [Test]
    public void ReturnsFalse_WhenHeaderIsEmpty()
    {
        var context = CreateContext("");
        Assert.That(context.IsInertiaRequest(), Is.False);
    }
}
