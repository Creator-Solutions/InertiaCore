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

Share data across Inertia responses.

When called **during a request** (for example inside a controller), `Inertia.Share`
is scoped to that request and is never visible to other requests or users:

```csharp
public IActionResult Index()
{
    Inertia.Share("auth", new { UserId = userId });
    return Inertia.Render("Dashboard");
}
```

When called **outside a request** (for example during application startup), the
value is registered globally and merged into every response:

```csharp
// Program.cs
Inertia.Share("appName", "My App");

Inertia.Share(new Dictionary<string, object?>
{
    ["auth"] = new { UserId = userId }
});
```

The merge order is **global → request-scoped → component props**, so component
props always win. When injecting `IInertia` via DI, `Share` is always
request-scoped.

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

## Merging Props

Merge props tell the client to combine incoming data with the existing page data
during partial reloads instead of replacing it — useful for "load more" pagination.
A full visit always replaces the prop.

```csharp
public IActionResult Index(int page = 1)
{
    var tags = _allTags.Skip((page - 1) * 5).Take(5);

    return Inertia.Render("Tags/Index", new
    {
        Tags = Inertia.Merge(tags)
    });
}
```

Append at the root (the default), prepend, deep merge, or target a nested path:

```csharp
Inertia.Merge(items);                        // append at the root
Inertia.Merge(items).Prepend();              // prepend at the root
Inertia.Merge(paginator).Append("data");     // merge only the "data" array
Inertia.DeepMerge(chat);                     // deep merge the whole structure
Inertia.Merge(posts).MatchOn("id");          // update items matched by id
Inertia.DeepMerge(chat).MatchOn("messages.id");
```

Merge props compose with the other prop wrappers, so a prop can be deferred and
mergeable at the same time:

```csharp
public IActionResult Index()
{
    return await _inertia.Render("Users/Index", new
    {
        Results = _inertia.Defer(() => LoadUsers()).Merge()
    });
}
```

To have the client replace a prop before merging new data, send its key in the
`X-Inertia-Reset` header (for example `X-Inertia-Reset: results`). Reset props are
still returned in `props`, but omitted from every merge array.

---

## Infinite Scroll

`Inertia.Scroll` configures a paginated prop for Inertia's `<InfiniteScroll>`
component. It emits the pagination cursor under `scrollProps` and marks the inner
array (`data` by default) for merge, appending when loading forward and prepending
when loading backward.

```csharp
public IActionResult Index(int page = 1)
{
    var users = _db.Users.OrderBy(u => u.Id).ToPagedList(page, 20);

    return Inertia.Render("Users/Index", new
    {
        Users = Inertia.Scroll(
            new { data = users.Items },
            new ScrollMetadata(
                PageName: "page",
                PreviousPage: page > 1 ? page - 1 : null,
                NextPage: users.HasMore ? page + 1 : null,
                CurrentPage: page))
    });
}
```

- The prop value is emitted as-is; the array at `data` is merged (`mergeProps:
  ["users.data"]`).
- The direction comes from the client's `X-Inertia-Infinite-Scroll-Merge-Intent`
  header (`prepend` or `append`).
- Cursor tokens are nullable and may be numbers or strings; `null` is serialized as
  `null`, never omitted.
- `X-Inertia-Reset: users` marks `scrollProps.users.reset = true` and removes the
  merge label so the client replaces instead of merging.
- Pass a custom wrapper as the third argument when the array key is not `data`
  (for example `Inertia.Scroll(paginator, metadata, "items")`).

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

The version defaults to an empty string, which is a valid Inertia protocol value
meaning "this server does not track asset versions". `AddInertia()` therefore
works out of the box without configuring a version.

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
return NoContent(); // 204
```

```csharp
Response.ContentLength = 0; // 200 with no body
```

The adapter redirects users back to the previous page using:

1. The `Referer` header, but only when it is a safe relative path or points at the
   current scheme, host and port
2. Current request URL fallback

A `200` response with no content type is treated as empty when its content length
is `null` or `0`.

---

## Page Registry & Validation

Declare your page components so the adapter can validate them and tooling can
reference them as strongly-typed constants:

```csharp
[assembly: InertiaPage("Dashboard")]
[assembly: InertiaPage("Users/Edit")]
```

or on a class:

```csharp
[InertiaPage("Account/Settings")]
public class AccountController { }
```

The source generator emits an `InertiaCore.Generated.InertiaPages` class with a
constant per page and registers each component with `InertiaPageRegistry` at
startup. Enable runtime validation to fail fast on unknown components:

```csharp
builder.Services.AddInertia(options =>
{
    options.ValidatePages = true;
});
```

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
