# InertiaCore Security Audit

Audit of `InertiaCore.AspNetCore` (ASP.NET Core Inertia.js adapter) on branch
`security/audit-fixes`. The codebase was revived from an abandoned repository and
treated as unreviewed. Phase 1 was read-only; Phase 2 applied the approved fixes,
one per commit.

Baseline before changes: **185 tests passing** (net8.0 / net9.0 / net10.0).
After all changes: **229 tests passing** (net8.0 / net9.0 / net10.0).

> Note: the working tree at audit time contained uncommitted Vite nonce/integrity/
> prefetch work. SC-06 was found to be a defect introduced by that work and was only
> reproducible once it was committed (`c2e7e2e`), so SC-06 is fixed on top of it.

## Summary

| ID | Title | Severity | Status | Commit |
|----|-------|----------|--------|--------|
| SC-01 | SSR gateway had no timeout, status validation or response-size cap | Medium | Fixed | `743b5df` |
| SC-02 | No CSRF/XSRF integration or guidance | Low | Documented | `0b71913` |
| SC-03 | CRLF injection risk via decoded request URI in `X-Inertia-Location` | Low | Fixed | `397be51` |
| SC-04 | Referer back-redirect: control-char / normalisation bypass | Low | Fixed (hardening; not reproducible via test host) | `f029206` |
| SC-05 | Singleton version resolvers captured the root `IServiceProvider` | Low | Fixed | `8724fd0` |
| SC-06 | Vite tag attribute values / prefetch event not encoded | Low | Fixed | `628cf46` |
| SC-07 | Page JSON embedded in `<script>` with slash-only escaping | Low | Fixed | `8929f3f` |
| SC-08 | Partial filtering did not require `X-Inertia: true` | Low | Fixed | `1915d85` |
| SC-09 | `Location`/redirect helpers do not validate URLs | Low | Documented | `0b71913` |
| SC-10 | `Vite.GetManifestHash()` read + hashed the manifest every call | Low | Fixed | `e87d176` |
| SC-11 | GitHub Actions pinned to mutable tags | Low/Info | Fixed | `dc67f18` |
| SC-12 | Committed build artifact and IDE metadata | Info | Fixed | `dc67f18` |
| SC-13 | Outdated dependencies | Info | Deferred | — |
| SC-14 | SSR/JSON null and error handling | Info | Fixed (with SC-01) | `743b5df` |
| SC-15 | `Inertia.Share` silently writes global registry outside a request | Info | Deferred | — |

---

## SC-01 — SSR gateway: timeout, status validation, size cap, safe fallback

**Severity:** Medium (resilience / availability; raw HTML injection is inherent to SSR)
**Status:** Fixed — `743b5df`
**Location:** `InertiaCore/Ssr/Gateway.cs`, `InertiaCore/Ssr/SsrResponse.cs`, `InertiaCore/Models/InertiaOptions.cs`

**What it did.** `Dispatch` serialized the page, `PostAsync` to `SsrUrl`, and
`ReadFromJsonAsync<SsrResponse>` with no timeout, no `EnsureSuccessStatusCode`, and
no response-size limit. A hung renderer could stall many requests (default 100 s),
and a non-2xx or oversized body was still parsed. The returned `Body` was injected
as raw HTML.

**Exploitation.** Denial of service against the app if the SSR renderer is slow or
hung; SSRF requires control of `SsrUrl` configuration. Raw SSR HTML trust is by
design.

**Fix.** Added `InertiaOptions.SsrTimeout` (default 5 s) and
`InertiaOptions.SsrMaxResponseBytes` (default 10 MB). `Gateway` now validates the
URL is absolute `http`/`https`, applies a `CancellationTokenSource` timeout, rejects
non-2xx responses, reads the body through a bounded reader, rejects a null `Body`,
and returns `null` (logging a warning) on every failure so `ResponseFactory.Html`
falls back to client-side rendering instead of throwing 500. `SsrResponse.GetBody`
/ `GetHead` guard nulls (SC-14).

**Behaviour change.** SSR failures now fall back to client-side rendering instead
of propagating an exception. New `InertiaOptions` properties are public API
(additive).

## SC-02 — CSRF / XSRF

**Severity:** Low (integration gap, not a code defect)
**Status:** Documented — `0b71913`
**Location:** whole library (no antiforgery/XSRF handling)

**What it did.** Inertia's `XSRF-TOKEN` cookie / `X-XSRF-TOKEN` header convention is
not implemented, and ASP.NET Core antiforgery is not wired up or documented, so an
app could ship unprotected state-changing endpoints.

**Fix.** Added a README "CSRF / Antiforgery" section covering both the ASP.NET Core
antiforgery service (with `HeaderName`, cookie flags, `GetAndStoreTokens`, shared
prop, and server-side validation) and Inertia's XSRF cookie/header convention,
including `HttpOnly`/`Secure`/`SameSite` guidance. No library code changed.

## SC-03 — `X-Inertia-Location` path decoding

**Severity:** Low (unsanitised control characters; on Kestrel likely a 500)
**Status:** Fixed — `397be51`
**Location:** `InertiaCore/Extensions/InertiaExtensions.cs`, `InertiaCore/Extensions/Configure.cs`

**What it did.** `OnVersionChange` wrote `RequestedUri()` (which runs
`Uri.UnescapeDataString` on the encoded path) into `X-Inertia-Location`. A path
containing `%0d%0a` was decoded to CR/LF before being written to a header.

**Verification (recorded as required).** Against the ASP.NET Core TestServer, an
encoded-CRLF path produced the single header value
`X-Inertia-Location: "/some\nX-Injected: yes"` with status `409` — i.e. a header
value containing a literal newline, **not** a separate injected header. So on this
host it is unsanitised control characters / latent response-splitting risk rather
than a confirmed split. Kept at Low.

**Fix.** Added `RequestedLocation()` which uses the encoded path/query verbatim and
strips CR/LF, and used it for `X-Inertia-Location`. `Page.Url` continues to use the
decoded `RequestedUri()` (JSON context). Added `UnitInertiaLocationEncoding`.

## SC-04 — Referer back-redirect

**Severity:** Low
**Status:** Fixed (hardening) — `f029206`
**Location:** `InertiaCore/Middleware/InertiaMiddleware.cs`

**What it did.** `GetSafeBackUrl` rejected `//`, `/\` and `\` prefixes but returned
other relative `Referer` values verbatim. Browsers strip literal tab/newline
characters, so `/\t/evil` could normalise to `//evil` (open redirect).

**Verification.** The literal-control-character path is **not reproducible through
the test host**: `HttpClient` percent-encodes the tab before it reaches the server
(the server received `/%09/evil.example`, which does not normalise to a
protocol-relative URL). The finding was therefore downgraded rather than treated as
a confirmed exploit.

**Fix.** `GetSafeBackUrl` now rejects any relative value containing control
characters or whitespace, in addition to the protocol-relative prefixes.
`GetSafeBackUrl` was made `internal` to allow direct unit tests (no public API
change). Added `UnitBackRedirect`.

## SC-05 — Version resolver lifetimes

**Severity:** Low (captive dependency; could become cross-request data sharing)
**Status:** Fixed — `8724fd0`
**Location:** `InertiaCore/Extensions/ServiceCollectionExtensions.cs`

**What it did.** `AddInertiaVersion(Func<IServiceProvider, string>)` and
`AddInertiaVersion<TResolver>()` registered the resolver/provider as **singletons**
capturing the root `IServiceProvider`. Any request-scoped dependency used by the
factory was created once at the root and shared between requests.

**Verification.** Added `UnitInertiaVersionLifetime`, which failed before the fix
(the same scoped instance was observed across two scopes) and passes after.

**Fix.** Both overloads now register `IInertiaVersionProvider` /
`IInertiaVersionResolver` as **scoped**, so the captured provider is the request
scope. Behaviour change (lifetime) noted in the CHANGELOG.

## SC-06 — Vite tag attribute encoding

**Severity:** Low (context-dependent; introduced by the uncommitted Vite work)
**Status:** Fixed — `628cf46`
**Location:** `InertiaCore/Utils/Vite.cs`

**What it did.** The Vite feature rendered attributes by string interpolation
(`$"{key}=\"{value}\""`) with no encoding, and interpolated `PrefetchEvent` into a
single-quoted JS string. A quote in a nonce, integrity hash, or a developer-supplied
attribute resolver value (or the event name) could break out of its context.

**Verification.** Added `UnitTestViteEncoding`; all four tests failed before the fix
(a hostile nonce produced `nonce="abc" onload="alert(1)"`).

**Fix.** Attribute keys and values are encoded with `HtmlEncoder.Default`; the nonce
attribute uses a shared encoded helper; `PrefetchEvent` is emitted via
`JsonSerializer.Serialize` (double-quoted, escaped). Two existing prefetch tests
were updated from `'load'` to `"load"`. Behaviour change noted.

## SC-07 — Page JSON script-context escaping

**Severity:** Low
**Status:** Fixed — `8929f3f`
**Location:** `InertiaCore/ResponseFactory.cs`

**What it did.** The initial page object was serialized with the host's
`JsonSerializerOptions` and only `/` was replaced. This is safe with the default
encoder, but a host using `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` left `<`,
`>`, `&`, U+2028 and U+2029 unescaped.

**Verification.** Added `UnitTestHtmlEscaping` using `UnsafeRelaxedJsonEscaping`
with `</script>`, `<!--<script>`, `<img onerror>`, `&`, U+2028 and U+2029 payloads;
five tests failed before the fix.

**Fix.** Added `EscapeJsonForHtmlScript`, which escapes `<`, `>`, `&`, U+2028,
U+2029 and `/` independently of the host encoder. Output for simple payloads is
unchanged. Behaviour change (output encoding) noted.

## SC-08 — Partial reload detection

**Severity:** Low
**Status:** Fixed — `1915d85`
**Location:** `InertiaCore/Extensions/InertiaExtensions.cs`

**What it did.** `IsInertiaPartialComponent` matched only
`X-Inertia-Partial-Component`, without requiring `X-Inertia: true`, so a plain
full-page request carrying partial headers was treated as a partial reload (applying
`only`/`except` and skipping first-load exclusion of lazy/deferred props).

**Verification.** Applying the fix caused 16 existing partial-reload unit tests to
fail (they relied on the unsafe behaviour); those tests now send `X-Inertia: true`,
matching real protocol usage. Added a full-page regression test
(`TestPartialHeadersIgnoredForNonInertiaRequest`).

**Fix.** `IsInertiaPartialComponent` now requires `context.HttpContext.IsInertiaRequest()`.
Behaviour change noted.

## SC-09 — Redirect URL validation

**Severity:** Low (app-dependent open redirect)
**Status:** Documented — `0b71913`
**Location:** `InertiaCore/Utils/LocationResult.cs`, `InertiaCore/Utils/InertiaActionFilter.cs`

**What it did.** `LocationResult` writes the caller-supplied URL to
`X-Inertia-Location` with no validation; `InertiaActionFilter` rewrites `Location`
from redirect results. This is standard `Location`/`Inertia.Location` behaviour, but
passing untrusted input creates an open redirect.

**Fix.** Added a README warning with a `Url.IsLocalUrl` example and guidance to use
`Inertia.Location` only for trusted external targets. No library code changed.

## SC-10 — Vite manifest hash caching

**Severity:** Low (per-request disk I/O and hashing)
**Status:** Fixed — `e87d176`
**Location:** `InertiaCore/Utils/Vite.cs`

**What it did.** `GetManifestHash()` called `GetManifest()` (uncached
`File.ReadAllText`) and hashed it, and was invoked at least twice per request when
no version was configured.

**Verification.** Added `UnitTestViteManifestHash`; the cache test failed before the
fix (the hash changed after modifying the file on disk).

**Fix.** Added a synchronous `ReadManifestStringWithCache()` that shares the existing
30 s manifest cache and `ClearManifestCache()`; `GetManifestHash()` uses it.
`GetManifest()` (public) is unchanged. No public API change.

## SC-11 — GitHub Actions pinning

**Severity:** Low/Info
**Status:** Fixed — `dc67f18`
**Location:** `.github/workflows/dotnet.yml`

**What it did.** `actions/checkout@v4` and `actions/setup-dotnet@v4` used mutable
tags. No secrets are referenced and there is no publish/nuget-push step in any ref.

**Fix.** Pinned to full commit SHAs with version comments
(`checkout@11bd71901bbe5b1630ceea73d27597364c9af683 # v4.2.2`,
`setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9 # v4.3.1`).

## SC-12 — Committed artifacts / IDE metadata

**Severity:** Info
**Status:** Fixed — `dc67f18`
**Location:** `ProjectsLocal/AspNetCore.InertiaCore.0.0.9.nupkg`, `.idea/`

**What it did.** A stale `0.0.9` package (current source is `0.2.1`) and IDE state
were committed. The package contained only DLLs/README/LICENSE (no secrets).

**Fix.** Removed both from the repository and added `.idea/` and `ProjectsLocal/`
to `.gitignore`.

## SC-13 — Outdated dependencies

**Severity:** Info
**Status:** Deferred
**Location:** `InertiaCore/InertiaCore.csproj`, `InertiaCore.SourceGenerators/InertiaCore.SourceGenerators.csproj`

`dotnet list package --vulnerable --include-transitive` → none.
`--deprecated` → none. `--outdated` → `System.IO.Abstractions` 19.2.16 → 22.3.0,
`Microsoft.CodeAnalysis.CSharp` 4.3.1 → 5.9.0 (source generator/tests only). No
security-blocking issues; deferred as a planned dependency maintenance task.

## SC-14 — SSR / JSON nulls and errors

**Severity:** Info
**Status:** Fixed (with SC-01) — `743b5df`
**Location:** `InertiaCore/Ssr/SsrResponse.cs`, `InertiaCore/Ssr/Gateway.cs`

Null `Body`/`Head` no longer throw, a null `Body` is treated as an SSR failure, and
malformed JSON/non-JSON bodies are caught and fall back to client-side rendering.
`DeserializeManifest` still converts parse errors into a generic message.

## SC-15 — `Inertia.Share` global fallback

**Severity:** Info
**Status:** Deferred (accepted behaviour)
**Location:** `InertiaCore/Inertia.cs`

`Inertia.Share` called when `IInertia` cannot be resolved falls through to the
static global registry (startup sharing). This is documented but silent; a
registration mistake could leak request-specific values globally. Deferred as an
accepted API behaviour rather than changed.

---

## Areas checked and found clean

- **Inertia JSON responses** (`Response.GetJson`): correct camelCase + configured
  encoder, `Vary`/`X-Inertia`/status 200 handled; no HTML context.
- **Version-mismatch flow:** correct `X-Inertia-Location` header and `409`; empty
  version disables the check; `X-Inertia` must equal `"true"`.
- **303 for non-GET:** `InertiaActionFilter` and `InertiaMiddleware` both map
  `POST`/`PUT`/`PATCH`/`DELETE` redirects to `303` (GET → `302`).
- **Error bags:** per-request scoped; the client-controlled `X-Inertia-Error-Bag`
  only selects a key within the current request's response.
- **DI lifetimes (main path):** `InertiaState`, `IInertia`, `IResponseFactory`,
  `IErrorBagService` scoped; `IGateway` singleton via `IHttpClientFactory`.
- **Source generator:** identifier sanitisation and string escaping adequate for
  developer-controlled attributes; collision diagnostics present.
- **Secrets:** none found in the working tree or across `git log --all -p` for
  common secret/API-key/private-key/token patterns.
- **Package vulnerabilities:** none.

## Known limitations

- SC-04's control-character bypass could not be reproduced through the in-process
  test host (transport normalises the header); the hardening is defence-in-depth.
- SC-03 was reproduced against TestServer as unsanitised header characters, not a
  confirmed response split; other hosts were not tested.
- The audit did not test a running application behind a reverse proxy, where header
  handling can differ.

## Version recommendation

Recommend a **minor version bump** (`0.2.1` → `0.3.0`): the security fixes add
public `InertiaOptions` members (`SsrTimeout`, `SsrMaxResponseBytes`) and change
observable behaviour (SSR fallback, scoped version resolvers, partial-reload
requirement, Vite output encoding). None of the changes remove or rename existing
public API, so a patch bump would understate the behaviour changes. The version was
**not** bumped as instructed.
