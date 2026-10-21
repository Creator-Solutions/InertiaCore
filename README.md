# InertiaCore.AspNetCore

[![NuGet](https://img.shields.io/nuget/v/InertiaCore.AspNetCore?style=flat-square&color=blue)](https://www.nuget.org/packages/InertiaCore.AspNetCore)
[![Downloads](https://img.shields.io/nuget/dt/InertiaCore.AspNetCore?style=flat-square)](https://www.nuget.org/packages/InertiaCore.AspNetCore)
[![Build](https://img.shields.io/github/actions/workflow/status/Creator-Solutions/InertiaCore/dotnet.yml?style=flat-square)](https://github.com/Creator-Solutions/InertiaCore/actions)
[![.NET](https://img.shields.io/badge/.NET-8%20%7C%209%20%7C%2010-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com)
[![Inertia.js](https://img.shields.io/badge/Inertia.js-v3-9553E9?style=flat-square)](https://inertiajs.com)
[![License](https://img.shields.io/github/license/Creator-Solutions/InertiaCore?style=flat-square)](https://github.com/Creator-Solutions/InertiaCore/blob/main/LICENSE)

**Build React or Vue apps on ASP.NET Core without writing an API.**
Your controllers return pages, Inertia carries the props to your frontend, and you keep
server-side routing, authentication and validation exactly where they are.

InertiaCore.AspNetCore is a production-ready adapter for [Inertia.js](https://inertiajs.com) v3. It works
with ASP.NET Core MVC and Minimal APIs on .NET 8, 9 and 10, and with React, Vue and other
Inertia-compatible frontend frameworks.

[Documentation](https://creator-solutions.github.io/InertiaCore/) · [Getting started](https://creator-solutions.github.io/InertiaCore/getting-started) · [Changelog](https://github.com/Creator-Solutions/InertiaCore/blob/main/CHANGELOG.md) · [NuGet](https://www.nuget.org/packages/InertiaCore.AspNetCore)

## Quick start

**1. Install the package**

```bash
dotnet add package InertiaCore.AspNetCore
```

**2. Register the services and middleware**

```csharp
using InertiaCore.Extensions;

builder.Services.AddInertia();

var app = builder.Build();

app.UseInertia();
```

**3. Render a page from a controller or a Minimal API endpoint**

```csharp
// Controller
public IActionResult Index()
{
    return Inertia.Render("Dashboard", new
    {
        User = user
    });
}

// Minimal API
app.MapGet("/dashboard", () =>
{
    return Inertia.Render("Dashboard");
});
```

**4. Receive the props in your frontend component**

```tsx
// Pages/Dashboard.tsx
type Props = {
  user: { name: string; email: string }
}

export default function Dashboard({ user }: Props) {
  return <h1>Welcome back, {user.name}</h1>
}
```

That is the whole round trip: no REST endpoints, no client-side data fetching, no duplicated routing.
The [installation guide](https://creator-solutions.github.io/InertiaCore/getting-started) covers the root view and the
Vite setup that wires the frontend up.

## Why InertiaCore

This project started from the original [InertiaCore](https://github.com/kapi2289/InertiaCore) by
Kacper Ziubryniewicz and has grown into a full Inertia.js v3 adapter. Compared with a bare-bones
adapter, it adds:

- The complete v3 protocol: deferred, once, merge and infinite-scroll props, partial reloads and error bags.
- Minimal API support alongside MVC, with the same Inertia responses in both.
- A resilient server-side rendering gateway, with health checks and Node process management.
- First-class tooling: Vite integration, .NET Aspire resources and generated TypeScript types.

## Features

| Area | What you get |
| --- | --- |
| **Protocol** | Inertia.js v3 support: request detection, version handling, partial reloads, lazy and shared props, redirects and error bags. |
| **Props** | Lazy, async, deferred, once, merge and infinite-scroll props, with per-prop rescue and DI-resolved props that share `HttpContext.RequestAborted`. [Props guide](https://creator-solutions.github.io/InertiaCore/guide/props) |
| **Server-side rendering** | Built-in SSR gateway with timeouts, retries with jitter, a circuit breaker and an ASP.NET health check. Opt-in management of the Node process. |
| **Vite** | HMR detection, manifest resolution, module preloading, CSS handling, CSP nonces and integrity hashes, plus an opt-in helper that starts the dev server for you. |
| **.NET Aspire** | Run the Vite dev server and Node SSR process as Aspire resources, with endpoints, health checks and dashboard logs. |
| **Validation** | Automatic `ModelState` error handling, `WithInertiaValidation()` for Minimal API endpoints, and form precognition. |
| **Security** | Opt-in `XSRF-TOKEN` / `X-XSRF-TOKEN` anti-forgery bridge with token rotation after sign-in and sign-out. [Security guide](https://creator-solutions.github.io/InertiaCore/security) |
| **Error pages** | Opt-in Inertia error component for `401`, `403`, `404`, `419`, `500` and `503`, with login redirects converted to `409 + X-Inertia-Location`. |
| **Shared data** | Contribute shared props from services with `AddInertiaSharedData<TProvider>()`, including an opt-in claims provider that shares `auth.user`. |
| **TypeScript** | Generate page prop declarations and typed route helpers from your C# contracts. [TypeScript guide](https://creator-solutions.github.io/InertiaCore/typescript/) |

Every option is covered in the [configuration reference](https://creator-solutions.github.io/InertiaCore/reference/configuration).

## Typed end to end

Define your page props once in C#:

```csharp
public record UserDto(string Name, string Email);
public record DashboardProps(UserDto User);

return Inertia.Render("Dashboard", new DashboardProps(user));
```

`InertiaCore.TypeGen` generates the matching TypeScript, so your components stay in sync with the
server and a renamed property becomes a compile error instead of a runtime surprise:

```ts
export interface UserDto {
  name: string
  email: string
}

export interface DashboardProps {
  user: UserDto
}
```

See the [TypeScript guide](https://creator-solutions.github.io/InertiaCore/typescript/) for setup and typed route helpers.

## Packages

| Package | Purpose |
| --- | --- |
| [`InertiaCore.AspNetCore`](https://www.nuget.org/packages/InertiaCore.AspNetCore) | The core adapter: responses, middleware, props, SSR, Vite, validation and security. |
| [`InertiaCore.Aspire.Hosting`](https://www.nuget.org/packages/InertiaCore.Aspire.Hosting) | Runs the Vite dev server and Node SSR process as .NET Aspire resources. |
| [`InertiaCore.TypeGen`](https://www.nuget.org/packages/InertiaCore.TypeGen) | Generates TypeScript declarations and typed route helpers from your C# contracts. |

## Documentation

Full guides and the configuration reference live at **[creator-solutions.github.io/InertiaCore](https://creator-solutions.github.io/InertiaCore/)**:

- [Introduction](https://creator-solutions.github.io/InertiaCore/introduction)
- [Installation](https://creator-solutions.github.io/InertiaCore/getting-started)
- [Props](https://creator-solutions.github.io/InertiaCore/guide/props)
- [TypeScript](https://creator-solutions.github.io/InertiaCore/typescript/)
- [Security](https://creator-solutions.github.io/InertiaCore/security)
- [Configuration reference](https://creator-solutions.github.io/InertiaCore/reference/configuration)

The documentation source lives in [`docs/`](https://github.com/Creator-Solutions/InertiaCore/tree/main/docs) and is built with
[VitePress](https://vitepress.dev). Run it locally with `cd docs && npm ci && npm run dev`.

## Examples

Runnable samples live in the [`samples`](https://github.com/Creator-Solutions/InertiaCore/tree/main/samples) folder.

Community examples:

- Vue: [NejcBW/InertiaCoreVueTemplate](https://github.com/NejcBW/InertiaCoreVueTemplate)
- React: [nicksoftware/React-AspnetCore-inertiaJS](https://github.com/nicksoftware/React-AspnetCore-inertiaJS)

## Contributing

Issues and pull requests are welcome. If InertiaCore is useful to you, a star on
[GitHub](https://github.com/Creator-Solutions/InertiaCore) helps other .NET developers find it.

- [Contributing guide](https://creator-solutions.github.io/InertiaCore/project/contributing)
- [Changelog](https://github.com/Creator-Solutions/InertiaCore/blob/main/CHANGELOG.md)
- [Security policy](https://github.com/Creator-Solutions/InertiaCore/blob/main/SECURITY.md)

## Credits

This project is based on the original [InertiaCore](https://github.com/kapi2289/InertiaCore) project
by Kacper Ziubryniewicz.

## License

Licensed under the MIT License. See [LICENSE](https://github.com/Creator-Solutions/InertiaCore/blob/main/LICENSE) for details.
