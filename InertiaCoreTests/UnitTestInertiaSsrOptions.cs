using InertiaCore.Models;

namespace InertiaCoreTests;

/// <summary>
/// Unit tests for <see cref="InertiaSsrOptions"/>: defaults and derived endpoint URLs.
/// </summary>
[TestFixture]
public class UnitTestInertiaSsrOptions
{
    [Test]
    [Description("Managed SSR is opt-in and the defaults match the Inertia SSR convention.")]
    public void Defaults()
    {
        var options = new InertiaSsrOptions();

        Assert.Multiple(() =>
        {
            Assert.That(options.Managed, Is.False, "opt-in by default");
            Assert.That(options.NodePath, Is.EqualTo("node"));
            Assert.That(options.ScriptPath, Is.EqualTo("ssr.mjs"));
            Assert.That(options.Port, Is.EqualTo(13714));
            Assert.That(options.StartupTimeout, Is.EqualTo(TimeSpan.FromSeconds(30)));
            Assert.That(options.RestartPolicy, Is.EqualTo(RestartPolicy.OnFailure));
            Assert.That(options.InitialBackoff, Is.EqualTo(TimeSpan.FromMilliseconds(500)));
            Assert.That(options.MaxBackoff, Is.EqualTo(TimeSpan.FromSeconds(10)));
            Assert.That(options.MaxRestarts, Is.EqualTo(5));
            Assert.That(options.WorkingDirectory, Is.Null);
            Assert.That(options.EnvironmentVariables, Is.Empty);
            Assert.That(options.ShutdownTimeout, Is.EqualTo(TimeSpan.FromSeconds(5)));
        });
    }

    [Test]
    [Description("The health, shutdown and render URLs derive from the configured port.")]
    public void DerivedUrls_UsePort()
    {
        var options = new InertiaSsrOptions { Port = 9000 };

        Assert.Multiple(() =>
        {
            Assert.That(options.HealthUrl, Is.EqualTo("http://127.0.0.1:9000/health"));
            Assert.That(options.ShutdownUrl, Is.EqualTo("http://127.0.0.1:9000/shutdown"));
            Assert.That(options.RenderUrl, Is.EqualTo("http://127.0.0.1:9000/render"));
        });
    }

    [Test]
    [Description("InertiaOptions exposes an Ssr object defaulting to unmanaged.")]
    public void InertiaOptions_ExposesSsr()
    {
        var options = new InertiaOptions();

        Assert.That(options.Ssr, Is.Not.Null);
        Assert.That(options.Ssr.Managed, Is.False);
    }

    [Test]
    [Description("RestartPolicy exposes exactly None and OnFailure.")]
    public void RestartPolicy_Values()
    {
        Assert.That(Enum.GetValues<RestartPolicy>(), Is.EquivalentTo(new[] { RestartPolicy.None, RestartPolicy.OnFailure }));
    }
}
