using System.Text.Json;
using System.Text.Json.Serialization;
using InertiaCore;
using InertiaCore.Contracts;
using InertiaCore.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInertia(options =>
{
    options.JsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        TypeInfoResolver = AotJsonContext.Default
    };
});

var app = builder.Build();
app.UseInertia();

app.MapGet("/", async (IInertia inertia) =>
    (IResult)await inertia.Render(new HomeProps
    {
        Title = "Hello AOT",
        Count = 42
    }));

app.Run();

[InertiaPage("Home")]
public class HomeProps
{
    public string Title { get; set; } = string.Empty;
    public int Count { get; set; }
}

[JsonSerializable(typeof(HomeProps))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
public partial class AotJsonContext : JsonSerializerContext
{
}