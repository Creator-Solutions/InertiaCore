# InertiaCore 0.3.0

## Highlights

This release makes the front end type-aware and rounds out the Inertia protocol. Generated TypeScript declarations, typed page rendering, a typed component registry and a Ziggy-style `route()` helper let your React code share contracts with your C# props and endpoints. Alongside that, the adapter gains merge props, infinite scroll, deferred/once/rescued prop handling and Precognition validation, the Vite helper reaches parity with Laravel's, and a security audit hardened the SSR gateway, output escaping and redirect handling.

## Features

- **Prop merging.** `Inertia.Merge` / `Inertia.DeepMerge` (and fluent `.Merge()`, `.DeepMerge()`, `.Prepend()`, `.Append()`, `.MatchOn()` on any prop wrapper) mark props that the client should merge rather than replace. The page object emits `mergeProps`, `prependProps`, `deepMergeProps` and `matchPropsOn`, and `X-Inertia-Reset` lets a request replace a prop instead of merging it. Useful for appended or paginated lists.
- **Infinite scroll.** `Inertia.Scroll(items, metadata)` configures a paginated prop and emits `scrollProps` (`PageName`, `CurrentPage`, `PreviousPage`, `NextPage`). The merge intent is taken from the `X-Inertia-Infinite-Scroll-Merge-Intent` header so you do not have to hand-roll the metadata.
- **Deferred, once and rescued props.** `Defer` groups props so the client fetches them in parallel after the first render, `Once` resolves a prop a single time and reports `onceProps` with an `expiresAt`, and `.Rescue()` (or the `rescue` argument on `Defer`) logs a resolution failure, omits the value and reports it under `rescuedProps` while still returning `200`.
- **Precognition.** Real-time, server-side form validation without running the endpoint. MVC controllers are covered by a global filter and Minimal API endpoints opt in with `WithInertiaPrecognition()`. Requests carrying `Precognition: true` return `204` when valid or `422` with the same `errors` shape on failure, and `Precognition-Validate-Only` narrows the fields checked. Replace `IInertiaRequestValidator` to plug in your own validation engine.
- **Protocol extras.** History fields (`encryptHistory`, `clearHistory`) are now serialized; a non-prefetch redirect whose target contains a fragment returns `409` with `X-Inertia-Redirect`; `HttpContext.PreserveInertiaFragment()` carries a fragment across an ordinary redirect; `HttpContext.IsInertiaPrefetch()` detects prefetch visits; top-level shared props are announced as `sharedProps`; and flash data is persisted through `IInertiaFlashProvider` (TempData-backed and replaceable) so it survives a redirect.
- **Typed page contracts (`Render<T>`).** Annotate a props record with `[InertiaPage]` and render it by type with `Inertia.Render<T>(props)` or `IInertia.Render<T>`; the component name is resolved from the generated registry with no runtime reflection. An analyzer flags `Render<T>` on a type that has no `[InertiaPage]`, and duplicate component names are reported as well.
- **TypeScript declarations.** The `inertiatsgen` dotnet tool reads your compiled assembly through `MetadataLoadContext` (your code is never executed) and writes TypeScript for every `[InertiaPage]` type plus an `InertiaPageProps` component-to-props map. It follows your serializer settings (camelCase, `[JsonPropertyName]`, `[JsonIgnore]`, `[JsonExtensionData]`, enum unions) and turns `Lazy`/`Defer`/`Optional` members into optional properties. `--check` fails when the declarations are stale.
- **Typed component registry and React helper.** Mark your shared props record with `[InertiaSharedProps]` and the interface is emitted alongside the page declarations; `--emit-react-helper` adds a typed `usePageProps(component)` hook. The component-name map is formalized so your resolver and page components are checked against the C# side.
- **Typed routes.** Named Minimal API endpoints are dumped as JSON and turned into a `routes.ts` module with a `route('users.show', { id: 5 })` helper. Unknown route names and missing required parameters are compile errors, extra values become a query string, and route constraints drive the parameter types.

## Improvements

**Security hardening**

- **SSR gateway limits and fallback.** The gateway applies a timeout (`InertiaOptions.SsrTimeout`, default 5s), rejects non-2xx and oversized responses (`InertiaOptions.SsrMaxResponseBytes`, default 10MB), only accepts absolute `http`/`https` URLs and guards null fields. A failed or timed-out render logs a warning and falls back to client-side rendering instead of returning a 500.
- **Output escaping.** Page JSON is escaped for the script context independently of the host's JSON encoder, so `<`, `>`, `&`, U+2028 and U+2029 stay safe even under relaxed encoders, and the Vite helper HTML-encodes attribute keys and values and emits the prefetch event as a serialized string.
- **Redirect and header validation.** `X-Inertia-Location` no longer decodes the request path and strips CR/LF, and the Referer back-redirect rejects relative values with control characters or whitespace that could normalize to a protocol-relative URL.
- **Header checks on partial reloads.** A partial reload now requires `X-Inertia: true`; a full-page request that carries partial headers is no longer treated as a partial reload.
- **Version resolver lifetimes and manifest caching.** `AddInertiaVersion(Func<...>)` and `AddInertiaVersion<TResolver>()` register their resolver as scoped rather than singleton, and the Vite manifest hash is read through the manifest cache instead of re-reading and hashing on every call.
- **CI and supply chain hygiene.** GitHub Actions are pinned to full commit SHAs, and stale committed build artifacts and IDE metadata were removed and added to `.gitignore`.
- A `SECURITY.md` documents supported versions, private vulnerability reporting and scope, and the accompanying audit report records what was checked and fixed.

- **Vite helper parity with Laravel.** Recursive import resolution with `<link rel="modulepreload">`, preload tags for every entry and imported chunk, multiple entry points in one call, CSP nonces, subresource integrity hashes, custom tag attribute resolvers, prefetch strategies (`None` / `Waterfall` / `Aggressive`), a public async/hot/asset API, and `GetManifestHash()` returning `null` while the dev server is hot.
- **Documentation.** New guidance on CSRF/antiforgery and on validating `Inertia.Location` and other redirect targets, plus sections covering the type generation tooling and typed pages.

## Fixes

- Flash data set before a redirect now appears on the target page; previously it was only available on the current request.
- Version mismatch is now reported only for GET requests, so non-GET requests proceed instead of receiving a `409`.
- Control `409` responses no longer include the `X-Inertia` header, matching the protocol.

## Upgrade notes

- A failed or timed-out SSR request now falls back to client-side rendering instead of returning a 500.
- Version resolvers registered by `AddInertiaVersion` are now scoped rather than singleton. If you relied on the singleton lifetime, register your provider explicitly.
- Partial reloads require the `X-Inertia: true` header; a full-page request that carries partial headers is no longer filtered.
- Vite tag output changed: attributes are encoded, tags use Laravel's attribute ordering and include preload tags, and the prefetch event is emitted double-quoted. Update any exact-string test expectations.
- `Vite.GetManifestHash()` returns `null` while the dev server is hot.
- Two new public `InertiaOptions` members (`SsrTimeout`, `SsrMaxResponseBytes`) ship with defaults.
