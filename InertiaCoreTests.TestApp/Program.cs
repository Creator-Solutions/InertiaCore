using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FluentValidation;
using InertiaCore;
using InertiaCore.FluentValidation;
using InertiaCore.Contracts;
using InertiaCore.Extensions;
using InertiaCore.Props;
using InertiaCoreTests.TestApp;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInertia(options =>
{
    // Opt-in anti-forgery bridge for the IN-64 integration tests.
    options.Antiforgery.Enabled = builder.Configuration.GetValue<bool>("UseAntiforgery");

    // Opt-in status pages for the IN-65 integration tests.
    options.StatusPages.Enabled = builder.Configuration.GetValue<bool>("UseStatusPages");
});
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<IValidator<FluentOrder>, FluentOrderValidator>();

// Opt-in cookie authentication for the IN-65 authorization result handler tests.
if (builder.Configuration.GetValue<bool>("UseAuth"))
{
    builder.Services
        .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.LoginPath = "/auth/login";
            options.AccessDeniedPath = "/auth/denied";

            // Emit a bare 403 for a forbidden principal so the status-pages middleware can
            // render the error component, rather than redirecting to the access-denied path.
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });
}

// Opt-in for the large-flash integration tests: a real distributed cache backs flash.
if (builder.Configuration.GetValue<bool>("UseDistributedFlash"))
{
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddInertiaDistributedCacheFlash();
}

#if NET10_0_OR_GREATER
// Opt-in so the default host keeps its existing behaviour. The built-in validation
// integration tests set this flag.
if (builder.Configuration.GetValue<bool>("UseBuiltInValidation"))
{
    builder.Services.AddValidation();
}
#endif

var app = builder.Build();
app.UseRouting();

app.UseInertia();

// Inertia error pages (IN-65). Placed after routing so the endpoint is known, and before
// authentication/authorization so a short-circuiting 401/403 is still observed.
app.UseInertiaStatusPages();

if (builder.Configuration.GetValue<bool>("UseAuth"))
{
    app.UseAuthentication();
    app.UseAuthorization();
}

// Anti-forgery bridge (IN-64). Placed after routing/auth in a real app.
app.UseInertiaAntiforgery();

app.MapGet("/merge", async (IInertia inertia) =>
    (IResult)await inertia.Render("Test/Page", new
    {
        Posts = inertia.Merge(new[] { 1, 2, 3 }),
        Other = "other"
    }));

app.MapGet("/scroll", async (IInertia inertia) =>
    (IResult)await inertia.Render("Test/Page", new
    {
        Posts = inertia.Scroll(
            new { data = new[] { 1, 2, 3 } },
            new ScrollMetadata("page", 1, 3, 2))
    }));

app.MapGet("/deferred", async (IInertia inertia) =>
    (IResult)await inertia.Render("Test/Page", new
    {
        Permissions = inertia.Defer(() => (object?)"permissions"),
        Teams = inertia.Defer(() => (object?)"teams", "attributes"),
        Projects = inertia.Defer(() => (object?)"projects", "attributes")
    }));

app.MapGet("/once", async (IInertia inertia) =>
    (IResult)await inertia.Render("Test/Page", new
    {
        Plans = inertia.Once(() => (object?)new[] { 1, 2 })
    }));

app.MapGet("/rescue", async (IInertia inertia) =>
    (IResult)await inertia.Render("Test/Page", new
    {
        Permissions = inertia.Defer(() => throw new InvalidOperationException("boom"), "default", true),
        Other = "ok"
    }));

app.MapPost("/precognition", (PrecognitionInput input) =>
{
    PrecognitionProbe.HandlerRuns++;
    // Text (not JSON) so the test host does not exercise the framework JSON pipe
    // path, which is unrelated to Precognition.
    return Results.Text("ok");
}).WithInertiaPrecognition();

// Minimal API validation (IN-61). The submitted model has nested and collection members
// so the error key format (dotted paths and numeric indexes) is exercised end to end.
app.MapPost("/validation", (ValidationInput input) =>
{
    ValidationProbe.HandlerRuns++;
    return Results.Text("ok");
}).WithInertiaValidation();

var validationGroup = app.MapGroup("/validation-group").WithInertiaValidation();
validationGroup.MapPost("/submit", (ValidationInput input) =>
{
    ValidationProbe.HandlerRuns++;
    return Results.Text("ok");
});

// Precognition and validation on the same endpoint, in both filter orders. A precognitive
// request must be answered by Precognition (204/422) whichever filter is added first.
app.MapPost("/precognition-validation", (ValidationInput input) =>
{
    ValidationProbe.HandlerRuns++;
    return Results.Text("ok");
}).WithInertiaPrecognition().WithInertiaValidation();

app.MapPost("/precognition-validation-reversed", (ValidationInput input) =>
{
    ValidationProbe.HandlerRuns++;
    return Results.Text("ok");
}).WithInertiaValidation().WithInertiaPrecognition();

// FluentValidation (IN-62). The validator is registered below.
app.MapPost("/fluent", (FluentOrder order) =>
{
    FluentProbe.HandlerRuns++;
    return Results.Text("ok");
}).WithInertiaFluentValidation<FluentOrder>();

app.MapPost("/fluent-strict", (FluentOrder order) =>
{
    FluentProbe.HandlerRuns++;
    return Results.Text("ok");
}).WithInertiaFluentValidation<FluentOrder>(new[] { "strict" });

app.MapPost("/fluent-precognition", (FluentOrder order) =>
{
    FluentProbe.HandlerRuns++;
    return Results.Text("ok");
}).WithInertiaFluentValidation<FluentOrder>();

var fluentGroup = app.MapGroup("/fluent-group").WithInertiaFluentValidation<FluentOrder>();
fluentGroup.MapPost("/submit", (FluentOrder order) =>
{
    FluentProbe.HandlerRuns++;
    return Results.Text("ok");
});

// Only mapped when the test host asks for it: the endpoint has no validator, so building it
// is expected to fail with the missing-validator message.
if (builder.Configuration.GetValue<bool>("MissingFluentValidator"))
{
    app.MapPost("/fluent-missing", (FluentUnregistered input) => Results.Text("ok"))
        .WithInertiaFluentValidation<FluentUnregistered>();
}

// Endpoint without validation attributes: the filter must be a no-op.
app.MapPost("/validation-plain", (PlainValidationInput input) =>
{
    ValidationProbe.HandlerRuns++;
    return Results.Text("ok");
}).WithInertiaValidation();

// Renders the page that a 303 redirect lands on, so flashed errors can be read back.
app.MapGet("/validation/page", async (IInertia inertia) =>
    (IResult)await inertia.Render("Test/Page", new { ok = true }));

// Flash (IN-63): a POST flashes a value and redirects; the page render exposes it once.
app.MapPost("/flash", (HttpContext context) =>
{
    Inertia.Flash("success", "Saved");
    context.Response.Headers.Location = "/flash/page";
    return Results.StatusCode(StatusCodes.Status303SeeOther);
});

app.MapPost("/flash-service", (HttpContext context, IInertia inertia) =>
{
    inertia.Flash("notice", "From IInertia");
    context.Response.Headers.Location = "/flash/page";
    return Results.StatusCode(StatusCodes.Status303SeeOther);
});

app.MapPost("/flash-large", (HttpContext context) =>
{
    Inertia.Flash("payload", new string('x', 8000));
    context.Response.Headers.Location = "/flash/page";
    return Results.StatusCode(StatusCodes.Status303SeeOther);
});

app.MapGet("/flash/page", async (IInertia inertia) =>
    (IResult)await inertia.Render("Test/Page", new { ok = true }));

// Anti-forgery bridge (IN-64): a Minimal API endpoint that validates the request token, plus
// a page render that issues the token cookie.
app.MapPost("/csrf", () =>
{
    AntiForgeryProbe.HandlerRuns++;
    return Results.Text("ok");
}).WithInertiaAntiforgery();

app.MapGet("/csrf/page", async (IInertia inertia) =>
    (IResult)await inertia.Render("Test/Page", new { ok = true }));

// Status-page probes (IN-65): endpoints that return a bare status with no body, so the
// status-pages middleware can render the configured error component.
app.MapGet("/status/403", () => Results.StatusCode(StatusCodes.Status403Forbidden));
app.MapGet("/status/404", () => Results.StatusCode(StatusCodes.Status404NotFound));
app.MapGet("/status/419", () => Results.StatusCode(419));
app.MapGet("/status/500", () => Results.StatusCode(StatusCodes.Status500InternalServerError));
app.MapGet("/status/503", () => Results.StatusCode(StatusCodes.Status503ServiceUnavailable));
app.MapGet("/status/page", async (IInertia inertia) =>
    (IResult)await inertia.Render("Test/Page", new { ok = true }));

app.MapGet("/protocol/target", async (IInertia inertia) =>
    (IResult)await inertia.Render("Test/Page", new { ok = true }));

app.MapGet("/typed", async (IInertia inertia) =>
    (IResult)await inertia.Render(new UsersIndexProps(new[] { "Ada", "Grace" }, 1)));

app.MapGet("/typed-sample", async (IInertia inertia) =>
{
    inertia.Share("appName", "InertiaCore TestApp");
    inertia.Share("flash", new[] { "welcome" });

    var sample = new TypedSampleProps(
        new[]
        {
            new TypedSampleUser(
                Guid.Empty,
                "Ada",
                DateTime.UnixEpoch,
                UserRole.Admin,
                UserStatus.Active,
                null,
                new[] { "founder" },
                new Address("1 Analytical Engine Way", null),
                new Dictionary<string, int> { ["logic"] = 10 })
        },
        null,
        new[] { "label" },
        2,
        new[] { new TreeNode("root", Array.Empty<TreeNode>()) });

    return (IResult)await inertia.Render(sample);
});

// Named Minimal API endpoints used by the IN-57 route-metadata sample and tests.
// These mirror a small Ziggy-style API surface consumed by routes.ts.
app.MapGet("/api/users", () => Results.Ok(new[] { "Ada", "Grace" }))
    .WithName("users.index");

app.MapGet("/api/users/{id:int}", (int id) => Results.Ok(new { id }))
    .WithName("users.show");

app.MapPut("/api/users/{id:int}", (int id) => Results.Ok(new { id }))
    .WithName("users.update");

app.MapGet("/api/users/{user:int}/posts/{post:int}", (int user, int post) => Results.Ok(new { user, post }))
    .WithName("posts.show");

app.MapGet("/api/search/{*rest}", (string? rest) => Results.Ok(new { rest }))
    .WithName("search");

// Development-only route dump consumed by `inertiatsgen routes`.
app.MapInertiaRoutes();

app.MapControllers();

app.Run();

public partial class Program
{
}

/// <summary>Model used by the Precognition Minimal API integration tests.</summary>
public sealed class PrecognitionInput
{
    [Required]
    public string? Name { get; set; }

    [Range(1, 120)]
    public int Age { get; set; }

    [EmailAddress]
    public string? Email { get; set; }
}

/// <summary>Tracks how often the precognition test endpoint handler executed.</summary>
public static class PrecognitionProbe
{
    public static int HandlerRuns;
}

/// <summary>Model used by the Minimal API validation integration tests.</summary>
public sealed class ValidationInput
{
    [Required]
    public string? Name { get; set; }

    [Required]
    public ValidationAddress? Address { get; set; }

    public List<ValidationLineItem>? Items { get; set; }
}

/// <summary>Nested object used by <see cref="ValidationInput"/>.</summary>
public sealed class ValidationAddress
{
    [Required]
    public string? Street { get; set; }
}

/// <summary>Collection element used by <see cref="ValidationInput"/>.</summary>
public sealed class ValidationLineItem
{
    [Required]
    public string? Name { get; set; }

    [Range(1, 100)]
    public int Quantity { get; set; }
}

/// <summary>Model with no validation attributes, for the no-op case.</summary>
public sealed class PlainValidationInput
{
    public string? Name { get; set; }
}

/// <summary>Tracks how often a validation test endpoint handler executed.</summary>
public static class ValidationProbe
{
    public static int HandlerRuns;
}

/// <summary>Model used by the FluentValidation integration tests.</summary>
public sealed class FluentOrder
{
    public string? CustomerName { get; set; }

    public FluentAddress? Address { get; set; }

    public string? Notes { get; set; }
}

/// <summary>Nested address used by <see cref="FluentOrder"/>.</summary>
public sealed class FluentAddress
{
    public string? PostalCode { get; set; }
}

/// <summary>A type with no validator registered, for the missing-registration test.</summary>
public sealed class FluentUnregistered
{
    public string? Name { get; set; }
}

/// <summary>Tracks how often a FluentValidation test endpoint handler executed.</summary>
public static class FluentProbe
{
    public static int HandlerRuns;
}

/// <summary>Validator for <see cref="FluentOrder"/>. Notes is required only in the "strict" rule set.</summary>
public sealed class FluentOrderValidator : AbstractValidator<FluentOrder>
{
    public FluentOrderValidator()
    {
        RuleFor(x => x.CustomerName).NotEmpty();
        RuleFor(x => x.Address).NotNull();
        RuleFor(x => x.Address!.PostalCode).NotEmpty().When(x => x.Address is not null);

        RuleSet("strict", () =>
        {
            RuleFor(x => x.Notes).NotEmpty();
        });
    }
}
