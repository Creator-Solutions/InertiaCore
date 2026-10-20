using InertiaCore.Models;
using InertiaCore.Ssr;

namespace InertiaCoreTests;

/// <summary>
/// Unit tests for <see cref="InertiaSsrOptionsValidator"/>.
/// </summary>
[TestFixture]
public class UnitTestInertiaSsrOptionsValidator
{
    private static InertiaOptions Managed(Action<InertiaSsrOptions>? configure = null)
    {
        var options = new InertiaOptions();
        options.Ssr.Managed = true;
        configure?.Invoke(options.Ssr);
        return options;
    }

    [Test]
    [Description("Default managed options are valid.")]
    public void Valid_Defaults()
    {
        var result = new InertiaSsrOptionsValidator().Validate(null, Managed());

        Assert.That(result.Succeeded, Is.True);
    }

    [Test]
    [Description("Validation is a no-op when management is disabled, even with otherwise-invalid values.")]
    public void Disabled_SkipsValidation()
    {
        var options = new InertiaOptions();
        options.Ssr.Managed = false;
        options.Ssr.NodePath = "";
        options.Ssr.ScriptPath = "";
        options.Ssr.Port = 70000;
        options.Ssr.StartupTimeout = TimeSpan.Zero;

        var result = new InertiaSsrOptionsValidator().Validate(null, options);

        Assert.That(result.Succeeded, Is.True);
    }

    [Test]
    [Description("An empty NodePath fails validation.")]
    public void NodePath_Empty_Fails()
    {
        var result = new InertiaSsrOptionsValidator().Validate(null, Managed(o => o.NodePath = " "));

        Assert.That(result.Failed, Is.True);
        Assert.That(result.Failures!, Has.Some.Contains(nameof(InertiaSsrOptions.NodePath)));
    }

    [Test]
    [Description("An empty ScriptPath fails validation.")]
    public void ScriptPath_Empty_Fails()
    {
        var result = new InertiaSsrOptionsValidator().Validate(null, Managed(o => o.ScriptPath = ""));

        Assert.That(result.Failed, Is.True);
        Assert.That(result.Failures!, Has.Some.Contains(nameof(InertiaSsrOptions.ScriptPath)));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(70000)]
    [Description("A Port outside 1..65535 fails validation.")]
    public void Port_OutOfRange_Fails(int port)
    {
        var result = new InertiaSsrOptionsValidator().Validate(null, Managed(o => o.Port = port));

        Assert.That(result.Failed, Is.True);
        Assert.That(result.Failures!, Has.Some.Contains(nameof(InertiaSsrOptions.Port)));
    }

    [Test]
    [Description("A non-positive StartupTimeout fails validation.")]
    public void StartupTimeout_NonPositive_Fails()
    {
        var result = new InertiaSsrOptionsValidator().Validate(null, Managed(o => o.StartupTimeout = TimeSpan.Zero));

        Assert.That(result.Failed, Is.True);
        Assert.That(result.Failures!, Has.Some.Contains(nameof(InertiaSsrOptions.StartupTimeout)));
    }

    [Test]
    [Description("A non-positive InitialBackoff fails validation.")]
    public void InitialBackoff_NonPositive_Fails()
    {
        var result = new InertiaSsrOptionsValidator().Validate(null, Managed(o => o.InitialBackoff = TimeSpan.Zero));

        Assert.That(result.Failed, Is.True);
        Assert.That(result.Failures!, Has.Some.Contains(nameof(InertiaSsrOptions.InitialBackoff)));
    }

    [Test]
    [Description("A MaxBackoff smaller than InitialBackoff fails validation.")]
    public void MaxBackoff_LessThanInitial_Fails()
    {
        var result = new InertiaSsrOptionsValidator().Validate(null,
            Managed(o =>
            {
                o.InitialBackoff = TimeSpan.FromSeconds(5);
                o.MaxBackoff = TimeSpan.FromSeconds(1);
            }));

        Assert.That(result.Failed, Is.True);
        Assert.That(result.Failures!, Has.Some.Contains(nameof(InertiaSsrOptions.MaxBackoff)));
    }

    [Test]
    [Description("MaxRestarts below -1 fails validation; -1 (unlimited) is allowed.")]
    public void MaxRestarts_BelowMinusOne_Fails()
    {
        var validator = new InertiaSsrOptionsValidator();

        Assert.That(validator.Validate(null, Managed(o => o.MaxRestarts = -2)).Failed, Is.True);
        Assert.That(validator.Validate(null, Managed(o => o.MaxRestarts = -1)).Succeeded, Is.True);
    }

    [Test]
    [Description("All failures are reported together.")]
    public void MultipleFailures_AreAggregated()
    {
        var result = new InertiaSsrOptionsValidator().Validate(null,
            Managed(o =>
            {
                o.NodePath = "";
                o.Port = 0;
            }));

        Assert.That(result.Failures!, Has.Count.GreaterThanOrEqualTo(2));
    }
}
