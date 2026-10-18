using System.ComponentModel.DataAnnotations;
using InertiaCore.Validation;
using Microsoft.AspNetCore.Http;

namespace InertiaCoreTests;

[TestFixture]
public class UnitTestDataAnnotationsNestedKeys
{
    private sealed class Address
    {
        [Required]
        public string? Street { get; set; }
    }

    private sealed class LineItem
    {
        [Required]
        public string? ProductName { get; set; }

        [System.ComponentModel.DataAnnotations.Range(1, 100)]
        public int Quantity { get; set; } = 1;
    }

    private sealed class Order
    {
        [Required]
        public string? CustomerName { get; set; }

        public Address? ShippingAddress { get; set; }

        public List<LineItem>? LineItems { get; set; }
    }

    private sealed class Node
    {
        [Required]
        public string? Label { get; set; }

        public Node? Child { get; set; }
    }

    private sealed class Strict
    {
        [Required]
        public Address? Address { get; set; }
    }

    private sealed class Unannotated
    {
        public string? Anything { get; set; }
    }

    private static async Task<IReadOnlyDictionary<string, string[]>> Validate(params object?[] arguments) =>
        await new DataAnnotationsRequestValidator().ValidateAsync(new DefaultHttpContext(), arguments.ToList());

    [Test]
    [Description("SC: a nested object error uses a camelCase dotted path.")]
    public async Task NestedObject_UsesDottedPath()
    {
        var errors = await Validate(new Order
        {
            CustomerName = "Ada",
            ShippingAddress = new Address { Street = null }
        });

        Assert.Multiple(() =>
        {
            Assert.That(errors.Keys, Is.EquivalentTo(new[] { "shippingAddress.street" }));
            Assert.That(errors["shippingAddress.street"], Has.Length.EqualTo(1));
        });
    }

    [Test]
    [Description("SC: collection element errors use numeric indexes, e.g. lineItems.0.productName.")]
    public async Task CollectionElements_UseNumericIndexes()
    {
        var errors = await Validate(new Order
        {
            CustomerName = "Ada",
            LineItems = new List<LineItem>
            {
                new() { ProductName = "Widget", Quantity = 1 },
                new() { ProductName = null, Quantity = 500 }
            }
        });

        Assert.Multiple(() =>
        {
            Assert.That(errors.Keys, Is.EquivalentTo(new[] { "lineItems.1.productName", "lineItems.1.quantity" }));
        });
    }

    [Test]
    [Description("SC: a valid nested graph produces no errors.")]
    public async Task ValidGraph_ProducesNoErrors()
    {
        var errors = await Validate(new Order
        {
            CustomerName = "Ada",
            ShippingAddress = new Address { Street = "1 Main" },
            LineItems = new List<LineItem> { new() { ProductName = "Widget", Quantity = 2 } }
        });

        Assert.That(errors, Is.Empty);
    }

    [Test]
    [Description("SC: a failing property's own attributes stop descent into that property.")]
    public async Task FailedProperty_DoesNotDescend()
    {
        var errors = await Validate(new Strict { Address = null });

        Assert.That(errors.Keys, Is.EquivalentTo(new[] { "address" }));
    }

    [Test]
    [Description("SC: an object cycle terminates and does not overflow the stack.")]
    public async Task Cycle_Terminates()
    {
        var a = new Node { Label = "a" };
        var b = new Node { Label = null, Child = a };
        a.Child = b;

        var errors = await Validate(a);

        Assert.That(errors.Keys, Is.EquivalentTo(new[] { "child.label" }));
    }

    [Test]
    [Description("SC: arguments with no DataAnnotations metadata are a no-op.")]
    public async Task Unannotated_IsNoOp()
    {
        var errors = await Validate(new Unannotated { Anything = null }, "text", 42, null);

        Assert.That(errors, Is.Empty);
    }
}
