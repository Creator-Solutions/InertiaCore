# Slop audit — InertiaCore docs

Phase 1 audit of user-facing copy in the VitePress docs site and the READMEs. **No copy has
been changed yet.** This file records what was found, what will be left alone, and which
claims need the owner's decision.

Tools: `scripts/slop-lint` (pattern scan) and `scripts/meaning-guard` (mechanical before/after
safety net). Both are run with the results in this document.

## 1. Inventory — how the copy was found

There is no i18n framework, no `locales/*.json`, and no resource files. User-facing text lives in
markdown content and a small amount of theme-config chrome.

| Source | How found | Scope |
| --- | --- | --- |
| `docs/**/*.md` (20 files) | VitePress content root (`docs/`) | In scope (body copy) |
| `docs/.vitepress/config.mts` | VitePress theme config: nav labels, outline label, footer, edit-link text | In scope (chrome only) |
| `README.md` | Repo landing page describing the product | In scope |
| `samples/react/README.md` | Sample project prose | In scope |
| `docs/project/changelog.md` | Docs page paraphrasing release facts | In scope (body) |
| `CHANGELOG.md`, `RELEASE_NOTES.md` | Root release notes | **Out of scope, flag only** |
| `SECURITY.md`, `SECURITY_AUDIT.md` | Security statements | **Out of scope, flag only** |
| `docs/security.md` | Security page | **Punctuation only** (owner exception) |
| `InertiaCore/**` code + comments | Library source | **Out of scope, flag only** |
| `docs/package-lock.json` | Not copy | Out |

## 2. Matches per file and category

`slop-lint` result on the current tree: **34 matches** (29 `em-dash`, 5 `hype-vocab`), exit `1`.

| File | em-dash | modern | Notes |
| --- | --- | --- | --- |
| `README.md` | 7 | 2 | Features list dashes + "modern" ×2 |
| `docs/introduction.md` | 1 | 3 | "modern" ×3; 1 of them retained (see §6) |
| `docs/typescript/index.md` | 4 | – | Link bullets and inline dashes |
| `docs/project/changelog.md` | 3 | – | Bold-label bullets |
| `docs/security.md` | 3 | – | Punctuation-only exception |
| `docs/guide/precognition.md` | 3 | – | Protocol bullets + prose |
| `docs/typescript/declarations.md` | 1 | – | Prose aside |
| `docs/guide/props.md` | 1 | – | Prose aside |
| `docs/guide/responses.md` | 1 | – | Prose aside |
| `docs/guide/validation.md` | 1 | – | Prose aside |
| `samples/react/README.md` | 4 | – | File-explanation bullets |
| All other docs files | 0 | 0 | Clean |

Rules with **zero** matches on the current tree: `filler-opener`, `rhetorical-heading`
(1 allowlisted), `stacked-punctuation`, `emoji`, `spaced-hyphen-dash`. `title-case-heading` is
disabled by default to avoid heuristic noise; headings are already sentence case.

Two allowlisted matches (intentionally not counted):
- `docs/guide/vite.md:57` — em dash in a comment inside a copy-pasteable config block (owner ruled not editable).
- `docs/introduction.md:17` — `## Why InertiaCore?` rhetorical heading (owner ruled keep).

### 2a. Hidden by the "don't touch frontmatter" rule

`docs/index.md` is a VitePress home page; its hero and feature copy is **frontmatter**, which the
owner excluded from edits. Two slop items therefore sit in code the linter skips by design, listed
here for completeness:

- `docs/index.md:7` (tagline) — `Build modern monolithic applications with server-side routing and your favourite frontend framework. No separate API layer.`
- `docs/index.md:24` (feature) — `Full protocol support — request detection, version handling, partial reloads, lazy props, shared props, redirects and error bags.`

Both are blocked by the frontmatter rule. Flagged for a decision if you later want the hero
cleaned.

### 2b. `scripts/slop-lint` self-note

The linter masks inline code spans (so `IList<...>` and `Inertia.Share(...)` are not mistaken for
decorative ellipses) and scans fenced code blocks only for em dashes in comment lines.

## 3. Ten worst offenders (before text only)

1. `README.md:8` — `A production-ready ASP.NET Core adapter for building modern **Inertia.js** applications with .NET.`
2. `README.md:59` — `- **Rich props** — lazy and async props, deferred, once, merge and infinite-scroll props, with per-prop rescue.`
3. `docs/introduction.md:3` — `**InertiaCore.AspNetCore** is a production-ready ASP.NET Core adapter for building modern [Inertia.js](https://inertiajs.com) applications with .NET.`
4. `docs/introduction.md:6` — `It provides a complete server-side integration layer for Inertia.js, letting you build modern monolithic applications with your preferred frontend framework while keeping the simplicity of server-side routing and controllers.`
5. `docs/introduction.md:44` — `Full support for the latest Inertia.js v3 protocol, including request detection, version handling, partial reloads, lazy props, shared props, redirect handling, error bag support and response formatting.`
6. `README.md:56` — `- **Inertia.js v3** — full protocol support: request detection, version handling, partial reloads, lazy props, shared props, redirects and error bags.`
7. `docs/typescript/index.md:4` — `typed helper for building URLs — both generated from your C# code.`
8. `docs/guide/precognition.md:5` — `Precognition: true` requests, validates them, and short-circuits with `204`/`422` — the endpoint/action body never runs.`
9. `docs/security.md:10` — `adapter does not wire up CSRF protection for you** — you must do it explicitly.`
10. `docs/typescript/declarations.md:97` — `The attribute is a code-generation marker only — it does not share anything at runtime.`

## 4. Recurring phrases to standardize

- **Label dash `— `** → use a colon for bold-label bullets (`**Rich props**: lazy and async props`), and restructure prose into a sentence, colon or parentheses. Never a bare comma swap; keep the text after the dash.
- **`modern`** → remove only where it modifies *applications* (owner rule). Keep where it modifies *frontend components* (`docs/introduction.md:31`).
- No other phrase recurs enough to standardize. Headings are already sentence case.

## 5. What will NOT be touched, and why

- **All headings** — anchors; links elsewhere depend on them.
- **All frontmatter** (`docs/index.md` hero/features, page titles/descriptions) — owner rule. See §2a for the slop it hides.
- **VitePress containers** (`:::`) and their markup.
- **Code blocks**, except em dashes inside non-config comments.
- **`docs/security.md` wording** — punctuation only (its 3 em dashes).
- **`CHANGELOG.md`, `RELEASE_NOTES.md`, `SECURITY.md`, `SECURITY_AUDIT.md`, `InertiaCore/**` comments** — out of scope, flag only.
- **`docs/guide/vite.md:57`** — config comment, allowlisted.
- **`docs/introduction.md:31`** — `modern` retained (modifies "frontend components").
- **Code identifiers, keys, anchors, JSON fields, `data-testid`s, and links** — never edited.

## 6. Claims at risk — need your decision

Per your rule, these are retained verbatim. They remain listed here for you to rule on after
checking them against the code and tests.

| Location | Original wording | Claim it makes | Status |
| --- | --- | --- | --- |
| `README.md:8`, `docs/introduction.md:3`, `config.mts:9` | `production-ready` | quality/readiness | Retained — your call |
| `docs/introduction.md:6`, `README.md:10` | `complete server-side integration layer` | completeness | Retained — your call |
| `docs/introduction.md:44`, `README.md:56`, `docs/index.md:24` | `Full support` / `full protocol support` | completeness | Retained — your call |
| `README.md:59`, `docs/index.md:27` | `Rich props` | quality | Retained — your call |
| `docs/index.md:30` | `hardened rendering out of the box` | security/robustness | Retained — your call |
| `docs/index.md:33`, `README.md:65` | `Typed end to end` | quality | Retained — your call |
| `docs/getting-started.md:35` | `Your application is now ready to serve Inertia responses.` | readiness | Retained — your call |
| `docs/security.md` (whole page) | CSRF / redirect / SSR statements | security | No wording change |
| `docs/introduction.md:31` | `modern frontend components` | style | Retained (owner rule) |

**`modern` removal plan (wording change, not a claim):** remove at `README.md:8`,
`README.md:11`, `docs/introduction.md:3`, `docs/introduction.md:6`; keep at
`docs/introduction.md:31`. Each removal gets a meaning check in `CHANGELOG-COPY.md`.

## 7. Tool results (current tree)

- `scripts/slop-lint` → 34 matches, exit `1` (expected; fixes are staged in batches).
- `scripts/meaning-guard --self-test` → all 7 cases pass, exit `0`.
- `scripts/meaning-guard` (empty map) → 0 pairs, no mismatches, exit `0`.

## 8. Limits of the safety net

`meaning-guard` compares mechanical tokens only: numbers/currency, URLs, emails, placeholders
(`{name}`, `%s`, `{{x}}`), HTML/markdown tags, quantifiers/modality, negations and proper nouns.
It cannot catch subtler meaning drift, so every changed string still gets a written per-string
meaning check in `CHANGELOG-COPY.md`.

## 9. Planned batches (after approval)

1. Home and README (`README.md`, `docs/index.md` body) — then stop for review.
2. `docs/introduction.md`, `docs/getting-started.md`.
3. `docs/guide/*`.
4. `docs/typescript/*`, `docs/reference/*`, `docs/project/changelog.md`.
5. `docs/security.md` (punctuation only), `samples/react/README.md`, `config.mts` chrome.

Docs build (`npm run build`, dead-link check) must pass after every batch.
