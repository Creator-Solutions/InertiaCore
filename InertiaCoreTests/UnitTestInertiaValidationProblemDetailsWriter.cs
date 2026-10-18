using System.Net;
using InertiaCore.Services;
using InertiaCore.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace InertiaCoreTests;

[TestFixture]
public class UnitTestInertiaValidationProblemDetailsWriter
{
    private static Endpoint Endpoint(bool optedIn)
    {
        var metadata = optedIn
            ? new EndpointMetadataCollection(new InertiaValidationMetadata())
            : EndpointMetadataCollection.Empty;

        return new Endpoint(null, metadata, "test");
    }

    private static HttpValidationProblemDetails Problem(params (string Key, string Message)[] errors)
    {
        var dictionary = errors
            .GroupBy(e => e.Key)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray());

        return new HttpValidationProblemDetails(dictionary);
    }

    private static (DefaultHttpContext Http, ErrorBagService Bag) Create(bool inertia, bool optedIn, IInertiaFlashProvider? flash = null, string? bag = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var bagService = new ErrorBagService();
        if (bag is not null)
            bagService.CurrentBagName = bag;
        services.AddSingleton<IErrorBagService>(bagService);
        if (flash is not null)
            services.AddSingleton(flash);

        var http = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        if (inertia)
            http.Request.Headers["X-Inertia"] = "true";
        http.Request.Headers["Referer"] = "/form";
        http.SetEndpoint(Endpoint(optedIn));

        return (http, bagService);
    }

    [Test]
    [Description("SC: the writer only handles validation problems on endpoints that opted in.")]
    public void CanWrite_OnlyOptedInValidationProblems()
    {
        var writer = new InertiaValidationProblemDetailsWriter();

        var (optedIn, _) = Create(inertia: true, optedIn: true);
        var (plain, _) = Create(inertia: true, optedIn: false);

        Assert.Multiple(() =>
        {
            Assert.That(writer.CanWrite(new ProblemDetailsContext { HttpContext = optedIn, ProblemDetails = Problem(("name", "Required")) }), Is.True);
            Assert.That(writer.CanWrite(new ProblemDetailsContext { HttpContext = plain, ProblemDetails = Problem(("name", "Required")) }), Is.False);
            Assert.That(writer.CanWrite(new ProblemDetailsContext { HttpContext = optedIn, ProblemDetails = new ProblemDetails { Status = 500 } }), Is.False);
        });
    }

    [Test]
    [Description("SC: an Inertia validation failure on an opted-in endpoint is written as a 303 with the errors in the bag and flashed.")]
    public async Task InertiaRequest_WritesErrors_AndRedirects303()
    {
        var flash = new Mock<IInertiaFlashProvider>();
        var (http, bag) = Create(inertia: true, optedIn: true, flash: flash.Object);
        var writer = new InertiaValidationProblemDetailsWriter();

        await writer.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = Problem(("Items[0].Name", "Required"), ("Address.Street", "Required"))
        });

        Assert.Multiple(() =>
        {
            Assert.That(http.Response.StatusCode, Is.EqualTo((int)HttpStatusCode.SeeOther));
            Assert.That(http.Response.Headers.Location.ToString(), Is.EqualTo("/form"));
            Assert.That(bag.GetErrors(ErrorBagService.DefaultBagName)!.Keys,
                Is.EquivalentTo(new[] { "items.0.name", "address.street" }));
            flash.Verify(f => f.Flash(ValidationErrorFlash.Key, It.IsAny<object?>()), Times.Once);
        });
    }

    [Test]
    [Description("SC: an Inertia validation failure on an opted-in endpoint without a flash provider is a 422 body.")]
    public async Task InertiaRequest_WithoutFlashProvider_Returns422()
    {
        var (http, bag) = Create(inertia: true, optedIn: true);
        var writer = new InertiaValidationProblemDetailsWriter();

        await writer.WriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = Problem(("name", "Required")) });

        Assert.Multiple(() =>
        {
            Assert.That(http.Response.StatusCode, Is.EqualTo((int)HttpStatusCode.UnprocessableEntity));
            Assert.That(bag.GetErrors(ErrorBagService.DefaultBagName), Is.Not.Null);
        });
    }

    [Test]
    [Description("SC: errors are written to the bag named by X-Inertia-Error-Bag.")]
    public async Task NamedBag_IsUsed()
    {
        var flash = new Mock<IInertiaFlashProvider>();
        var (http, bag) = Create(inertia: true, optedIn: true, flash: flash.Object, bag: "settings");
        var writer = new InertiaValidationProblemDetailsWriter();

        await writer.WriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = Problem(("name", "Required")) });

        Assert.That(bag.GetErrors("settings"), Is.Not.Null);
    }

    [Test]
    [Description("SC: framework keys are normalized to camelCase segments with numeric indexes.")]
    [TestCase("Customer.Name", "customer.name")]
    [TestCase("Items[0].Name", "items.0.name")]
    [TestCase("LineItems.12.ProductName", "lineItems.12.productName")]
    [TestCase("name", "name")]
    public void NormalizeKey_UsesDocumentedFormat(string input, string expected)
    {
        Assert.That(InertiaValidationResponse.NormalizeKey(input), Is.EqualTo(expected));
    }
}
