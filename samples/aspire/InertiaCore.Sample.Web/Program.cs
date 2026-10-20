using InertiaCore.Contracts;
using InertiaCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Bind InertiaOptions and ViteOptions from configuration so the Aspire AppHost can
// supply the SSR gateway URL and the Vite dev server URL through environment variables
// (Inertia__SsrUrl, Inertia__SsrEnabled, Vite__HotUrl).
builder.Services.AddInertia(builder.Configuration);
builder.Services.AddViteHelper(builder.Configuration);
builder.Services.AddControllersWithViews();

var app = builder.Build();

app.UseInertia();
app.UseStaticFiles();

app.MapGet("/", async (IInertia inertia) =>
    (IResult)await inertia.Render("Home", new { message = "Hello from InertiaCore and .NET Aspire" }));

app.Run();

// Exposed for integration tests that spin the app up in-process.
public partial class Program;
