# InertiaCore.AspNetCore

[![NuGet](https://img.shields.io/nuget/v/InertiaCore.AspNetCore?style=flat-square&color=blue)](https://www.nuget.org/packages/InertiaCore.AspNetCore)
[![Downloads](https://img.shields.io/nuget/dt/InertiaCore.AspNetCore?style=flat-square)](https://www.nuget.org/packages/InertiaCore.AspNetCore)
[![Build](https://img.shields.io/github/actions/workflow/status/Creator-Solutions/InertiaCore/dotnet.yml?style=flat-square)](https://github.com/Creator-Solutions/InertiaCore/actions)
[![License](https://img.shields.io/github/license/Creator-Solutions/InertiaCore?style=flat-square)](https://github.com/Creator-Solutions/InertiaCore/blob/main/LICENSE)

A production-ready ASP.NET Core adapter for building **Inertia.js** applications with .NET.

InertiaCore.AspNetCore provides a complete server-side integration layer for Inertia.js, enabling you
to build monolithic applications using your preferred frontend framework while keeping the
simplicity of server-side routing and controllers.

Supports ASP.NET Core MVC and Minimal APIs, Inertia.js v3, and Vue, React and other
Inertia-compatible frontend frameworks.

> **Documentation:** [https://creator-solutions.github.io/InertiaCore/](https://creator-solutions.github.io/InertiaCore/)

## Quick start

Install the package:

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

Render a page:

```csharp
public IActionResult Index()
{
    return Inertia.Render("Dashboard", new
    {
        User = user
    });
}
```

See the [installation guide](https://inertiacore.dev/getting-started) to create your root view and
wire up the frontend.

## Features

- **Inertia.js v3**: full protocol support: request detection, version handling, partial reloads,
  lazy props, shared props, redirects and error bags.
- **MVC & Minimal APIs**: the same Inertia responses work in controllers and endpoint handlers.
- **Rich props**: lazy and async props, deferred, once, merge and infinite-scroll props, with
  per-prop rescue.
- **Server-side rendering**: built-in SSR gateway support.
- **Vite integration**: HMR detection, manifest resolution, module preloading, CSS handling, CSP
  nonces and integrity hashes.
- **Validation & Precognition**: automatic `ModelState` error handling, `WithInertiaValidation()`
  for Minimal API endpoints (303 back with errors for Inertia requests), and form precognition.
- **Typed end to end**: generate TypeScript declarations for your page props and typed route
  helpers from your C# contracts with `InertiaCore.TypeGen`.

## Documentation

Full guides and the configuration reference live at **https://inertiacore.dev**:

- [Introduction](https://inertiacore.dev/introduction)
- [Installation](https://inertiacore.dev/getting-started)
- [Props](https://inertiacore.dev/guide/props)
- [TypeScript](https://inertiacore.dev/typescript/)
- [Security](https://inertiacore.dev/security)
- [Configuration reference](https://inertiacore.dev/reference/configuration)

The documentation source lives in [`docs/`](docs) and is built with
[VitePress](https://vitepress.dev). Run it locally with `cd docs && npm ci && npm run dev`.

## Example applications

- Vue: [NejcBW/InertiaCoreVueTemplate](https://github.com/NejcBW/InertiaCoreVueTemplate)
- React: [nicksoftware/React-AspnetCore-inertiaJS](https://github.com/nicksoftware/React-AspnetCore-inertiaJS)

## Project

- [Changelog](CHANGELOG.md)
- [Security policy](SECURITY.md)
- [Contributing](https://inertiacore.dev/project/contributing)
- [Examples](samples)

This project is based on the original [InertiaCore](https://github.com/kapi2289/InertiaCore) project
by Kacper Ziubryniewicz.

## License

Licensed under the MIT License. See [LICENSE](LICENSE) for details.
