using InertiaCore.Extensions;
using InertiaCore.Utils;
using Microsoft.AspNetCore.Http;

namespace InertiaCoreTests;

[TestFixture]
public class UnitTestPrefetch
{
    private static DefaultHttpContext Context(string? purpose)
    {
        var context = new DefaultHttpContext();
        if (purpose != null)
            context.Request.Headers[InertiaHeader.Purpose] = purpose;

        return context;
    }

    [Test]
    [Description("IsInertiaPrefetch is true only for 'Purpose: prefetch'.")]
    public void IsInertiaPrefetch_Detection()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Context("prefetch").IsInertiaPrefetch(), Is.True);
            Assert.That(Context("Prefetch").IsInertiaPrefetch(), Is.False);
            Assert.That(Context("other").IsInertiaPrefetch(), Is.False);
            Assert.That(Context(null).IsInertiaPrefetch(), Is.False);
        });
    }
}
