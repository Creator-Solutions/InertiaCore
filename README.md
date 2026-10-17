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

## Precognition

[Precognition](https://inertiajs.com/docs/v2/the-basics/forms#precognition) lets the
client validate a form against the real server-side rules without running the
action. InertiaCore detects `Precognition: true` requests, validates them, and
short-circuits with `204`/`422` — the endpoint/action body never runs.

The response follows the Inertia protocol:

- **Valid** — `204 No Content` with `Precognition-Success: true`.
- **Invalid** — `422 Unprocessable Entity` with `{ "errors": { field: [messages] } }`.
- `Precognition-Validate-Only: field1,field2` limits the returned errors to those fields.
- `Vary: Precognition` is added to the response.

### MVC controllers

Precognition is enabled globally; no per-action setup is required. When the request
carries `Precognition: true`, the action filter validates the model state and
short-circuits before the action runs (and before MVC's automatic `400`):

```csharp
public class UsersController : Controller
{
    [HttpPost]
    public IActionResult Store(CreateUserRequest request)
    {
        // Not reached for precognitive or invalid requests.
        _db.Users.Add(request.ToUser());
        return RedirectToAction("Index");
    }
}
```

### Minimal APIs

Endpoint filters run after argument binding, so keep side effects inside the handler
body. Opt in per endpoint with `WithInertiaPrecognition()`:

```csharp
app.MapPost("/users", (CreateUserRequest request) =>
{
    // Not reached for precognitive requests.
    db.Users.Add(request.ToUser());
    return Results.Created("/users", null);
})
.WithInertiaPrecognition();
```

### Validation

The default validator applies
[`System.ComponentModel.DataAnnotations`](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations)
(including `IValidatableObject`) to the bound arguments. To use a different engine
(for example FluentValidation), register your own `IInertiaRequestValidator` after
`AddInertia()`:

```csharp
builder.Services.AddInertia();
builder.Services.AddScoped<IInertiaRequestValidator, FluentValidationRequestValidator>();
```

---

## CSRF / Antiforgery

Inertia sends every visit as an `XMLHttpRequest`/`fetch` call, so requests are not
submitted as HTML forms and ASP.NET Core's automatic form-field antiforgery no
longer sees a hidden field. **The adapter does not wire up CSRF protection for
you** — you must do it explicitly.

ASP.NET Core's antiforgery service issues a request token and validates it from a
request header. Configure the header name to the one your frontend will send and
expose the token to the page (for example as a shared prop):

```csharp
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    // Inertia's Laravel-style convention uses an XSRF-TOKEN cookie plus an
    // X-XSRF-TOKEN header. Either naming works; the cookie must stay readable by
    // JavaScript, so do not set HttpOnly on it.
    options.Cookie.Name = "XSRF-TOKEN";
    options.Cookie.HttpOnly = false;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

var app = builder.Build();
app.UseInertia();

app.Use(async (context, next) =>
{
    var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
    var tokens = antiforgery.GetAndStoreTokens(context);
    Inertia.Share("csrfToken", tokens.RequestToken);
    await next();
});
```

Send the token back on every mutating request. With Inertia's `XSRF-TOKEN` cookie /
`X-XSRF-TOKEN` header convention, set `options.HeaderName = "X-XSRF-TOKEN"` and the
Inertia client will attach the cookie value automatically; otherwise set the header
from your HTTP client:

```js
import axios from "axios";

axios.defaults.headers.common["X-CSRF-TOKEN"] =
  window.initialPage.props.csrfToken;
```

Finally, validate the token server-side on every `POST`/`PUT`/`PATCH`/`DELETE`
(for example with a global `AutoValidateAntiforgeryToken` filter, an endpoint
filter, or `IAntiforgery.ValidateRequestAsync`). Keep `Secure` on in production and
set `SameSite` appropriately; only the readable XSRF token cookie should omit
`HttpOnly`.

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

## Deferred Props

Deferred props are excluded from the initial response and fetched afterwards. The
page object announces them under `deferredProps`, grouped so the client can fetch
groups in parallel.

```csharp
return Inertia.Render("Users/Index", new
{
    Users = _db.Users.ToList(),
    Permissions = Inertia.Defer(() => LoadPermissions()),
    Teams = Inertia.Defer(() => LoadTeams(), "attributes"),
    Projects = Inertia.Defer(() => LoadProjects(), "attributes")
});
```

The response contains `deferredProps: { "default": ["permissions"], "attributes":
["teams", "projects"] }` and omits the values. The client requests each group with
a partial reload (`only`), and the callbacks are only invoked when requested.

---

## Once Props

Once props are resolved a single time and remembered by the client, then reused on
subsequent pages that include them.

```csharp
return Inertia.Render("Billing", new
{
    Plans = Inertia.Once(() => Plan.All()).Until(TimeSpan.FromHours(1))
});
```

The page object emits `onceProps` (`{ "plans": { "prop": "plans", "expiresAt": ... } }`).
On later visits the client lists the once keys it already holds in the
`X-Inertia-Except-Once-Props` header, and the callback is skipped and the prop
omitted. A partial reload always resolves a requested once prop. Use `.As("key")`
to share data under a different name and `.Fresh()` to force a refresh.

---

## Rescuing Deferred Props

A deferred prop can be resolved with rescue enabled, so a failure is logged, the
value omitted, and the key reported through `rescuedProps` instead of failing the
whole request.

```csharp
return Inertia.Render("Users/Index", new
{
    Permissions = Inertia.Defer(() => LoadPermissions(), "default", rescue: true)
    // or: Inertia.Defer(() => LoadPermissions()).Rescue()
});
```

A response with `rescuedProps: ["permissions"]` and a `200` status lets the client
render an error state. Exceptions from props without rescue still fail the request.

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

Includes helpers for Vite-powered applications with feature parity to Laravel's
`@vite` directive: hot-module-reload detection, manifest resolution, module
preloading, CSS handling, CSP nonces, integrity hashes and prefetching.

Register:

```csharp
builder.Services.AddViteHelper();
```

Use in your layout:

```html
@Vite.Input("src/main.ts")
```

Multiple entry points:

```html
@Vite.Input("src/main.ts", "src/admin.ts")
```

Async rendering (uses the cached manifest read):

```html
@await Vite.InputAsync("src/main.ts")
```

React HMR support:

```html
@Vite.ReactRefresh()
```

Resolve a manifest entry to its public URL, or read its content:

```html
<img src="@Vite.Asset("src/logo.svg")" />
```

```csharp
var svg = Vite.Content("src/logo.svg");
```

### Configuration

```csharp
builder.Services.AddViteHelper(options =>
{
    options.HotFile = "hot";
    options.BuildDirectory = "build";
    options.ManifestFilename = "manifest.json";
    options.PublicDirectory = "wwwroot";

    // CSP nonce — static, or resolved per request.
    options.CspNonce = "nonce-value";
    options.NonceResolver = () => httpContextAccessor.HttpContext?.Items["csp-nonce"] as string;

    // Subresource integrity (null disables it).
    options.IntegrityKey = "integrity";

    // Prefetch dynamic imports after load.
    options.PrefetchStrategy = PrefetchStrategy.Aggressive;
    options.PrefetchConcurrency = 3;   // waterfall only
    options.PrefetchEvent = "load";

    // Append custom attributes to generated tags.
    options.ScriptTagAttributes.Add((src, url, chunk) =>
        new Dictionary<string, object?> { ["defer"] = true });
});
```

Each rendered entry point produces a module preload link plus its script tag,
walks manifest `imports` recursively, and emits stylesheet preloads before
scripts. `Vite.GetManifestHash()` returns `null` while running hot so the asset
version does not change during development.

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

> **Warning:** `Inertia.Location(url)` and the redirect helpers do **not** validate
> the destination. Passing a user-supplied value straight through creates an open
> redirect. Validate the target before redirecting — for example, only accept local
> URLs, or an allow-listed set of hosts:

```csharp
public IActionResult AfterLogin(string? returnUrl)
{
    if (Url.IsLocalUrl(returnUrl))
        return Redirect(returnUrl!);

    return RedirectToAction("Index", "Home");
}
```

Use `Inertia.Location(url)` only when you intend a full-page, cross-origin redirect
(for example to an external OAuth provider) and the URL is a trusted constant.

---

## History State

`encryptHistory` and `clearHistory` are emitted in the page object when set, so the
client can encrypt the current history state or clear any encrypted state:

```csharp
public IActionResult Index()
{
    Inertia.EncryptHistory(); // emit "encryptHistory": true
    return Inertia.Render("Dashboard");
}
```

When both are left unset they are omitted from the page object.

---

## Preserving Fragments

If a redirect target contains a URL fragment (`/article/new#section`), the adapter
returns a `409 Conflict` with `X-Inertia-Redirect`, and the client performs a fresh
Inertia `GET` to that URL instead of a full-page reload. Prefetch requests are
excluded.

To carry the **original** request's fragment to a redirect target that has none, call
`PreserveInertiaFragment()` before redirecting:

```csharp
public IActionResult Show(string slug)
{
    HttpContext.PreserveInertiaFragment();
    return Redirect("/article/new-slug"); // client ends up on /article/new-slug#section
}
```

The following request's page object carries `preserveFragment: true`. The flag is
transported through `TempData`, so a TempData/session provider must be configured.

---

## Prefetching

Inertia prefetch requests set the `Purpose: prefetch` header. Detect them with
`HttpContext.IsInertiaPrefetch()`. Prefetch requests do not consume flash data, so
the eventual real visit still receives it.

```csharp
if (!HttpContext.IsInertiaPrefetch())
{
    // Only run side effects for real visits.
    await _service.MarkSeenAsync();
}
```

---

## Shared Props Metadata

The keys registered via `Inertia.Share` (global and request-scoped) are emitted as
`sharedProps`, which the client uses to carry shared props over during instant
visits:

```json
{
  "component": "Dashboard",
  "props": { "auth": { "user": "..." }, "stats": { } },
  "sharedProps": ["auth"]
}
```

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

### Typed props (`Render<T>`)

A props record can be the page contract: annotate it with `[InertiaPage]` and render
it by type, so callers never type the component string:

```csharp
[InertiaPage("Users/Index")]
public sealed record UsersIndexProps(IReadOnlyList<UserDto> Users, Pagination Page);

public IActionResult Index()
{
    var props = new UsersIndexProps(users, pagination);
    return Inertia.Render(props); // resolves the component "Users/Index" from the type
}
```

The generator emits a type-to-component table (a dictionary keyed by `Type`, no
runtime reflection) alongside the constants, and registers it at startup. `Render<T>`
returns a page object identical to `Render("Users/Index", props)`.

- If the type has no `[InertiaPage]` attribute, `Render<T>` is a compile-time error
  (`INERTIA006`) and throws `InvalidOperationException` at runtime.
- Two types declaring the same component name is a compile-time error (`INERTIA005`).
- A type with **multiple** `[InertiaPage]` attributes (for example a controller) has no
  unambiguous mapping, so it is left out of the type table and `Render<T>` throws —
  use the string overload for those.
- Wrapper members (`DeferredProp`, `LazyProp`, `AlwaysProp`, `MergeProp`, `OnceProp`,
  `ScrollProp`) are allowed on the props record and keep their behaviour
  (`[Deferred]`/`[Lazy]` resolve on partial reloads, `Always` on every response).

The existing `Render(string component, props)` overloads are unchanged.

### TypeScript declarations (`InertiaCore.TypeGen`)

`InertiaCore.TypeGen` is a dotnet tool that reads your compiled assembly and writes
TypeScript declarations for every `[InertiaPage]` type and the types it references, so
the frontend has a compile-time contract for page props. It loads the assembly with
`MetadataLoadContext` and never executes your code.

Install and run:

```bash
dotnet tool install --global InertiaCore.TypeGen

inertiatsgen --assembly bin/Debug/net8.0/MyApp.dll \
             --output Client/types/inertia.generated.d.ts
```

For a record like this:

```csharp
[InertiaPage("Users/Index")]
public sealed record UsersIndexProps(IReadOnlyList<UserDto> Users, int Page);
```

the tool emits:

```ts
export interface UsersIndexProps {
  users: UserDto[];
  page: number;
}

export interface InertiaPageProps {
  'Users/Index': UsersIndexProps;
}
```

Options:

| Option | Description |
| --- | --- |
| `--assembly <path>` | Compiled assembly to read. Required. |
| `--output <path>` | Output file. Default: `Client/types/inertia.generated.d.ts`. |
| `--reference <path>` | Additional assembly used to resolve references. Repeatable. |
| `--enum-mode <mode>` | `auto` (default), `string` or `number`. |
| `--no-camel-case` | Disable camelCase property naming. |
| `--emit-react-helper` | Emit a typed `usePageProps` helper for `@inertiajs/react`. |
| `--check` | Exit non-zero if the output is out of date (no write). |
| `--watch` | Regenerate whenever the assembly changes. |

Type mapping follows the InertiaCore serializer defaults:

| C# | TypeScript |
| --- | --- |
| `string`, `Guid`, `DateTime`, `DateTimeOffset`, `TimeSpan` | `string` |
| numeric types | `number` |
| `bool` | `boolean` |
| `T?` | `T \| null` |
| `IEnumerable<T>` / `T[]` / `List<T>` | `T[]` |
| `Dictionary<string, T>` | `Record<string, T>` |
| enum without a string converter | numeric union (`0 \| 1`) |
| enum with `JsonStringEnumConverter` | string-literal union (`'A' \| 'B'`) |
| record / class | `interface` |

camelCase naming, `[JsonPropertyName]` and `[JsonIgnore]` are respected. Members that
are `Lazy`, `Defer` or `Optional` props, and members marked
`[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`, are emitted as
optional properties (`name?: T`). Cyclic references are handled; output is
deterministic so diffs stay clean.

Use `--check` in CI to fail when the declarations are stale:

```bash
inertiatsgen --assembly bin/Release/net10.0/MyApp.dll \
             --output Client/types/inertia.generated.d.ts --check
```

#### Shared props

Mark the record that describes your shared props with `[InertiaSharedProps]` and the tool
emits it as a dedicated `InertiaSharedProps` interface:

```csharp
[InertiaSharedProps]
public sealed record SharedProps(string AppName, TypedUser? Auth);
```

```ts
export interface InertiaSharedProps {
  appName: string;
  auth: TypedUser | null;
}
```

The attribute is a code-generation marker only — it does not share anything at runtime.
Keep using `Inertia.Share(...)` to populate the values; the record just describes their
shape. At most one type per assembly may be annotated.

#### React helper (opt-in)

Pass `--emit-react-helper` (or set `TypeScriptGeneratorOptions.EmitReactHelper = true`) to
add a typed accessor for the props of a page component:

```bash
inertiatsgen --assembly bin/Release/net10.0/MyApp.dll \
             --output Client/types/inertia.generated.d.ts --emit-react-helper
```

```ts
import { usePage } from '@inertiajs/react';

// ...

export function usePageProps<K extends keyof InertiaPageProps>(component: K): InertiaPageProps[K] & InertiaSharedProps {
  void component;
  return usePage().props as InertiaPageProps[K] & InertiaSharedProps;
}
```

The helper is omitted by default so projects that do not use React hooks are unaffected.
When a shared props record is present, the return type intersects `InertiaSharedProps`
because Inertia merges shared props into every page's props.

#### Typing the page resolver

`InertiaPageProps` maps each component name to its props, which lets you type the
resolver and the page components themselves:

```tsx
import { createInertiaApp } from '@inertiajs/react';
import type { ComponentType } from 'react';
import type { InertiaPageProps } from './types/inertia.generated';
import UsersIndex from './Pages/Users/Index';

type PageComponent<K extends keyof InertiaPageProps> = ComponentType<InertiaPageProps[K]>;

// Every component name must have a matching entry, bound to its declared props.
const pages: { [K in keyof InertiaPageProps]: PageComponent<K> } = {
  'Users/Index': UsersIndex,
};

createInertiaApp({
  // The framework types `name` as `string`; narrow it to the registry keys here.
  resolve: (name) => ({ default: pages[name as keyof InertiaPageProps] }),
});
```

A page component can then take its props straight from the registry:

```tsx
export default function UsersIndex(props: InertiaPageProps['Users/Index']) {
  return <ul>{props.users.map((u) => <li key={u}>{u}</li>)}</ul>;
}
```

Because the page's props are now typed, renaming a props member in C# and regenerating
produces a compile error in every component that read the old name. A working project that
compiles under `tsc --strict` lives in [`samples/react`](samples/react).

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
