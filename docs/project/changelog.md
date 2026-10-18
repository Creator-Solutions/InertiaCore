# Changelog

The changelog is maintained in the repository as the single source of truth:

- [CHANGELOG.md](https://github.com/Creator-Solutions/InertiaCore/blob/main/CHANGELOG.md)
- [GitHub releases](https://github.com/Creator-Solutions/InertiaCore/releases)
- [NuGet package](https://www.nuget.org/packages/InertiaCore.AspNetCore)

## 0.3.0

The current release focuses on making the C# side the source of truth for the TypeScript frontend:

- **Typed routes**: `inertiatsgen routes` generates a Ziggy-style `route()` helper from your named
  Minimal API endpoints, with a Development-only metadata dump via `MapInertiaRoutes()`. See
  [Typed routes](/typescript/routes).
- **Typed registry, React helper & shared props**: declare pages with `[InertiaPage]`, render by
  type with `Render<T>`, emit an opt-in `usePageProps` helper, and describe shared props with
  `[InertiaSharedProps]`. See [TypeScript](/typescript/).
- **`InertiaCore.TypeGen`**: a dotnet tool that writes TypeScript declarations from your compiled
  assembly without executing it. See [Declarations](/typescript/declarations).

All additions are additive; there is no breaking change to the existing public API.
