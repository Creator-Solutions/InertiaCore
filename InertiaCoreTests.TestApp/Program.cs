using System.ComponentModel.DataAnnotations;
using InertiaCore.Contracts;
using InertiaCore.Extensions;
using InertiaCore.Props;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInertia();
builder.Services.AddControllersWithViews();

var app = builder.Build();
app.UseRouting();
app.UseInertia();

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

app.MapGet("/protocol/target", async (IInertia inertia) =>
    (IResult)await inertia.Render("Test/Page", new { ok = true }));

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
