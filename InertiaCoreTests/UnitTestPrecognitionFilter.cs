using InertiaCore.Filters;
using InertiaCore.Precognition;
using InertiaCore.Services;
using InertiaCore.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;

namespace InertiaCoreTests;

[TestFixture]
public class UnitTestPrecognitionFilter
{
    private static ActionExecutingContext CreateContext(
        bool precognition,
        bool invalid,
        string? validateOnly = null)
    {
        var httpContext = new DefaultHttpContext();

        if (precognition)
            httpContext.Request.Headers[InertiaHeader.Precognition] = "true";

        if (validateOnly != null)
            httpContext.Request.Headers[InertiaHeader.PrecognitionValidateOnly] = validateOnly;

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor(),
            new ModelStateDictionary());

        if (invalid)
            actionContext.ModelState.AddModelError("Name", "The Name field is required.");

        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller: null!);
    }

    [Test]
    [Description("A valid precognitive request sets a 204 result with Precognition-Success.")]
    public void Valid_Produces204()
    {
        var filter = new InertiaPrecognitionFilter(new ErrorBagService());
        var context = CreateContext(precognition: true, invalid: false);

        filter.OnActionExecuting(context);

        Assert.Multiple(() =>
        {
            Assert.That(context.Result, Is.TypeOf<StatusCodeResult>());
            Assert.That(((StatusCodeResult)context.Result!).StatusCode, Is.EqualTo(204));
            Assert.That(context.HttpContext.Response.Headers[InertiaHeader.Precognition].ToString(), Is.EqualTo("true"));
            Assert.That(context.HttpContext.Response.Headers[InertiaHeader.PrecognitionSuccess].ToString(), Is.EqualTo("true"));
            Assert.That(context.HttpContext.Response.Headers["Vary"].ToString(), Is.EqualTo("Precognition"));
        });
    }

    [Test]
    [Description("An invalid precognitive request sets a 422 result containing the errors.")]
    public void Invalid_Produces422WithErrors()
    {
        var filter = new InertiaPrecognitionFilter(new ErrorBagService());
        var context = CreateContext(precognition: true, invalid: true);

        filter.OnActionExecuting(context);

        Assert.That(context.Result, Is.TypeOf<PrecognitionJsonResult>());
        var result = (PrecognitionJsonResult)context.Result!;

        Assert.Multiple(() =>
        {
            Assert.That(context.HttpContext.Response.Headers[InertiaHeader.Precognition].ToString(), Is.EqualTo("true"));
            Assert.That(context.HttpContext.Response.Headers.ContainsKey(InertiaHeader.PrecognitionSuccess), Is.False);

            var body = result.Body as IDictionary<string, object?>;
            Assert.That(body, Is.Not.Null);
            var errors = body!["errors"] as IDictionary<string, object?>;
            Assert.That(errors, Is.Not.Null);
            Assert.That(errors!.ContainsKey("name"), Is.True);
        });
    }

    [Test]
    [Description("Validate-Only filters the returned errors to the requested fields.")]
    public void ValidateOnly_FiltersErrors()
    {
        var filter = new InertiaPrecognitionFilter(new ErrorBagService());
        var context = CreateContext(precognition: true, invalid: true, validateOnly: "email");

        filter.OnActionExecuting(context);

        // Only "name" is invalid, but only "email" was requested: the result is a
        // success (204) because the requested field has no errors.
        Assert.That(context.Result, Is.TypeOf<StatusCodeResult>());
        Assert.That(((StatusCodeResult)context.Result!).StatusCode, Is.EqualTo(204));
    }

    [Test]
    [Description("A named error bag nests the errors under the bag name.")]
    public void NamedBag_NestsErrors()
    {
        var bag = new ErrorBagService { CurrentBagName = "myForm" };
        var filter = new InertiaPrecognitionFilter(bag);
        var context = CreateContext(precognition: true, invalid: true);

        filter.OnActionExecuting(context);

        var result = (PrecognitionJsonResult)context.Result!;
        var body = result.Body as IDictionary<string, object?>;
        var errors = body!["errors"] as IDictionary<string, object?>;

        Assert.Multiple(() =>
        {
            Assert.That(errors, Is.Not.Null);
            Assert.That(errors!.ContainsKey("myForm"), Is.True);
        });
    }

    [Test]
    [Description("Vary is appended without clobbering an existing value.")]
    public void Vary_IsAppended()
    {
        var filter = new InertiaPrecognitionFilter(new ErrorBagService());
        var context = CreateContext(precognition: true, invalid: false);
        context.HttpContext.Response.Headers["Vary"] = "X-Inertia";

        filter.OnActionExecuting(context);

        Assert.That(context.HttpContext.Response.Headers["Vary"].ToString(), Is.EqualTo("X-Inertia, Precognition"));
    }

    [Test]
    [Description("A non-precognitive request is left untouched.")]
    public void NonPrecognition_IsUntouched()
    {
        var filter = new InertiaPrecognitionFilter(new ErrorBagService());
        var context = CreateContext(precognition: false, invalid: true);

        filter.OnActionExecuting(context);

        Assert.Multiple(() =>
        {
            Assert.That(context.Result, Is.Null);
            Assert.That(context.HttpContext.Response.Headers.ContainsKey(InertiaHeader.Precognition), Is.False);
            Assert.That(context.HttpContext.Response.Headers.ContainsKey("Vary"), Is.False);
        });
    }

    [Test]
    [Description("The filter runs before MVC's automatic 400-on-invalid-model filter.")]
    public void Order_IsBeforeModelStateInvalidFilter()
    {
        var filter = new InertiaPrecognitionFilter(new ErrorBagService());

        Assert.That(filter.Order, Is.LessThan(-2000));
    }
}
