# Configuration reference

## `AddInertia`

`AddInertia` accepts a configuration delegate over `InertiaOptions`:

```csharp
builder.Services.AddInertia(options =>
{
    // ...
});
```

| Option | Type | Default | Description |
| --- | --- | --- | --- |
| `RootView` | `string` | `"~/Views/App.cshtml"` | Path to the root HTML view. |
| `SsrEnabled` | `bool` | `false` | Enable server-side rendering via the gateway. |
| `SsrUrl` | `string` | `"http://127.0.0.1:13714/render"` | SSR gateway endpoint. |
| `SsrTimeout` | `TimeSpan` | `5s` | Timeout for a single SSR render request; on timeout the response falls back to client-side rendering. |
| `SsrMaxResponseBytes` | `long` | `10 MiB` | Maximum SSR response body size that will be read and parsed; larger responses fall back to client-side rendering. |
| `EncryptHistory` | `bool` | `false` | Emit `encryptHistory: true` in the page object. |
| `Version` | `string?` | `""` | The current asset version. An empty string tells the client this server does not track asset versions. |
| `VersionResolver` | `Func<IServiceProvider, string>?` | `null` | Resolve the asset version per request; takes precedence over `Version`. |
| `ValidatePages` | `bool` | `false` | Throw when rendering a component not declared with `[InertiaPage]`. |

### `InertiaOptions.Antiforgery`

The `Antiforgery` object configures the opt-in XSRF cookie bridge. See
[CSRF / antiforgery](/security) for usage.

| Option | Type | Default | Description |
| --- | --- | --- | --- |
| `Enabled` | `bool` | `false` | Register anti-forgery and issue the XSRF cookie via `UseInertiaAntiforgery()`. |
| `CookieName` | `string` | `"XSRF-TOKEN"` | Name of the JavaScript-readable token cookie. |
| `HeaderName` | `string` | `"X-XSRF-TOKEN"` | Request header the token is validated from. |
| `SecurePolicy` | `CookieSecurePolicy` | `SameAsRequest` | When the token cookie is sent over HTTPS. Use `Always` in production. |
| `SameSite` | `SameSiteMode` | `Lax` | `SameSite` policy of the token cookie. |
| `CookiePath` | `string` | `"/"` | Path the token cookie is scoped to. |

### `InertiaOptions.StatusPages`

The `StatusPages` object configures the opt-in Inertia error pages. See
[Error pages](/guide/error-pages) for usage.

| Option | Type | Default | Description |
| --- | --- | --- | --- |
| `Enabled` | `bool` | `false` | Render the error component for handled statuses via `UseInertiaStatusPages()`, and convert cookie-auth login redirects for Inertia requests. |
| `Component` | `string` | `"Error"` | Component rendered for a handled status with no per-status override. |
| `Components` | `IDictionary<int, string>` | `{}` | Per-status component overrides. |
| `StatusCodes` | `IReadOnlyCollection<int>` | `401, 403, 404, 419, 500, 503` | The status codes rendered through Inertia. |
| `Messages` | `IDictionary<int, string>` | `{}` | Per-status message overrides. Built-in defaults apply otherwise. |
| `MessageFactory` | `Func<HttpContext, int, string>?` | `null` | Resolves the message for a status; takes precedence over `Messages`. |
| `RespectDeveloperExceptionPage` | `bool` | `true` | In Development, preserve the developer exception page for `500` instead of rendering the Inertia error page. |

## `AddViteHelper`

`AddViteHelper` configures the Vite integration with `ViteOptions`:

```csharp
builder.Services.AddViteHelper(options =>
{
    // ...
});
```

| Option | Type | Default | Description |
| --- | --- | --- | --- |
| `HotFile` | `string` | `"hot"` | Path to the Vite "hot" file. |
| `BuildDirectory` | `string?` | `"build"` | Path to the build directory. |
| `ManifestFilename` | `string` | `"manifest.json"` | Name of the manifest file. |
| `PublicDirectory` | `string` | `"wwwroot"` | Path to the public directory. |
| `CspNonce` | `string?` | `null` | Static CSP nonce applied to generated tags. Prefer `NonceResolver` when the nonce is per-request. |
| `NonceResolver` | `Func<string?>?` | `null` | Resolves the CSP nonce at render time; takes precedence over `CspNonce` when it returns non-null. |
| `IntegrityKey` | `string?` | `"integrity"` | Manifest key used to detect Subresource Integrity hashes. Set to `null` to disable. |
| `PrefetchStrategy` | `PrefetchStrategy` | `None` | Prefetch strategy for dynamic imports. |
| `PrefetchConcurrency` | `int` | `3` | Concurrency for the waterfall prefetch strategy. |
| `PrefetchEvent` | `string` | `"load"` | Window event that triggers prefetching. |
| `ScriptTagAttributes` | `IList<...>` | empty | Extra attributes applied to generated script tags. |
| `StyleTagAttributes` | `IList<...>` | empty | Extra attributes applied to generated stylesheet tags. |
| `PreloadTagAttributes` | `IList<...>` | empty | Extra attributes applied to generated preload/modulepreload tags. |

## Minimal API route dump

`MapInertiaRoutes()` maps the Development-only route metadata endpoint used by
`inertiatsgen routes`. It returns `404` outside Development. See
[Typed routes](/typescript/routes).

## Tool options

The `inertiatsgen` tool has its own flags; see
[Declarations options](/typescript/declarations#options) and
[Typed routes](/typescript/routes).
