using InertiaCore.Contracts;
using InertiaCore.Extensions;

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

app.Run();

public partial class Program
{
}
