---
layout: home

hero:
  name: InertiaCore
  text: Inertia.js for ASP.NET Core
  tagline: Build modern monolithic applications with server-side routing and your favourite frontend framework. No separate API layer.
  image:
    src: /logo.svg
    alt: InertiaCore
  actions:
    - theme: brand
      text: Get started
      link: /getting-started
    - theme: alt
      text: Introduction
      link: /introduction
    - theme: alt
      text: GitHub
      link: https://github.com/Creator-Solutions/InertiaCore

features:
  - title: Inertia.js v3
    details: Full protocol support — request detection, version handling, partial reloads, lazy props, shared props, redirects and error bags.
  - title: MVC & Minimal APIs
    details: The same Inertia responses work in controllers and endpoint handlers alike.
  - title: Rich props
    details: Lazy and async props, deferred, once, merge and infinite-scroll props, with per-prop rescue.
  - title: Server-side rendering
    details: Built-in SSR gateway support with hardened rendering out of the box.
  - title: Vite integration
    details: HMR detection, manifest resolution, module preloading, CSS handling, CSP nonces and integrity hashes.
  - title: Typed end to end
    details: Generate TypeScript declarations for your page props and typed route helpers from your C# contracts.
---

## Quick start

Add the package:

```bash
dotnet add package InertiaCore.AspNetCore
```

Register the services and middleware:

```csharp
using InertiaCore.Extensions;

builder.Services.AddInertia();

var app = builder.Build();

app.UseInertia();
```

Render a page from a controller or an endpoint:

::: code-group

```csharp [Controller]
public IActionResult Index()
{
    return Inertia.Render("Dashboard", new
    {
        User = user
    });
}
```

```csharp [Minimal API]
app.MapGet("/dashboard", () =>
{
    return Inertia.Render("Dashboard");
});
```

:::

Continue with [Installation](/getting-started) to create your root view and wire up the frontend.
