# Needs a human decision

Strings left untouched (or changed only mechanically) that require the owner's ruling. Nothing here
was resolved by guessing.

## 1. Retained claims (kept verbatim per your instruction)

Rule on each after checking it against the code and test results.

| Original wording | Location(s) | Note |
| --- | --- | --- |
| `production-ready` | `README.md:8`, `docs/introduction.md:3`, `docs/.vitepress/config.mts:9,26` | quality/readiness claim |
| `complete server-side integration layer` | `docs/introduction.md:6`, `README.md:10` | completeness claim |
| `Full support` / `full protocol support` | `docs/introduction.md:44`, `README.md:56`, `docs/index.md:24` (frontmatter) | completeness claim |
| `Rich props` | `README.md:59`, `docs/index.md:27` (frontmatter) | quality claim |
| `hardened rendering out of the box` | `docs/index.md:30` (frontmatter) | security/robustness claim |
| `Typed end to end` | `README.md:65`, `docs/index.md:33` (frontmatter) | quality claim |
| `Your application is now ready to serve Inertia responses.` | `docs/getting-started.md:35` | readiness claim |

## 2. Double colons introduced by the label-dash fix

Converting `**Label** — value` to a colon produces a double colon where the value starts with its own
colon. Values were left verbatim per your rule ("do not alter anything after the dash").

| Location | Resulting text | Options |
| --- | --- | --- |
| `README.md:56` | `- **Inertia.js v3**: full protocol support: request detection, ...` | leave, or change the inner `:` to a period/comma |
| `samples/react/README.md:38` | `- \`app.tsx\`: a typed page resolver: every name in ...` | leave, or change the inner `:` |
| `samples/react/README.md:43` | `- \`routes-usage.ts\`: the typed \`route()\` helper: known names ...` | leave, or change the inner `:` |

## 3. Slop hidden in frontmatter (blocked by the "don't touch frontmatter" rule)

`docs/index.md` hero and features are frontmatter, excluded from edits. Two items sit there:

- `docs/index.md:7` — `Build modern monolithic applications with server-side routing and your favourite frontend framework. No separate API layer.` (`modern`)
- `docs/index.md:24` — `Full protocol support — request detection, version handling, partial reloads, lazy props, shared props, redirects and error bags.` (em dash)

Decision needed only if you later lift the frontmatter rule.

## 4. Security page punctuation

`docs/security.md` was punctuation-only as instructed (3 em dashes → colons). No wording changed. If
you would prefer those three sentences split into two sentences (requires capitalizing the following
word), that is a wording/typography decision for you; it is not done here.

## 5. Notes, not decisions

- `modern` was removed only where it modified "applications" (5 places). It is retained at
  `docs/introduction.md:31`, where it modifies "frontend components". Recorded in
  `CHANGELOG-COPY.md`.
- The one em dash inside a copy-pasteable config comment (`docs/guide/vite.md:57`) is allowlisted and
  was not touched.
