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

    // --- SSR resilience (IN-69) ---

    private static InertiaOptions Ssr(Action<InertiaOptions>? configure = null)
    {
        var options = new InertiaOptions { SsrEnabled = true };
        configure?.Invoke(options);
        return options;
    }

    [Test]
    [Description("The default resilience options are valid when SSR is enabled.")]
    public void Resilience_Defaults_AreValid()
    {
        var result = new InertiaSsrOptionsValidator().Validate(null, Ssr());

        Assert.That(result.Succeeded, Is.True);
    }

    [Test]
    [Description("Resilience validation is a no-op when SSR is disabled, even with invalid values.")]
    public void Resilience_Disabled_SkipsValidation()
    {
        var options = new InertiaOptions
        {
            SsrEnabled = false,
            SsrHealthUrl = "not-a-url"
        };
        options.SsrResilience.MaxRetryAttempts = -5;
        options.SsrResilience.CircuitBreakerFailureRatio = 0;

        var result = new InertiaSsrOptionsValidator().Validate(null, options);

        Assert.That(result.Succeeded, Is.True);
    }

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(11)]
    [Description("A MaxRetryAttempts outside 1..10 fails validation (the handler does not support disabling retries).")]
    public void Resilience_MaxRetryAttempts_OutOfRange_Fails(int attempts)
    {
        var result = new InertiaSsrOptionsValidator().Validate(null,
            Ssr(o => o.SsrResilience.MaxRetryAttempts = attempts));

        Assert.That(result.Failed, Is.True);
        Assert.That(result.Failures!, Has.Some.Contains(nameof(InertiaSsrResilienceOptions.MaxRetryAttempts)));
    }

    [TestCase(1)]
    [TestCase(10)]
    [Description("A MaxRetryAttempts inside 1..10 is accepted.")]
    public void Resilience_MaxRetryAttempts_InRange_IsValid(int attempts)
    {
        var result = new InertiaSsrOptionsValidator().Validate(null,
            Ssr(o => o.SsrResilience.MaxRetryAttempts = attempts));

        Assert.That(result.Succeeded, Is.True);
    }

    [Test]
    [Description("A non-positive RetryBaseDelay fails validation.")]
    public void Resilience_RetryBaseDelay_NonPositive_Fails()
    {
        var result = new InertiaSsrOptionsValidator().Validate(null,
            Ssr(o => o.SsrResilience.RetryBaseDelay = TimeSpan.Zero));

        Assert.That(result.Failed, Is.True);
        Assert.That(result.Failures!, Has.Some.Contains(nameof(InertiaSsrResilienceOptions.RetryBaseDelay)));
    }

    [Test]
    [Description("A non-positive AttemptTimeout fails validation.")]
    public void Resilience_AttemptTimeout_NonPositive_Fails()
    {
        var result = new InertiaSsrOptionsValidator().Validate(null,
            Ssr(o => o.SsrResilience.AttemptTimeout = TimeSpan.Zero));

        Assert.That(result.Failed, Is.True);
        Assert.That(result.Failures!, Has.Some.Contains(nameof(InertiaSsrResilienceOptions.AttemptTimeout)));
    }

    [Test]
    [Description("An AttemptTimeout exceeding SsrTimeout fails validation; equal is allowed.")]
    public void Resilience_AttemptTimeout_ExceedingBudget_Fails()
    {
        var validator = new InertiaSsrOptionsValidator();

        Assert.That(validator.Validate(null, Ssr(o =>
        {
            o.SsrTimeout = TimeSpan.FromSeconds(2);
            o.SsrResilience.AttemptTimeout = TimeSpan.FromSeconds(3);
        })).Failed, Is.True);

        Assert.That(validator.Validate(null, Ssr(o =>
        {
            o.SsrTimeout = TimeSpan.FromSeconds(2);
            o.SsrResilience.AttemptTimeout = TimeSpan.FromSeconds(2);
        })).Succeeded, Is.True);
    }

    [TestCase(0.0)]
    [TestCase(1.5)]
    [Description("A CircuitBreakerFailureRatio outside (0, 1] fails validation.")]
    public void Resilience_FailureRatio_OutOfRange_Fails(double ratio)
    {
        var result = new InertiaSsrOptionsValidator().Validate(null,
            Ssr(o => o.SsrResilience.CircuitBreakerFailureRatio = ratio));

        Assert.That(result.Failed, Is.True);
        Assert.That(result.Failures!, Has.Some.Contains(nameof(InertiaSsrResilienceOptions.CircuitBreakerFailureRatio)));
    }

    [Test]
    [Description("A CircuitBreakerMinimumThroughput below 1 fails validation.")]
    public void Resilience_MinimumThroughput_BelowOne_Fails()
    {
        var result = new InertiaSsrOptionsValidator().Validate(null,
            Ssr(o => o.SsrResilience.CircuitBreakerMinimumThroughput = 0));

        Assert.That(result.Failed, Is.True);
        Assert.That(result.Failures!, Has.Some.Contains(nameof(InertiaSsrResilienceOptions.CircuitBreakerMinimumThroughput)));
    }

    [Test]
    [Description("Non-positive circuit-breaker durations fail validation.")]
    public void Resilience_Durations_NonPositive_Fails()
    {
        var validator = new InertiaSsrOptionsValidator();

        Assert.That(validator.Validate(null, Ssr(o => o.SsrResilience.CircuitBreakerSamplingDuration = TimeSpan.Zero)).Failed, Is.True);
        Assert.That(validator.Validate(null, Ssr(o => o.SsrResilience.CircuitBreakerBreakDuration = TimeSpan.Zero)).Failed, Is.True);
    }

    [Test]
    [Description("A BreakDuration below the 500 ms floor required by the resilience handler fails validation.")]
    public void Resilience_BreakDuration_BelowFiveHundredMilliseconds_Fails()
    {
        var result = new InertiaSsrOptionsValidator().Validate(null,
            Ssr(o => o.SsrResilience.CircuitBreakerBreakDuration = TimeSpan.FromMilliseconds(300)));

        Assert.That(result.Failed, Is.True);
        Assert.That(result.Failures!, Has.Some.Contains(nameof(InertiaSsrResilienceOptions.CircuitBreakerBreakDuration)));
    }

    [Test]
    [Description("A BreakDuration of exactly 500 ms is accepted.")]
    public void Resilience_BreakDuration_AtFloor_IsValid()
    {
        var result = new InertiaSsrOptionsValidator().Validate(null,
            Ssr(o => o.SsrResilience.CircuitBreakerBreakDuration = TimeSpan.FromMilliseconds(500)));

        Assert.That(result.Succeeded, Is.True);
    }

    [Test]
    [Description("A non-positive SsrTimeout fails validation when SSR is enabled.")]
    public void Resilience_SsrTimeout_NonPositive_Fails()
    {
        var result = new InertiaSsrOptionsValidator().Validate(null,
            Ssr(o => o.SsrTimeout = TimeSpan.Zero));

        Assert.That(result.Failed, Is.True);
        Assert.That(result.Failures!, Has.Some.Contains(nameof(InertiaOptions.SsrTimeout)));
    }

    [TestCase("not-a-url")]
    [TestCase("file:///etc/passwd")]
    [Description("An SsrHealthUrl that is not an absolute http/https URL fails validation.")]
    public void Resilience_HealthUrl_Invalid_Fails(string healthUrl)
    {
        var result = new InertiaSsrOptionsValidator().Validate(null,
            Ssr(o => o.SsrHealthUrl = healthUrl));

        Assert.That(result.Failed, Is.True);
        Assert.That(result.Failures!, Has.Some.Contains(nameof(InertiaOptions.SsrHealthUrl)));
    }

    [Test]
    [Description("A valid absolute SsrHealthUrl is accepted.")]
    public void Resilience_HealthUrl_Valid_IsAccepted()
    {
        var result = new InertiaSsrOptionsValidator().Validate(null,
            Ssr(o => o.SsrHealthUrl = "https://ssr.internal:8443/health"));

        Assert.That(result.Succeeded, Is.True);
    }

    [Test]
    [Description("An invalid SsrUrl without an explicit SsrHealthUrl fails validation (the health URL is derived from it).")]
    public void Resilience_SsrUrl_Invalid_WithoutHealthUrl_Fails()
    {
        var result = new InertiaSsrOptionsValidator().Validate(null,
            Ssr(o => o.SsrUrl = "file:///render"));

        Assert.That(result.Failed, Is.True);
        Assert.That(result.Failures!, Has.Some.Contains(nameof(InertiaOptions.SsrUrl)));
    }

    [Test]
    [Description("Managed-process and resilience failures are reported together.")]
    public void Resilience_AndManaged_Failures_AreAggregated()
    {
        var result = new InertiaSsrOptionsValidator().Validate(null,
            Ssr(o =>
            {
                o.Ssr.Managed = true;
                o.Ssr.NodePath = "";
                o.SsrResilience.MaxRetryAttempts = -1;
            }));

        Assert.That(result.Failures!, Has.Count.GreaterThanOrEqualTo(2));
    }
}
