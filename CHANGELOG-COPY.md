# Copy changelog

One row per changed string. `meaning-guard` must pass on each batch before moving on, followed by
a written per-string meaning check.

## Batch 1 — Home and README

Files: `README.md`, `docs/index.md` (body).

`docs/index.md`: **no strings changed.** Its hero and feature text is frontmatter, which is out of
scope by rule; the markdown body (Quick start section) contained no slop.

`meaning-guard` on this batch: 9 pairs, no mismatches (allowRemoved: `modern`).

| File:line | Before | After | Change type | Meaning check |
| --- | --- | --- | --- | --- |
| `README.md:8` | `A production-ready ASP.NET Core adapter for building modern **Inertia.js** applications with .NET.` | `A production-ready ASP.NET Core adapter for building **Inertia.js** applications with .NET.` | wording | Claim `production-ready` retained; subject (`ASP.NET Core adapter`) and object (`Inertia.js applications with .NET`) unchanged; no qualifier/quantity/negation changed. Removed `modern` only, which modified "applications". |
| `README.md:11` | `to build modern monolithic applications using your preferred frontend framework while keeping the` | `to build monolithic applications using your preferred frontend framework while keeping the` | wording | Actor/action unchanged (build applications); `monolithic`, `preferred frontend framework`, `keeping the simplicity of server-side routing and controllers` unchanged; removed `modern` only, which modified "applications". |
| `README.md:56` | `- **Inertia.js v3** — full protocol support: request detection, version handling, partial reloads,` | `- **Inertia.js v3**: full protocol support: request detection, version handling, partial reloads,` | punctuation-only | Label `Inertia.js v3`; value verbatim (`full protocol support` retained). Only the label separator changed from em dash to colon. |
| `README.md:58` | `- **MVC & Minimal APIs** — the same Inertia responses work in controllers and endpoint handlers.` | `- **MVC & Minimal APIs**: the same Inertia responses work in controllers and endpoint handlers.` | punctuation-only | Text after separator verbatim. |
| `README.md:59` | `- **Rich props** — lazy and async props, deferred, once, merge and infinite-scroll props, with` | `- **Rich props**: lazy and async props, deferred, once, merge and infinite-scroll props, with` | punctuation-only | Claim `Rich props` retained; list after separator verbatim. |
| `README.md:61` | `- **Server-side rendering** — built-in SSR gateway support.` | `- **Server-side rendering**: built-in SSR gateway support.` | punctuation-only | Text after separator verbatim. |
| `README.md:62` | `- **Vite integration** — HMR detection, manifest resolution, module preloading, CSS handling, CSP` | `- **Vite integration**: HMR detection, manifest resolution, module preloading, CSS handling, CSP` | punctuation-only | Text after separator verbatim. |
| `README.md:64` | `- **Validation & Precognition** — automatic \`ModelState\` error handling and form precognition.` | `- **Validation & Precognition**: automatic \`ModelState\` error handling and form precognition.` | punctuation-only | Text after separator verbatim; code span `ModelState` unchanged. |
| `README.md:65` | `- **Typed end to end** — generate TypeScript declarations for your page props and typed route` | `- **Typed end to end**: generate TypeScript declarations for your page props and typed route` | punctuation-only | Claim `Typed end to end` retained; text after separator verbatim. |

### Flagged in this batch (needs a human decision)

- `README.md:56` — replacing the label dash with a colon produces a **double colon** because the
  value itself starts with a colon (`full protocol support: request detection, ...`). Per your rule
  ("do not alter anything after the dash") the value was left verbatim. If you prefer, the inner
  colon could become a period or comma — that is a human decision, not made here.

## Batch 2 — Introduction and Installation

Files: `docs/introduction.md`, `docs/getting-started.md`.

`docs/getting-started.md`: **no strings changed.** No slop matched; `Your application is now ready
to serve Inertia responses.` is a retained claim.

`meaning-guard` on this batch's pairs (part of the cumulative 12): no mismatches.

| File:line | Before | After | Change type | Meaning check |
| --- | --- | --- | --- | --- |
| `docs/introduction.md:3` | `**InertiaCore.AspNetCore** is a production-ready ASP.NET Core adapter for building modern [Inertia.js](https://inertiajs.com) applications with .NET.` | `**InertiaCore.AspNetCore** is a production-ready ASP.NET Core adapter for building [Inertia.js](https://inertiajs.com) applications with .NET.` | wording | Claim `production-ready` retained; subject (`InertiaCore.AspNetCore`) and object (`Inertia.js applications with .NET`) unchanged. Removed `modern` only, which modified "applications". Link target unchanged. |
| `docs/introduction.md:6` | `It provides a complete server-side integration layer for Inertia.js, letting you build modern monolithic applications with your preferred frontend framework while keeping the simplicity of` | `It provides a complete server-side integration layer for Inertia.js, letting you build monolithic applications with your preferred frontend framework while keeping the simplicity of` | wording | Claims retained (`complete server-side integration layer`; build monolithic applications with your preferred framework; keeps the simplicity of server-side routing and controllers). Removed `modern` only, which modified "applications". |
| `docs/introduction.md:38` | `- A frontend build tool — [Vite](https://vitejs.dev) is supported directly, but any bundler works` | `- A frontend build tool. [Vite](https://vitejs.dev) is supported directly, but any bundler works` | restructure | Same three claims: a frontend build tool is required; Vite is supported directly; any bundler works. Quantifier `any` and modality (`is supported`, `works`) unchanged. Em dash aside split into a second sentence. |

No items flagged for a decision in batch 2.

## Batch 3 — Guide

Files: `docs/guide/*` (advanced, precognition, props, responses, shared-data, ssr, validation,
versioning, vite).

Only four files needed changes; the rest had no matches (and a manual sweep for contrastive
reframes, rhetorical questions, filler transitions and other vocabulary found nothing). The one
em dash in `docs/guide/vite.md:57` is inside a copy-pasteable config comment and is allowlisted.

`meaning-guard` on this batch's pairs (part of the cumulative 18): no mismatches.

| File:line | Before | After | Change type | Meaning check |
| --- | --- | --- | --- | --- |
| `docs/guide/precognition.md:5` | `\`Precognition: true\` requests, validates them, and short-circuits with \`204\`/\`422\` — the endpoint/action body never runs.` | `\`Precognition: true\` requests, validates them, and short-circuits with \`204\`/\`422\`. The endpoint/action body never runs.` | restructure | Same claims: detects precognition requests, validates, short-circuits with 204/422; body never runs. Status codes and `never` unchanged. Em dash split into two sentences. |
| `docs/guide/precognition.md:10` | `- **Valid** — \`204 No Content\` with \`Precognition-Success: true\`.` | `- **Valid**: \`204 No Content\` with \`Precognition-Success: true\`.` | punctuation-only | Label `Valid`; value verbatim (status and header unchanged). |
| `docs/guide/precognition.md:11` | `- **Invalid** — \`422 Unprocessable Entity\` with \`{ "errors": { field: [messages] } }\`.` | `- **Invalid**: \`422 Unprocessable Entity\` with \`{ "errors": { field: [messages] } }\`.` | punctuation-only | Label `Invalid`; value verbatim (status and error shape unchanged). |
| `docs/guide/props.md:42` | `reloads instead of replacing it — useful for "load more" pagination. A full visit always replaces the prop.` | `reloads instead of replacing it (useful for "load more" pagination). A full visit always replaces the prop.` | restructure | Claims unchanged: merge combines data on partial reloads instead of replacing; useful for "load more" pagination; a full visit always replaces. Quantifier `full` and `always` kept. Aside moved into parentheses. |
| `docs/guide/responses.md:61` | `...Validate the target before redirecting — for example, only accept local URLs, or an allow-listed set of hosts:` | `...Validate the target before redirecting. For example, only accept local URLs, or an allow-listed set of hosts:` | restructure | Same warning and example; `only` and the allow-list condition unchanged. Em dash split into two sentences. |
| `docs/guide/validation.md:50` | `With React you can read the same prop through the typed page hook — see [Typed declarations](/typescript/declarations).` | `With React you can read the same prop through the typed page hook. See [Typed declarations](/typescript/declarations).` | restructure | Modality `can` kept; object (the same prop via the typed page hook) and the link unchanged. Em dash split into two sentences. |

No items flagged for a decision in batch 3.

## Batch 4 — TypeScript, Reference, Project changelog

Files: `docs/typescript/*`, `docs/reference/*`, `docs/project/*`.

`docs/reference/configuration.md`, `docs/project/roadmap.md` and `docs/project/contributing.md`:
**no strings changed** (no slop matched; a manual sweep of the reference and project directories
found nothing).

`meaning-guard` on this batch's pairs (part of the cumulative 26): no mismatches.

| File:line | Before | After | Change type | Meaning check |
| --- | --- | --- | --- | --- |
| `docs/typescript/index.md:4` | `typed helper for building URLs — both generated from your C# code.` | `typed helper for building URLs (both generated from your C# code).` | restructure | Claims unchanged: InertiaCore can give a compile-time contract for page props and a typed URL helper; both generated from your C# code. Aside kept in parentheses. |
| `docs/typescript/index.md:6` | `- [Declarations](/typescript/declarations) — generate TypeScript types for every \`[InertiaPage]\` type with \`InertiaCore.TypeGen\`.` | `- [Declarations](/typescript/declarations): generate TypeScript types for every \`[InertiaPage]\` type with \`InertiaCore.TypeGen\`.` | punctuation-only | Link and description verbatim; quantifier `every` kept. Separator only. |
| `docs/typescript/index.md:8` | `- [Typed routes](/typescript/routes) — generate a Ziggy-style \`route()\` helper from your named Minimal API endpoints.` | `- [Typed routes](/typescript/routes): generate a Ziggy-style \`route()\` helper from your named Minimal API endpoints.` | punctuation-only | Link and description verbatim. Separator only. |
| `docs/typescript/index.md:65` | `mapping, so it is left out of the type table and \`Render<T>\` throws — use the string overload for those.` | `mapping, so it is left out of the type table and \`Render<T>\` throws. Use the string overload for those.` | restructure | Same claims: no unambiguous mapping, left out of the type table, `Render<T>` throws, use the string overload. Em dash split into two sentences. |
| `docs/typescript/declarations.md:97` | `The attribute is a code-generation marker only — it does not share anything at runtime. Keep using` | `The attribute is a code-generation marker only. It does not share anything at runtime. Keep using` | restructure | `only` and the negation `does not share anything at runtime` kept. Two sentences. |
| `docs/project/changelog.md:13` | `- **Typed routes** — \`inertiatsgen routes\` generates a Ziggy-style \`route()\` helper from your named Minimal API endpoints, with a Development-only metadata dump via \`MapInertiaRoutes()\`. See [Typed routes](/typescript/routes).` | `- **Typed routes**: \`inertiatsgen routes\` generates a Ziggy-style \`route()\` helper from your named Minimal API endpoints, with a Development-only metadata dump via \`MapInertiaRoutes()\`. See [Typed routes](/typescript/routes).` | punctuation-only | Label `Typed routes`; description and details verbatim. Separator only. |
| `docs/project/changelog.md:16` | `- **Typed registry, React helper & shared props** — declare pages ... See [TypeScript](/typescript/).` | `- **Typed registry, React helper & shared props**: declare pages ... See [TypeScript](/typescript/).` | punctuation-only | Label and description verbatim. Separator only. |
| `docs/project/changelog.md:19` | `- **\`InertiaCore.TypeGen\`** — a dotnet tool that writes TypeScript declarations from your compiled assembly without executing it. See [Declarations](/typescript/declarations).` | `- **\`InertiaCore.TypeGen\`**: a dotnet tool that writes TypeScript declarations from your compiled assembly without executing it. See [Declarations](/typescript/declarations).` | punctuation-only | Label and description verbatim. Separator only. |

No items flagged for a decision in batch 4.

## Batch 5 — Security page, React sample README, config chrome

Files: `docs/security.md`, `samples/react/README.md`, `docs/.vitepress/config.mts`.

`docs/.vitepress/config.mts`: **no strings changed.** Chrome (nav labels, `On this page`, footer,
edit-link text) had no slop. The site description and `og:description` contain `production-ready`,
which is a retained claim.

`docs/security.md` is the owner-flagged exception: **punctuation only**, no wording changed (all
letters and code spans identical).

`meaning-guard` on this batch's pairs (part of the cumulative 33): no mismatches.

| File:line | Before | After | Change type | Meaning check |
| --- | --- | --- | --- | --- |
| `docs/security.md:10` | `adapter does not wire up CSRF protection for you** — you must do it explicitly.` | `adapter does not wire up CSRF protection for you**: you must do it explicitly.` | punctuation-only — security page | No wording changed. Em dash → colon only. Negation (`does not wire up`) and obligation (`must`) intact. |
| `docs/security.md:60` | `user-supplied value straight through — validate it first (for example with \`Url.IsLocalUrl\`) or use` | `user-supplied value straight through: validate it first (for example with \`Url.IsLocalUrl\`) or use` | punctuation-only — security page | No wording changed. Em dash → colon only. `Never` (previous line) and the condition/example intact. |
| `docs/security.md:74` | `response — do not put user-specific values there.` | `response: do not put user-specific values there.` | punctuation-only — security page | No wording changed. Em dash → colon only. Negation `do not` intact. |
| `samples/react/README.md:38` | `- \`app.tsx\` — a typed page resolver: every name in \`InertiaPageProps\` must have a` | `- \`app.tsx\`: a typed page resolver: every name in \`InertiaPageProps\` must have a` | punctuation-only | Label `app.tsx`; description verbatim; modality `must` kept. (Double colon, see flags.) |
| `samples/react/README.md:40` | `- \`Pages/*/Index.tsx\` — page components typed with \`InertiaPageProps['<Component>']\`.` | `- \`Pages/*/Index.tsx\`: page components typed with \`InertiaPageProps['<Component>']\`.` | punctuation-only | Label and description verbatim. |
| `samples/react/README.md:41` | `- \`components/SharedBanner.tsx\` — the generated \`usePageProps\` helper, whose result` | `- \`components/SharedBanner.tsx\`: the generated \`usePageProps\` helper, whose result` | punctuation-only | Label and description verbatim. |
| `samples/react/README.md:43` | `- \`routes-usage.ts\` — the typed \`route()\` helper: known names and required parameters are` | `- \`routes-usage.ts\`: the typed \`route()\` helper: known names and required parameters are` | punctuation-only | Label and description verbatim. (Double colon, see flags.) |

### Flagged in this batch (needs a human decision)

- `samples/react/README.md:38` and `:43` — as in `README.md:56`, replacing the label dash with a
  colon produces a **double colon** because the value starts with its own colon. Value left verbatim
  per your rule.

## Final summary

- `scripts/slop-lint` on the whole in-scope tree: **34 matches before → 0 after** (exit 1 → 0).
  - 29 em dashes fixed; `modern` 5 matches → 4 removed, 1 allowlisted (`docs/introduction.md:31`).
- Strings changed: **33** total.

| Change type | Count |
| --- | --- |
| punctuation-only | 21 |
| restructure | 8 |
| wording | 4 (all: removed `modern`) |

- `scripts/meaning-guard`: 33 pairs, **no mismatches**; self-test all passed.
- Files changed: 11 content files. Files reviewed with **no changes**: `docs/index.md`,
  `docs/getting-started.md`, `docs/reference/configuration.md`, `docs/project/roadmap.md`,
  `docs/project/contributing.md`, `docs/.vitepress/config.mts`.
- Allowlisted (not fixed, with reason): `docs/guide/vite.md:57` (config comment em dash),
  `docs/introduction.md:31` (`modern` modifying "frontend components"), `## Why InertiaCore?`.
- Untouched by scope: all headings, frontmatter, VitePress containers, code blocks (except the one
  allowed comment), `CHANGELOG.md`, `RELEASE_NOTES.md`, `SECURITY.md`, `SECURITY_AUDIT.md`, and all
  `InertiaCore/**` code comments.
- Docs build (`npm run build`, dead-link check): passes after every batch.
- No tests assert on this prose; no test files were changed.
