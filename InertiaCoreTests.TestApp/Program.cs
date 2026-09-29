using InertiaCore.Contracts;
using InertiaCore.Extensions;
using InertiaCore.Props;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInertia();

var app = builder.Build();
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

app.Run();

public partial class Program
{
}
