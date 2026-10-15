using System.ComponentModel.DataAnnotations;
using InertiaCore.Validation;
using Microsoft.AspNetCore.Http;

namespace InertiaCoreTests;

[TestFixture]
public class UnitTestDataAnnotationsRequestValidator
{
    private sealed class SampleModel
    {
        [Required]
        public string? Name { get; set; }

        [System.ComponentModel.DataAnnotations.Range(1, 120)]
        public int Age { get; set; }
    }

    private static Task<IReadOnlyDictionary<string, string[]>> ValidateAsync(params object?[] arguments) =>
        new DataAnnotationsRequestValidator().ValidateAsync(new DefaultHttpContext(), arguments.ToList());

    [Test]
    [Description("A valid model produces no errors.")]
    public async Task Valid_ReturnsNoErrors()
    {
        var errors = await ValidateAsync(new SampleModel { Name = "Jane", Age = 30 });

        Assert.That(errors, Is.Empty);
    }

    [Test]
    [Description("Invalid members are keyed by camelCased property name.")]
    public async Task Invalid_ReturnsCamelCasedKeys()
    {
        var errors = await ValidateAsync(new SampleModel { Name = null, Age = 200 });

        Assert.Multiple(() =>
        {
            Assert.That(errors, Does.ContainKey("name"));
            Assert.That(errors, Does.ContainKey("age"));
            Assert.That(errors["name"], Is.Not.Empty);
        });
    }

    [Test]
    [Description("Null and simple arguments are ignored rather than treated as models.")]
    public async Task SimpleAndNull_AreIgnored()
    {
        var errors = await ValidateAsync(null, "text", 42, true, Guid.Empty);

        Assert.That(errors, Is.Empty);
    }

    [Test]
    [Description("Validation is applied to each model argument and merged.")]
    public async Task MultipleModels_AreMerged()
    {
        var errors = await ValidateAsync(
            new SampleModel { Name = null, Age = 1 },
            new SampleModel { Name = "ok", Age = 999 });

        Assert.Multiple(() =>
        {
            Assert.That(errors, Does.ContainKey("name"));
            Assert.That(errors, Does.ContainKey("age"));
        });
    }
}
