# Changelog

## v0.3.0 — Merging Props (IN-49)

Adds support for Inertia's prop-merging protocol, the foundation for infinite
scroll and pagination packages. No breaking changes.

### Added

- `Inertia.Merge(value)` / `inertia.Merge(...)` and `Inertia.DeepMerge(value)` /
  `inertia.DeepMerge(...)` create merge props. Both accept a value or a sync/async factory.
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
- `Inertia.Scroll(items, metadata)` / `inertia.Scroll(...)` configure a paginated prop
  for infinite scroll. `ScrollMetadata` (a plain public record with `PageName`,
  `PreviousPage`, `NextPage`, `CurrentPage`) is emitted under `scrollProps`, and the
  inner array (`data` by default) is labelled for append or prepend merge based on the
  `X-Inertia-Infinite-Scroll-Merge-Intent` request header. `X-Inertia-Reset` sets the
  entry's `reset` flag. Null tokens are serialized as `null`.

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

### What's Next (v0.3.0+)

- Additional Inertia protocol coverage
- Improved developer tooling
- More ASP.NET Core integrations
- Additional examples and templates
