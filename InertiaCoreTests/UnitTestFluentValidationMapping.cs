using FluentValidation;
using FluentValidation.Results;
using InertiaCore.FluentValidation;

namespace InertiaCoreTests;

/// <summary>
/// Unit tests for the key mapping and validation helpers shared by the FluentValidation
/// endpoint filter and MVC action filter.
/// </summary>
[TestFixture]
public class UnitTestFluentValidationMapping
{
    [Test]
    [Description("SC: PascalCase segments become camelCase and stay dotted for nested properties.")]
    public void ToErrors_NestedProperty_CamelCasesSegments()
    {
        var errors = FluentValidationSupport.ToErrors(new[] { new ValidationFailure("Address.PostalCode", "required") });

        Assert.That(errors.Keys, Is.EqualTo(new[] { "address.postalCode" }));
    }

    [Test]
    [Description("SC: collection indexes become numeric segments in the dotted path.")]
    public void ToErrors_IndexedProperty_UsesNumericIndex()
    {
        var errors = FluentValidationSupport.ToErrors(new[] { new ValidationFailure("Items[0].Name", "required") });

        Assert.That(errors.Keys, Is.EqualTo(new[] { "items.0.name" }));
    }

    [Test]
    [Description("SC: a failure without a property name maps to the empty key, matching the DataAnnotations path.")]
    public void ToErrors_ClassLevelFailure_UsesEmptyKey()
    {
        var errors = FluentValidationSupport.ToErrors(new[] { new ValidationFailure(null!, "invalid") });

        Assert.That(errors.Keys, Is.EqualTo(new[] { string.Empty }));
    }

    [Test]
    [Description("SC: multiple failures on one property are grouped in order.")]
    public void ToErrors_MultipleFailuresOnSameProperty_AreGrouped()
    {
        var errors = FluentValidationSupport.ToErrors(new[]
        {
            new ValidationFailure("Name", "first"),
            new ValidationFailure("Name", "second")
        });

        Assert.That(errors["name"], Is.EqualTo(new[] { "first", "second" }));
    }

    [Test]
    [Description("SC: rule sets run only when requested; the strict rule is skipped without them.")]
    public async Task ValidateAsync_WithoutRuleSets_SkipsStrictRules()
    {
        var validator = new MappingOrderValidator();

        var errors = await FluentValidationSupport.ValidateAsync(
            validator, new MappingOrder { Name = "Ada" }, ruleSets: null, CancellationToken.None);

        Assert.That(errors, Is.Empty);
    }

    [Test]
    [Description("SC: when rule sets are requested, their rules run and failures are mapped.")]
    public async Task ValidateAsync_WithRuleSets_RunsStrictRules()
    {
        var validator = new MappingOrderValidator();

        var errors = await FluentValidationSupport.ValidateAsync(
            validator, new MappingOrder { Name = "Ada" }, ruleSets: new[] { "strict" }, CancellationToken.None);

        Assert.That(errors.Keys, Is.EqualTo(new[] { "notes" }));
    }

    [Test]
    [Description("SC: a cancelled token surfaces as a cancellation, so the request can abandon validation.")]
    public void ValidateAsync_ObservesCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.CatchAsync<OperationCanceledException>(() =>
            FluentValidationSupport.ValidateAsync(
                new CancellingValidator(), new MappingOrder { Name = "Ada" }, null, cts.Token));
    }

    private sealed class MappingOrder
    {
        public string? Name { get; set; }
        public string? Notes { get; set; }
    }

    private sealed class MappingOrderValidator : AbstractValidator<MappingOrder>
    {
        public MappingOrderValidator()
        {
            RuleFor(x => x.Name).NotEmpty();
            RuleSet("strict", () => RuleFor(x => x.Notes).NotEmpty());
        }
    }

    private sealed class CancellingValidator : AbstractValidator<MappingOrder>
    {
        public CancellingValidator() =>
            RuleFor(x => x.Name).MustAsync((_, ct) => Task.FromCanceled<bool>(ct));
    }
}
