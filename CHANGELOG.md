# Changelog

## Unreleased — Typed Routes (IN-57)

Generates a typed `routes.ts` module (a Ziggy-style `route()` helper) from named Minimal
API endpoints, so the front end builds URLs from a checked API instead of string literals.
No breaking change to existing public API (all additions are purely additive).

### Added

- **`MapInertiaRoutes()`** (`InertiaCore.Extensions`) — maps a Development-only endpoint
  (default `/_inertia/routes`) that dumps every named Minimal API route as JSON. It returns
  `404` outside the Development environment.
- **`GetInertiaRoutes()`** — collects named `RouteEndpoint`s (via `RouteNameMetadata`) into
  `InertiaRoute`/`InertiaRouteParameter` metadata, with route constraints, optional and
  catch-all flags, ordered by name for deterministic output. Endpoints without a name are
  ignored.
- **`inertiatsgen routes`** subcommand — reads the dump (`--input`) and writes a typed
  `routes.ts` (`--output`, default `Client/types/routes.ts`). Supports `--check` for CI.
  The generated module exports `InertiaRouteName`, `InertiaRouteParams`, `inertiaRouteDefs`
  and `route(name, params)`: unknown route names and missing required parameters are compile
  errors, while extra values are emitted as a query string.
- **`InertiaRouteUrlBuilder`** — a server-side mirror of the generated `route()` helper for
  asserting the correct URLs in unit tests.

### Notes

- Two metadata sources are supported and documented in the README: the runtime
  Development dump (used by `inertiatsgen routes`) and the startup host dump, with the
  trade-offs of each.
- The `InertiaCore.AspNetCore` package is unchanged and still ships the source generator
  exactly once.

---

## Unreleased — Typed Registry, React Helper & Shared Props (IN-56)

Makes the generated declarations usable by the React side: the component-name map is
formalised, a typed `usePageProps` helper is available behind an opt-in setting, and a
shared props record can be declared so its shape is emitted too. No breaking change to
existing public API (both additions are purely additive).

### Added

- **`[InertiaSharedProps]` attribute.** Mark the record describing your shared props and
  the tool emits it as an `InertiaSharedProps` interface alongside the page declarations.
  It is a code-generation marker only — continue to populate values with `Inertia.Share(...)`.
  Multiple annotated types warn and fall back to the first (deterministic) one.
- **React helper behind `--emit-react-helper`** (also `TypeScriptGeneratorOptions.EmitReactHelper`,
  default `false`). When enabled, the output imports `usePage` from `@inertiajs/react` and
  exports `usePageProps<K extends keyof InertiaPageProps>(component: K)`, returning the
  component's props intersected with `InertiaSharedProps` when a shared record exists.
  Projects not using React hooks are unaffected by default.
- **`samples/react`** — a committed sample app whose `app.tsx` types the Inertia page
  resolver with `InertiaPageProps` and whose components are typed from the registry. CI
  regenerates the declarations, checks they compile under `tsc --strict`, and verifies that
  renaming a props member produces a compile error in the consumer.

### Notes

- The `InertiaCore.AspNetCore` package is unchanged and still ships the source generator
  exactly once.

---

## Unreleased — TypeScript Declarations (IN-55)

Adds `InertiaCore.TypeGen`, a dotnet tool that reads a compiled assembly and writes
TypeScript declarations for every `[InertiaPage]` type and the types those props
reference, catching contract drift at build time. No breaking change to existing
public API.

### Added

- **`InertiaCore.TypeGen` dotnet tool** (`inertiatsgen`). Loads the target assembly
  with `MetadataLoadContext` (user code is never executed) and emits TypeScript
  interfaces plus an `InertiaPageProps` component-to-props map.
- **Options:** `--assembly`, `--output` (default
  `Client/types/inertia.generated.d.ts`), `--reference`, `--enum-mode`
  (`auto`/`string`/`number`), `--no-camel-case`, `--check` and `--watch`.
- **Serializer-aware mapping:** camelCase naming, `[JsonPropertyName]`,
  `[JsonIgnore]` (including `WhenWritingNull`/`WhenWritingDefault` becoming optional
  properties), `[JsonExtensionData]`, and enum string/number unions driven by
  `[JsonConverter]`. `Lazy`, `Defer` and `Optional` wrapper members become optional.
- **Deterministic, auto-generated header**, cyclic-reference safe, with `--check`
  for CI gating.
- **Golden-file tests** plus unit tests for the mapping rules; a `WebApplicationFactory`
  integration test asserts the sample endpoint's JSON matches the declared shape.

### Notes

- The generator is a separate tool package; the `InertiaCore.AspNetCore` package is
  unchanged and still ships the source generator exactly once.

---

## Unreleased — Typed Page Contracts (IN-54)

Makes a props record the page contract: a type annotated with `[InertiaPage]` can be
rendered by type, resolving the component name from the generated registry. No
breaking change to existing public API.

### Added

- **`Render<TProps>(TProps props)`** on the static `Inertia` facade and on `IInertia`
  (added as a default interface method, so existing implementers keep compiling). The
  component name is resolved from `InertiaPageRegistry` by the props type.
- **Type-to-component table.** The generator emits a `Type → component` dictionary
  (populated from `typeof(...)`, no runtime reflection) alongside the existing
  constants and registers it through the module initializer.
- **`InertiaPageRegistry` type mapping:** `Register(Type, string)`,
  `GetComponent(Type)` and `TryGetComponent(Type, out string)`.
- **`INERTIA005`** — duplicate component name across distinct types.
- **`INERTIA006`** (analyzer) — `Inertia.Render<T>` / `IInertia.Render<T>` used with a
  type that has no `[InertiaPage]` (compile-time error; a clear
  `InvalidOperationException` is the runtime fallback).

### Notes

- `[InertiaPage]` on props records works as on classes; wrapper members
  (`DeferredProp`, `LazyProp`, `AlwaysProp`, …) are allowed on the record.
- A type with multiple `[InertiaPage]` attributes has no unambiguous mapping and is
  omitted from the type table (`Render<T>` throws for it); use the string overload.

---

## Unreleased — Protocol Extras (IN-53)

Adds the remaining small Inertia protocol features: history-field serialization,
fragment redirects, prefetch detection, shared-props metadata, flash persistence and
full version-`409` compliance.

### Added

- **History fields.** `encryptHistory` and `clearHistory` are serialized into the page
  object when set via `Inertia.EncryptHistory()` / `Inertia.ClearHistory()` (they were
  previously omitted).
- **Fragment redirects.** A non-prefetch redirect whose `Location` contains a URL
  fragment now returns `409 Conflict` with `X-Inertia-Redirect` (the full URL), handled
  centrally for MVC and Minimal APIs.
- **`preserveFragment`.** `HttpContext.PreserveInertiaFragment()` carries the original
  request's fragment across an ordinary redirect; the target page object emits
  `preserveFragment: true`.
- **Prefetch detection.** `HttpContext.IsInertiaPrefetch()` (`Purpose: prefetch`).
- **`sharedProps` metadata.** Top-level `Inertia.Share` keys are emitted as
  `sharedProps` for instant visits.
- **`IInertiaFlashProvider`** (default `InertiaFlashProvider`, TempData-backed) with a
  `TryAddScoped` registration so a custom flash store can replace it.
- `InertiaHeader.Purpose` and `InertiaHeader.Redirect` constants.

### Changed

- **Version mismatch is now `GET`-only** (matching the Inertia protocol and Laravel).
  Non-`GET` requests proceed instead of receiving a `409`; the `409` also echoes
  `X-Inertia-Version` and reflashes flash/errors for the follow-up request.
- **Control `409` responses no longer include `X-Inertia`** (per the protocol).
- **Flash is persisted for the next request** through `IInertiaFlashProvider`, so flash
  set before a redirect is shown on the target page. Previously flash only appeared on
  the current request. Prefetch requests do not consume it.

---

## Unreleased — Precognition (IN-52)

Adds support for Inertia.js Precognition: real-time, server-side form validation
without running the endpoint. No breaking changes.

### Added

- **Precognition request detection.** Requests carrying `Precognition: true` are
  detected via `InertiaHeader.Precognition`, and the `Precognition-Validate-Only`
  header limits the returned errors to the listed fields.
- **MVC support.** `InertiaPrecognitionFilter` validates `ModelState`, records the
  errors in the Inertia error bag, and short-circuits with `204` (valid) or `422`
  (invalid) before the action runs. It is registered globally by `AddInertia()` and
  runs before MVC's automatic `400`-on-invalid-model filter.
- **Minimal API support.** `InertiaPrecognitionEndpointFilter` (enable per endpoint
  with `WithInertiaPrecognition()`) validates the bound arguments and returns
  `204`/`422` without invoking the handler. Endpoint filters run after argument
  binding, so keep side effects inside the handler.
- **Shared validation abstraction.** `IInertiaRequestValidator` with a default
  `DataAnnotationsRequestValidator`; replace the registration to integrate a
  different validation engine.
- Precognition responses set `Precognition: true` (and `Precognition-Success: true`
  on success) and append `Vary: Precognition`. The `422` body uses the same
  `errors` shape as the Inertia `errors` prop (flattened default bag, named bag
  nested under its name).
- `InertiaHeader.Precognition`, `InertiaHeader.PrecognitionValidateOnly` and
  `InertiaHeader.PrecognitionSuccess` constants.

---

## v0.3.0

Adds Inertia's prop-merging, infinite-scroll, deferred, once and rescued prop
protocols; brings the Vite helper to feature parity with Laravel's
`Illuminate\Foundation\Vite` (recursive import resolution, preload tags, CSP
nonces, integrity hashes, tag attribute resolvers, prefetch strategies and a
public async/hot/asset API); and hardens the adapter following an internal
security audit. No public API is removed, but some behaviour changed — see below.

### Added

- **Merge props.** `Inertia.Merge(value)` / `inertia.Merge(...)` and
  `Inertia.DeepMerge(value)` / `inertia.DeepMerge(...)` create merge props. Both
  accept a value or a sync/async factory.
- Fluent modifiers on every prop wrapper: `.Merge()`, `.DeepMerge()`, `.Prepend()`,
  `.Append(params string[])`, `.Prepend(params string[])` and `.MatchOn(params string[])`.
  This allows composition such as `_inertia.Defer(() => LoadUsers()).Merge()`.
- The page object now emits `mergeProps`, `prependProps`, `deepMergeProps` and
  `matchPropsOn` arrays. Empty arrays are omitted, matching the Laravel adapter.
- `InertiaHeader.Reset` (`X-Inertia-Reset`): props listed in this request header are
  returned in `props` but excluded from every merge array, so the client replaces
  instead of merges them.
- Merge metadata is computed after `X-Inertia-Partial-Data` / `-Except` filtering, so a
  partial reload only advertises merge props it actually returned.
- Metadata keys are camelCased the same way as prop names (`TestMerge` → `testMerge`,
  `MatchOn("data.id")` → `posts.data.id`).
- **Infinite scroll.** `Inertia.Scroll(items, metadata)` / `inertia.Scroll(...)` configure a
  paginated prop for infinite scroll. `ScrollMetadata` (a plain public record with
  `PageName`, `PreviousPage`, `NextPage`, `CurrentPage`) is emitted under `scrollProps`, and
  the inner array (`data` by default) is labelled for append or prepend merge based on the
  `X-Inertia-Infinite-Scroll-Merge-Intent` request header. `X-Inertia-Reset` sets the entry's
  `reset` flag. Null tokens are serialized as `null`.
- **Deferred props.** `Defer(callback, group)` / `Defer(callback, group, rescue)` group
  deferred props and announce them under `deferredProps`, so the client fetches groups in
  parallel. Callbacks are never invoked for props that are excluded by a partial reload.
- **Once props.** `Inertia.Once(callback)` / `inertia.Once(...)` resolve a prop a single time
  and emit `onceProps` metadata (`prop` + `expiresAt`). Requests that already hold a once prop
  (listed in `X-Inertia-Except-Once-Props`) skip resolution. Fluent `.Once()`, `.As(key)`,
  `.Fresh()` and `.Until(...)` compose with merge, deferred and lazy props.
- **Rescued props.** `DeferredProp.Rescue()` (and the `rescue` argument to `Defer`) rescues
  resolution failures: the exception is logged, the value omitted, and the key reported
  through the page object's `rescuedProps` array while the response stays `200`. Non-rescued
  exceptions still fail the request.
- **Multiple Vite entry points.** `Vite.Input(...)` / `Vite.InputAsync(...)` and the builder
  equivalents now accept `params string[]` and render tags for every entry point in a single call.
- **Recursive imports and module preloading.** Manifest `imports` are walked and emitted as
  `<link rel="modulepreload">`, and every imported chunk's `css` is emitted as a
  `<link rel="preload" as="style">` plus its stylesheet.
- **Asset preloading.** Every entry point and imported chunk now emits a preload tag;
  stylesheet preloads are ordered before script preloads.
- **CSP nonce support.** `ViteOptions.CspNonce` (static) and `ViteOptions.NonceResolver`
  (per-request) apply a `nonce` attribute to every generated script, stylesheet and preload tag.
- **Subresource integrity.** `ViteOptions.IntegrityKey` (default `"integrity"`, set to `null`
  to disable) reads the hash from the manifest chunk and emits an `integrity` attribute.
- **Tag attribute resolvers.** `ViteOptions.ScriptTagAttributes`, `StyleTagAttributes` and
  `PreloadTagAttributes` allow appending custom attributes. A preload resolver returning
  `null` skips the tag.
- **Prefetch strategies.** `ViteOptions.PrefetchStrategy`
  (`PrefetchStrategy.None` / `Waterfall` / `Aggressive`), `PrefetchConcurrency` and
  `PrefetchEvent` render a prefetch script for manifest dynamic imports, mirroring Laravel's
  `prefetch()`, `useWaterfallPrefetching()` and `useAggressivePrefetching()`.
- **Public async and helper API on the `Vite` facade:** `InputAsync`, `ReactRefreshAsync`,
  `IsRunningHot`, `Asset` and `Content`, alongside the existing `Input`, `ReactRefresh` and
  `GetManifestHash`.
- `InertiaOptions.SsrTimeout` and `InertiaOptions.SsrMaxResponseBytes`.
- New `ViteChunk`, `PrefetchStrategy`, `ViteScriptTagAttributesResolver`,
  `ViteStyleTagAttributesResolver` and `VitePreloadTagAttributesResolver` types.

### Changed

- **SSR failure falls back to client-side rendering.** A failed or timed-out SSR request now
  logs a warning and renders the client-side script tag instead of throwing a 500.
- **`AddInertiaVersion` resolvers are scoped.** `AddInertiaVersion(Func<IServiceProvider, string>)`
  and `AddInertiaVersion<TResolver>()` now register `IInertiaVersionProvider` /
  `IInertiaVersionResolver` as scoped (previously singleton), preventing request-scoped
  dependencies from being shared between requests.
- **Partial reloads require `X-Inertia: true`.** A full-page request that carries partial
  headers is no longer treated as a partial reload.
- **Vite prefetch event uses a JSON string literal.** The event name is emitted double-quoted
  (`"load"`) instead of single-quoted, and nonce/attribute values are HTML-encoded. Existing
  exact-string expectations were updated.
- The Vite helper is no longer marked `[Obsolete]`; the sync methods remain for compatibility
  and the async variants use cached manifest reads.
- Generated Vite tags now use Laravel's attribute ordering (`type` before `src`, `rel` before
  `href`) and include preload tags. Existing exact-string expectations should be updated.
- `Vite.GetManifestHash()` now returns `null` while the dev server is running (hot), matching
  Laravel, instead of hashing the stale manifest.
- The CSS path detection regex is now anchored and matches Laravel's list
  (`css|less|sass|scss|styl|stylus|pcss|postcss`).

### Security

- **SSR gateway hardened (SC-01/SC-14).** `Gateway` applies a timeout
  (`InertiaOptions.SsrTimeout`, default 5 s), rejects non-2xx and oversized responses
  (`InertiaOptions.SsrMaxResponseBytes`, default 10 MB), only accepts absolute
  `http`/`https` SSR URLs, and guards null `head`/`body`.
- **`X-Inertia-Location` no longer decodes the request path (SC-03).** The encoded path/query
  is used verbatim with CR/LF stripped.
- **Referer back-redirect hardened (SC-04).** Relative `Referer` values containing control
  characters or whitespace, or that can normalise to a protocol-relative URL, are rejected.
- **Page JSON escaped for the script context (SC-07).** `<`, `>`, `&`, U+2028, U+2029 and `/`
  are escaped independently of the host's JSON encoder.
- **Vite tag attributes encoded (SC-06).** Attribute keys/values are HTML-encoded and the
  prefetch event is emitted as a serialized string.
- **Vite manifest hash cached (SC-10).** `GetManifestHash()` uses the manifest cache instead
  of re-reading and hashing the file on every call.

---

## v0.2.2 — Security & Reliability Hardening

This release fixes cross-request data leakage, an open redirect, packaging/CI issues, and several correctness problems. It also completes the source generator.

### Security

- **Request-scoped sharing is now isolated.** `Inertia.Share(...)` called during a request writes to that request's scope only. Called outside a request (startup), it writes to a thread-safe global registry. Previously a single static `Dictionary` was shared by every request and user, and could be corrupted under concurrency.
- **The static facade no longer captures a scoped service.** `Inertia.Render/Head/Html/Share/Version` resolve `IResponseFactory`/`IInertia` from the current request scope. Previously a scoped factory (and its per-request state) was captured at startup and shared by all requests.
- **Open redirect fixed.** The empty-response redirect only honours a `Referer` that is a safe relative path or matches the current scheme, host and port. Protocol-relative (`//host`) and backslash-prefixed forms are rejected; anything else falls back to the request path.
- **`AddInertia()` works with no version configured.** The version now defaults to an empty string (protocol-valid) instead of throwing on the first request.

### Fixed

- `UseInertia()` now registers `InertiaMiddleware`, so empty-response redirects and `X-Inertia-Version` response headers actually run.
- Minimal API hosts can resolve `IResponseFactory` without MVC's `JsonOptions` registered.
- The source generator supports `[InertiaPage]` on classes as well as assemblies, attaches real source locations to diagnostics, and registers declared pages with `InertiaPageRegistry` at module load. Opt in to runtime validation with `InertiaOptions.ValidatePages`.
- The source generator no longer reports false `INERTIA002` duplicate-component errors when assembly attributes are spread across multiple files or when `[InertiaPage]` is applied to several parts of a partial class. `[InertiaPage]` is now also supported on `record` types.
- The source generator is now packaged into `InertiaCore.AspNetCore` under `analyzers/dotnet/cs` using the supported MSBuild pattern.
- CI now installs .NET 8/9/10 and matches `global.json`.

### Changed

- Empty responses are converted to redirects for `204`, or `200` with no content type and a null or zero content length.

---

## v0.2.0 — Protocol Compliance & Laravel Feature Parity

This release brings the adapter in line with the official Inertia.js protocol specification and adds several features that match the Laravel adapter's developer experience — shared global data, strict header validation, proper redirect semantics, named error bags, and clean JSON output.

---

### Global Shared Data (`Inertia.Share`)

Data you register once at startup is now automatically merged into every Inertia response. The merge order is: **global → request-scoped → component props**. Closures in global data are resolved per-request.

```csharp
// Register at startup
Inertia.Share("appName", "My App");
Inertia.Share("auth", new { User = new { Name = "Jane" } });

// Or bulk
Inertia.Share(new Dictionary<string, object?>
{
    ["appName"] = "My App",
    ["auth"] = new { User = new { Name = "Jane" } }
});
```

Every `Inertia.Render(...)` call now includes these values automatically — no need to pass them manually each time.

---

### Named Validation Error Bags (`X-Inertia-Error-Bag`)

Supports the full Inertia.js error bag protocol. The **default bag** flattens errors to match Laravel's behaviour (`{ errors: { field: [...] } }`), while **named bags** nest under the bag name (`{ errors: { myForm: { field: [...] } } }`).

```csharp
// In your controller
public IActionResult Submit(MyModel model)
{
    if (!ModelState.IsValid)
    {
        HttpContext.Request.Headers["X-Inertia-Error-Bag"] = "myForm";
        return Inertia.Render("Form");
    }
    // ...
}
```

**Before (v0.1.x):**
```json
{ "errors": { "default": { "email": ["The email field is required."] } } }
```

**After (v0.2.0) — default bag:**
```json
{ "errors": { "email": ["The email field is required."] } }
```

**After (v0.2.0) — named bag:**
```json
{ "errors": { "myForm": { "email": ["The email field is required."] } } }
```

---

### POST Redirect → 303 See Other

The Inertia protocol requires that non-GET redirects return a `303 See Other` so the browser issues a fresh GET. This now includes **POST** requests, matching the Laravel adapter.

```csharp
[HttpPost]
public IActionResult Store(Model model)
{
    // Redirect is converted to 303 See Other automatically
    return RedirectToAction("Index");
}
```

`POST`, `PUT`, `PATCH`, and `DELETE` all produce `303`.

---

### Strict `X-Inertia` Header Validation

Only the literal value `"true"` is accepted as a valid Inertia request. Values like `"false"`, `"1"`, or an empty header are now treated as non-Inertia requests, matching the protocol spec precisely.

```csharp
// Internally changed from:
bool.TryParse(header, out var result) && result;

// To:
header == "true";
```

---

### Clean JSON: Non-Protocol Fields Removed

`EncryptHistory` and `ClearHistory` were leaking into the JSON response body even though they are internal C# properties, not part of the Inertia.js page protocol. They are now excluded via `[JsonIgnore]`.

**Before (v0.1.x) — serialized Page JSON:**
```json
{
    "component": "Dashboard",
    "props": {},
    "url": "/dashboard",
    "version": "1.0",
    "encryptHistory": false,
    "clearHistory": false
}
```

**After (v0.2.0) — serialized Page JSON:**
```json
{
    "component": "Dashboard",
    "props": {},
    "url": "/dashboard",
    "version": "1.0"
}
```

Only `component`, `props`, `url`, and `version` are serialized — exactly what the Inertia.js protocol defines.

---

### What's Next

- Additional Inertia protocol coverage
- Improved developer tooling
- More ASP.NET Core integrations
- Additional examples and templates
