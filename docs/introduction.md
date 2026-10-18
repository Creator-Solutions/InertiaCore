# Introduction

**InertiaCore.AspNetCore** is a production-ready ASP.NET Core adapter for building
[Inertia.js](https://inertiajs.com) applications with .NET.

It provides a complete server-side integration layer for Inertia.js, letting you build
monolithic applications with your preferred frontend framework while keeping the simplicity of
server-side routing and controllers.

Supported:

- ASP.NET Core MVC
- ASP.NET Core Minimal APIs
- Inertia.js v3
- Vue, React, and other Inertia-compatible frontend frameworks

## Why InertiaCore?

InertiaCore brings the Inertia.js server-adapter experience to the .NET ecosystem. It provides:

- A complete Inertia request/response pipeline
- Server-side rendering support
- Lazy and async props
- Shared application data
- Validation error handling
- Vite integration
- Asset version management
- Protocol-compliant redirects
- Minimal API support

No separate API layer is required. Build your application with server-side routing and modern
frontend components.

## Requirements

- .NET 8, 9, or 10
- An Inertia.js v3 client (`@inertiajs/vue3` or `@inertiajs/react`)
- A frontend build tool. [Vite](https://vitejs.dev) is supported directly, but any bundler works

## Features at a glance

### Inertia.js v3 support

Full support for the latest Inertia.js v3 protocol, including request detection, version handling,
partial reloads, lazy props, shared props, redirect handling, error bag support and response
formatting.

### MVC and Minimal APIs

The same Inertia responses work in both application styles:

::: code-group

```csharp [MVC]
public IActionResult Index()
{
    return Inertia.Render("Dashboard");
}
```

```csharp [Minimal API]
app.MapGet("/dashboard", () =>
{
    return Inertia.Render("Dashboard");
});
```

:::

### Validation error handling

Automatic validation handling using ASP.NET Core `ModelState`, with named validation error bags,
`X-Inertia-Error-Bag` support and standardized string-array error responses. See
[Validation](/guide/validation).

### Precognition

Validate a form against the real server-side rules without running the action, short-circuiting
with `204`/`422`. See [Precognition](/guide/precognition).

### Typed props and routes

Generate TypeScript declarations for every `[InertiaPage]` type and a Ziggy-style `route()` helper
from your named Minimal API endpoints. See [TypeScript](/typescript/).

## Example applications

- Vue: [NejcBW/InertiaCoreVueTemplate](https://github.com/NejcBW/InertiaCoreVueTemplate)
- React: [nicksoftware/React-AspnetCore-inertiaJS](https://github.com/nicksoftware/React-AspnetCore-inertiaJS)

## Credits

This project is based on the original
[InertiaCore](https://github.com/kapi2289/InertiaCore) project by Kacper Ziubryniewicz.

## License

Licensed under the MIT License. See [LICENSE](https://github.com/Creator-Solutions/InertiaCore/blob/main/LICENSE).
