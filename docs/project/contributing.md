# Contributing

Contributions, issues and feature requests are welcome. Please open an issue or submit a pull
request on [GitHub](https://github.com/Creator-Solutions/InertiaCore).

## Prerequisites

- .NET SDK 8, 9 and 10
- Node.js 22 (for the TypeScript sample and the docs site)

## Build and test

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

The project targets net8.0, net9.0 and net10.0 in both Debug and Release, and keeps the build free
of warnings.

## Definition of done

- Public API is XML-documented and covered by unit tests.
- Integration behavior is covered with `WebApplicationFactory`.
- Builds and tests pass on net8.0, net9.0 and net10.0 in Debug and Release with zero warnings.
- No breaking change to the existing public API.

## Generated declarations and routes

The repository commits generated TypeScript artifacts as goldens and checks them in CI:

```bash
# Declarations
dotnet run --project InertiaCore.TypeGen/InertiaCore.TypeGen.csproj --framework net8.0 -c Debug -- \
  --assembly InertiaCoreTests.TestApp/bin/Debug/net10.0/InertiaCoreTests.TestApp.dll \
  --output InertiaCoreTests/Golden/inertia.generated.d.ts --check

# Typed routes
dotnet run --project InertiaCore.TypeGen/InertiaCore.TypeGen.csproj --framework net8.0 -c Debug -- routes \
  --input InertiaCoreTests/Golden/routes.sample.json \
  --output InertiaCoreTests/Golden/routes.sample.ts --check
```

Regenerate them (drop `--check`) when a change affects the output, and commit the updated files.

## Documentation

The documentation site lives in `docs/` and is built with [VitePress](https://vitepress.dev). To
work on it locally:

```bash
cd docs
npm ci
npm run dev      # dev server with hot reload
npm run build    # production build into docs/.vitepress/dist
```

See the [deploy script](https://github.com/Creator-Solutions/InertiaCore/blob/main/scripts/deploy-docs.sh)
for how the built site is published.

## Pull requests

Keep changes focused, add tests where behavior changes, and make sure the full CI suite is green.
