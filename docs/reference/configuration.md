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
| `SsrHealthUrl` | `string?` | `null` | Health endpoint probed by the SSR health check; defaults to the origin of `SsrUrl` with `/health` appended. |
| `SsrTimeout` | `TimeSpan` | `5s` | Total budget for a single SSR render dispatch (covering attempts, retries and breaker interaction); on exhaustion the response falls back to client-side rendering. |
| `SsrMaxResponseBytes` | `long` | `10 MiB` | Maximum SSR response body size that will be read and parsed; larger responses fall back to client-side rendering. |
| `EncryptHistory` | `bool` | `false` | Emit `encryptHistory: true` in the page object. |
| `Version` | `string?` | `""` | The current asset version. An empty string tells the client this server does not track asset versions. |
| `VersionResolver` | `Func<IServiceProvider, string>?` | `null` | Resolve the asset version per request; takes precedence over `Version`. |
| `ValidatePages` | `bool` | `false` | Throw when rendering a component not declared with `[InertiaPage]`. |
| `PropResolution` | `PropResolution` | `Sequential` | Resolve props one at a time (`Sequential`) or concurrently (`Parallel`). |

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

### `InertiaOptions.ClaimsSharedData`

The `ClaimsSharedData` object configures the built-in claims shared-data provider. See
[Shared data](/guide/shared-data#built-in-claims-provider) for usage.

| Option | Type | Default | Description |
| --- | --- | --- | --- |
| `Enabled` | `bool` | `false` | Share `auth.user` from the current `ClaimsPrincipal` on every request. |
| `Key` | `string` | `"auth"` | Top-level prop the user object is nested under. |
| `ClaimMapping` | `IDictionary<string, string>` | `{}` | Maps prop names to claim types. The provider contributes nothing when empty. |

### `InertiaOptions.Ssr`

The `Ssr` object configures the managed Node SSR process. See
[Server-side rendering](/guide/ssr#managed-process) for usage.

| Option | Type | Default | Description |
| --- | --- | --- | --- |
| `Managed` | `bool` | `false` | Launch and manage the Node SSR process from the adapter. |
| `NodePath` | `string` | `"node"` | The Node executable; a relative name is resolved against `PATH`. |
| `ScriptPath` | `string` | `"ssr.mjs"` | Path to the SSR entry script, resolved against `WorkingDirectory`. |
| `Port` | `int` | `13714` | Port the SSR server listens on; also derives the health, shutdown and gateway URLs. |
| `StartupTimeout` | `TimeSpan` | `30s` | How long to wait for `/health` before treating the start as failed. |
| `RestartPolicy` | `RestartPolicy` | `OnFailure` | `None` or `OnFailure`. |
| `InitialBackoff` | `TimeSpan` | `500ms` | Delay before the first restart; doubles up to `MaxBackoff`. |
| `MaxBackoff` | `TimeSpan` | `10s` | Upper bound for the restart backoff. |
| `MaxRestarts` | `int` | `5` | Maximum restarts before giving up; `-1` restarts indefinitely. |
| `WorkingDirectory` | `string?` | `null` | Working directory for the process; defaults to the app base directory. |
| `EnvironmentVariables` | `IDictionary<string, string>` | `{}` | Extra environment variables for the process. |
| `ShutdownTimeout` | `TimeSpan` | `5s` | Time to wait for a graceful `/shutdown` before killing the process tree. |

### `InertiaOptions.SsrResilience`

The `SsrResilience` object tunes the standard resilience pipeline on the SSR client (total
timeout, retry with exponential backoff and jitter, circuit breaker, attempt timeout). See
[Server-side rendering](/guide/ssr#resilience) for usage. The total budget is
`InertiaOptions.SsrTimeout`; the values below configure the individual strategies.

| Option | Type | Default | Description |
| --- | --- | --- | --- |
| `MaxRetryAttempts` | `int` | `2` | Retry attempts for transient failures within the `SsrTimeout` budget. Must be between 1 and 10; retries cannot be disabled. |
| `RetryBaseDelay` | `TimeSpan` | `200ms` | Base delay between retries; grows exponentially per attempt with added jitter. |
| `AttemptTimeout` | `TimeSpan?` | `null` | Timeout for a single attempt; defaults to `SsrTimeout`. |
| `CircuitBreakerFailureRatio` | `double` | `0.5` | Proportion of failing outcomes within the sampling window that opens the circuit. Must be > 0 and ≤ 1. |
| `CircuitBreakerMinimumThroughput` | `int` | `10` | Minimum outcomes in the sampling window before the ratio can open the circuit. |
| `CircuitBreakerSamplingDuration` | `TimeSpan` | `30s` | Window over which failures are counted. |
| `CircuitBreakerBreakDuration` | `TimeSpan` | `30s` | How long the circuit stays open before a trial call is allowed through. Must be at least 500 ms. |

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
| `HotUrl` | `string?` | `null` | Base URL of a running dev server supplied by an orchestrator such as .NET Aspire. When set, assets resolve against this URL instead of reading `HotFile`. |
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

## Binding from configuration

Both `AddInertia` and `AddViteHelper` have overloads that bind their options from an
`IConfiguration` section before applying the delegate:

```csharp
builder.Services.AddInertia(builder.Configuration, options =>
{
    // Applied after the Inertia section is bound.
});

builder.Services.AddViteHelper(builder.Configuration);
```

The `Inertia` section binds to `InertiaOptions` and the `Vite` section binds to
`ViteOptions`. This lets an orchestrator such as
[.NET Aspire](/guide/aspire) supply values through environment variables:

| Setting | Maps to |
| --- | --- |
| `Inertia__SsrEnabled` | `InertiaOptions.SsrEnabled` |
| `Inertia__SsrUrl` | `InertiaOptions.SsrUrl` |
| `Vite__HotUrl` | `ViteOptions.HotUrl` |

## Minimal API route dump

`MapInertiaRoutes()` maps the Development-only route metadata endpoint used by
`inertiatsgen routes`. It returns `404` outside Development. See
[Typed routes](/typescript/routes).

## Tool options

The `inertiatsgen` tool has its own flags; see
[Declarations options](/typescript/declarations#options) and
[Typed routes](/typescript/routes).
