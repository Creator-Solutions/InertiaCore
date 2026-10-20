# .NET Aspire

The `InertiaCore.Aspire.Hosting` package models an Inertia application as .NET Aspire
resources. It adds the Vite dev server and the Node SSR process as separate resources so
their endpoints, health and logs appear in the Aspire dashboard, and wires the web
project to them through environment variables.

## Install

```bash
dotnet add package InertiaCore.Aspire.Hosting
```

Reference the package from the AppHost project, not the web application.

## Add the resources

```csharp
using InertiaCore.Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var vite = builder.AddInertiaViteApp("vite", "../frontend");
var ssr = builder.AddInertiaSsrApp("ssr", "../frontend");

builder.AddProject<Projects.MyApp_Web>("web")
    .WithInertiaReferences(vite, ssr);

builder.Build().Run();
```

`AddInertiaViteApp` runs the Vite dev server through its `dev` npm script and allocates an
HTTP endpoint. `AddInertiaSsrApp` runs the Node SSR script (default `ssr.mjs`), allocates
an HTTP endpoint on `PORT` and registers a health check against `/health`.

## Wire the web project

The AppHost injects app settings into the web project. Register the Inertia services with
the configuration-aware overloads so those settings are applied:

```csharp
builder.Services.AddInertia(builder.Configuration);
builder.Services.AddViteHelper(builder.Configuration);
```

`WithInertiaReferences` sets:

| Setting | Source |
| --- | --- |
| `Vite__HotUrl` | The Vite dev server endpoint |
| `Inertia__SsrEnabled` | `true` |
| `Inertia__SsrUrl` | The SSR endpoint plus `/render` |

`Vite__HotUrl` binds to `ViteOptions.HotUrl`. When it is set, the Vite helper runs in hot
mode and resolves assets against that URL, so hot module reload works without a `hot` file
in `wwwroot`.

`Inertia__SsrEnabled` and `Inertia__SsrUrl` bind to `InertiaOptions.SsrEnabled` and
`InertiaOptions.SsrUrl`. Because Aspire owns the SSR process, leave `InertiaOptions.Ssr.Managed`
disabled for this project.

## Sample

See [`samples/aspire`](https://github.com/Creator-Solutions/InertiaCore/tree/main/samples/aspire)
for a runnable AppHost, web project and Vite frontend.
