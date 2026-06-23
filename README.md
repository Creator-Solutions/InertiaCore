# InertiaCore.AspNetCore

[![NuGet](https://img.shields.io/nuget/v/InertiaCore.AspNetCore?style=flat-square&color=blue)](https://www.nuget.org/packages/InertiaCore.AspNetCore)
[![Downloads](https://img.shields.io/nuget/dt/InertiaCore.AspNetCore?style=flat-square)](https://www.nuget.org/packages/InertiaCore.AspNetCore)
[![Build](https://img.shields.io/github/actions/workflow/status/kapi2289/InertiaCore/dotnet.yml?style=flat-square)](https://github.com/kapi2289/InertiaCore/actions)
[![License](https://img.shields.io/github/license/kapi2289/InertiaCore?style=flat-square)](https://github.com/kapi2289/InertiaCore/blob/main/LICENSE)

# Inertia.js Adapter for ASP.NET Core

A production-ready ASP.NET Core adapter for building modern **Inertia.js** applications with .NET.

InertiaCore.AspNetCore provides a complete server-side integration layer for Inertia.js, enabling you to build modern monolithic applications using your preferred frontend framework while keeping the simplicity of server-side routing and controllers.

Supports:

- ASP.NET Core MVC
- ASP.NET Core Minimal APIs
- Inertia.js v3
- Vue, React, and other Inertia-compatible frontend frameworks

This project is based on the original [InertiaCore](https://github.com/kapi2289/InertiaCore) project by Kacper Ziubryniewicz.

---

## Why InertiaCore?

InertiaCore brings the Inertia.js server adapter experience to the .NET ecosystem.

It provides:

- A complete Inertia request/response pipeline
- Server-side rendering support
- Lazy and async props
- Shared application data
- Validation error handling
- Vite integration
- Asset version management
- Protocol-compliant redirects
- Minimal API support

No separate API layer is required. Build your application with server-side routing and modern frontend components.

---

# Features

## Inertia.js v3 Support

✅ Full support for the latest Inertia.js v3 protocol.

Includes:

- Inertia request detection
- Version handling
- Partial reloads
- Lazy props
- Shared props
- Redirect handling
- Error bag support
- Response formatting

---

## ASP.NET Core Support

Supported application styles:

✅ MVC Controllers

```csharp
public IActionResult Index()
{
    return Inertia.Render("Dashboard");
}
```

✅ Minimal APIs

```csharp
app.MapGet("/dashboard", () =>
{
    return Inertia.Render("Dashboard");
});
```

---

## Validation Error Handling

Automatic validation handling using ASP.NET Core ModelState.

Features:

- Automatic validation error collection
- Named validation error bags
- `X-Inertia-Error-Bag` support
- Standardized string array error responses

Example:

```json
{
  "errors": {
    "default": {
      "email": [
        "The email field is required."
      ]
    }
  }
}
```

---

## Shared Data

Share data globally across all Inertia responses.

Example:

```csharp
Inertia.Share("auth", new
{
    UserId = userId
});
```

or:

```csharp
Inertia.Share(new Dictionary<string, object?>
{
    ["auth"] = new
    {
        UserId = userId
    }
});
```

---

## Lazy and Async Props

Load expensive data only when required.

Example:

```csharp
public IActionResult Index()
{
    return Inertia.Render("Posts", new
    {
        Posts = new LazyProp(async () =>
        {
            return await _context.Posts.ToListAsync();
        })
    });
}
```

---

## Server-Side Rendering

Built-in support for Inertia SSR.

Enable SSR:

```csharp
builder.Services.AddInertia(options =>
{
    options.SsrEnabled = true;
});
```

Configure your SSR endpoint:

```csharp
builder.Services.AddInertia(options =>
{
    options.SsrUrl = "http://127.0.0.1:13714/render";
});
```

---

## Vite Integration

Includes helpers for Vite-powered applications.

Register:

```csharp
builder.Services.AddViteHelper();
```

Use in your layout:

```html
@Vite.Input("src/main.ts")
```

React HMR support:

```html
@Vite.ReactRefresh()
```

---

## Asset Version Management

Flexible version resolution support.

Built-in providers:

- Default static version provider
- Delegate-based version provider
- Custom provider implementations

Example:

```csharp
builder.Services.AddInertia(options =>
{
    options.VersionResolver = () =>
    {
        return "1.0.0";
    };
});
```

---

## Inertia Redirect Handling

Automatically handles Inertia-compliant redirects.

Supports:

- GET redirects
- POST redirects
- PUT redirects
- PATCH redirects
- DELETE redirects

Non-GET requests automatically use `303 See Other`.

---

## Empty Response Handling

Empty responses from Inertia requests are automatically converted into redirects.

Handled scenarios:

```csharp
return Ok();
```

```csharp
return NoContent();
```

The adapter redirects users back to the previous page using:

1. The `Referer` header
2. Current request URL fallback

---

# Installation

Install from NuGet:

### Package Manager

```powershell
Install-Package InertiaCore.AspNetCore
```

### .NET CLI

```bash
dotnet add package InertiaCore.AspNetCore
```

---

# Getting Started

Add Inertia services:

```csharp
using InertiaCore.Extensions;

builder.Services.AddInertia();
```

Add middleware:

```csharp
app.UseInertia();
```

Your application is now ready to serve Inertia responses.

---

# Frontend Setup

Create your root view.

Example:

`Views/App.cshtml`

```html
@using InertiaCore

<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8"/>
    <meta name="viewport" content="width=device-width, initial-scale=1.0"/>

    <title inertia>
        My Application
    </title>

    @await Inertia.Head(Model)
</head>

<body>

@await Inertia.Html(Model)

<script type="module" src="/src/main.ts"></script>

</body>
</html>
```

---

# Backend Usage

Render an Inertia page:

```csharp
public IActionResult Index()
{
    return Inertia.Render("Dashboard", new
    {
        User = user
    });
}
```

---

# Configuration

Customize Inertia:

```csharp
builder.Services.AddInertia(options =>
{
    options.RootView = "~/Views/App.cshtml";

    options.SsrEnabled = true;

    options.SsrUrl =
        "http://127.0.0.1:13714/render";
});
```

---

# Example Applications

Example projects:

- Vue:
  https://github.com/NejcBW/InertiaCoreVueTemplate

- React:
  https://github.com/nicksoftware/React-AspnetCore-inertiaJS

---

# Roadmap

Upcoming improvements:

- Additional Inertia protocol coverage
- Improved developer tooling
- More ASP.NET Core integrations
- Additional examples and templates

---

# Contributing

Contributions, issues, and feature requests are welcome.

Please open an issue or submit a pull request.

---

# License

Licensed under the MIT License.

See [LICENSE](LICENSE) for details.
