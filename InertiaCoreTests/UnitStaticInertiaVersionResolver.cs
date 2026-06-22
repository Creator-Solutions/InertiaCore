using InertiaCore.Resolvers;
using NUnit;

namespace InertiaCoreTests;


public class StaticInertiaVersionResolverTests
{
    [Test]
    public void GetVersion_Returns_StaticString()
    {
        var expected = "v2.5.0";
        var resolver = new StaticInertiaVersionResolver(expected);
        var version = resolver.GetVersion();
        Assert.That(version, Is.EqualTo(expected));   // ✅ NUnit 4
    }

    [Test]
    public void GetVersion_WithEmptyString_StillReturnsEmpty()
    {
        var resolver = new StaticInertiaVersionResolver("");
        var version = resolver.GetVersion();
        Assert.AreEqual("", version);
    }
}
