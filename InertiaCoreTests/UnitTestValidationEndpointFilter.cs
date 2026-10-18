using System.ComponentModel.DataAnnotations;
using System.Net;
using InertiaCore.Filters;
using InertiaCore.Services;
using InertiaCore.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace InertiaCoreTests;

[TestFixture]
public class UnitTestValidationEndpointFilter
{
    private sealed class Model
    {
        [Required]
        public string? Name { get; set; }
    }

    private sealed class StubInvocationContext : EndpointFilterInvocationContext
    {
        public StubInvocationContext(HttpContext httpContext, params object?[] arguments)
        {
            HttpContext = httpContext;
            Arguments = arguments.ToList();
        }

        public override HttpContext HttpContext { get; }

        public override IList<object?> Arguments { get; }

        public override T GetArgument<T>(int index) => (T)Arguments[index]!;
    }

    private sealed class Probe
    {
        public int Runs;
    }

    private static (DefaultHttpContext Http, ErrorBagService Bag) Create(
        bool inertia,
        string? errorBag = null,
        IInertiaFlashProvider? flashProvider = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (flashProvider is not null)
            services.AddSingleton(flashProvider);

        var http = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        if (inertia)
            http.Request.Headers["X-Inertia"] = "true";
        http.Request.Headers["Referer"] = "/form";

        var bag = new ErrorBagService();
        if (errorBag is not null)
            bag.CurrentBagName = errorBag;

        return (http, bag);
    }

    private static async Task<(object? Result, int Runs)> Invoke(
        DefaultHttpContext http,
        ErrorBagService bag,
        object? argument)
    {
        var probe = new Probe();
        var filter = new InertiaValidationEndpointFilter(bag, new DataAnnotationsRequestValidator());
        var context = new StubInvocationContext(http, argument);

        var result = await filter.InvokeAsync(context, _ =>
        {
            probe.Runs++;
            return ValueTask.FromResult<object?>(Results.Text("ok"));
        });

        return (result, probe.Runs);
    }

    private static async Task<int> StatusOf(object? result, DefaultHttpContext http)
    {
        Assert.That(result, Is.InstanceOf<IResult>());
        var ir = (IResult)result!;
        await ir.ExecuteAsync(http);
        return http.Response.StatusCode;
    }

    [Test]
    [Description("SC: a valid argument calls the next delegate and returns its result untouched.")]
    public async Task Valid_CallsNext()
    {
        var (http, bag) = Create(inertia: false);

        var (result, runs) = await Invoke(http, bag, new Model { Name = "ok" });

        Assert.Multiple(() =>
        {
            Assert.That(runs, Is.EqualTo(1));
            Assert.That(bag.GetErrors(ErrorBagService.DefaultBagName), Is.Null);
            Assert.That(result, Is.InstanceOf<IResult>());
        });
    }

    [Test]
    [Description("SC: a non-Inertia invalid request returns a 422 validation problem and does not call next.")]
    public async Task NonInertiaInvalid_Returns422()
    {
        var (http, bag) = Create(inertia: false);

        var (result, runs) = await Invoke(http, bag, new Model());
        var status = await StatusOf(result, http);

        Assert.Multiple(() =>
        {
            Assert.That(runs, Is.EqualTo(0));
            Assert.That(status, Is.EqualTo(StatusCodes.Status422UnprocessableEntity));
        });
    }

    [Test]
    [Description("SC: an Inertia invalid request writes errors to the default bag and redirects 303 with flashed errors.")]
    public async Task InertiaInvalid_WithFlashProvider_Returns303_AndFlashes()
    {
        var flash = new Mock<IInertiaFlashProvider>();
        var (http, bag) = Create(inertia: true, flashProvider: flash.Object);

        var (result, runs) = await Invoke(http, bag, new Model());
        var status = await StatusOf(result, http);

        Assert.Multiple(() =>
        {
            Assert.That(runs, Is.EqualTo(0));
            Assert.That(bag.GetErrors(ErrorBagService.DefaultBagName)!["name"], Has.Length.EqualTo(1));
            Assert.That(status, Is.EqualTo(StatusCodes.Status303SeeOther));
            Assert.That(http.Response.Headers.Location.ToString(), Is.EqualTo("/form"));
            flash.Verify(f => f.Flash(ValidationErrorFlash.Key, It.IsAny<object?>()), Times.Once);
        });
    }

    [Test]
    [Description("SC: an Inertia invalid request without a flash provider falls back to a 422 validation problem.")]
    public async Task InertiaInvalid_WithoutFlashProvider_Returns422()
    {
        var (http, bag) = Create(inertia: true);

        var (result, _) = await Invoke(http, bag, new Model());
        var status = await StatusOf(result, http);

        Assert.Multiple(() =>
        {
            Assert.That(bag.GetErrors(ErrorBagService.DefaultBagName), Is.Not.Null);
            Assert.That(status, Is.EqualTo(StatusCodes.Status422UnprocessableEntity));
        });
    }

    [Test]
    [Description("SC: errors are written to the named bag from X-Inertia-Error-Bag, not the default bag.")]
    public async Task NamedBag_WritesToNamedBag()
    {
        var flash = new Mock<IInertiaFlashProvider>();
        var (http, bag) = Create(inertia: true, errorBag: "settings", flashProvider: flash.Object);

        await Invoke(http, bag, new Model());

        Assert.Multiple(() =>
        {
            Assert.That(bag.GetErrors("settings"), Is.Not.Null);
            Assert.That(bag.GetErrors(ErrorBagService.DefaultBagName), Is.Null);
        });
    }

    [Test]
    [Description("SC: a model with no validation attributes is a no-op.")]
    public async Task NoValidationAttributes_IsNoOp()
    {
        var (http, bag) = Create(inertia: true);
        var plain = new object();

        var (_, runs) = await Invoke(http, bag, plain);

        Assert.That(runs, Is.EqualTo(1));
    }

    [Test]
    [Description("SC: the flash payload round-trips the bag name and errors.")]
    public void FlashPayload_RoundTrips()
    {
        var flash = new Mock<IInertiaFlashProvider>();
        object? stored = null;
        flash.Setup(f => f.Flash(ValidationErrorFlash.Key, It.IsAny<object?>()))
            .Callback<string, object?>((_, value) => stored = value);

        ValidationErrorFlash.Store(
            flash.Object,
            "settings",
            new Dictionary<string, string[]> { ["address.street"] = new[] { "Required" } });

        var ok = ValidationErrorFlash.TryRead(stored, out var bagName, out var errors);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(bagName, Is.EqualTo("settings"));
            Assert.That(errors["address.street"], Is.EqualTo(new[] { "Required" }));
        });
    }

    [Test]
    [Description("SC: a malformed or missing flash value is ignored rather than restored.")]
    public void FlashPayload_Malformed_IsIgnored()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorFlash.TryRead(null, out _, out _), Is.False);
            Assert.That(ValidationErrorFlash.TryRead("{not json", out _, out _), Is.False);
            Assert.That(ValidationErrorFlash.TryRead(42, out _, out _), Is.False);
        });
    }
}
