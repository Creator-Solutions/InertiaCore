# Declarations

`InertiaCore.TypeGen` is a dotnet tool that reads your compiled assembly and writes TypeScript
declarations for every `[InertiaPage]` type and the types it references, so the frontend has a
compile-time contract for page props. It loads the assembly with `MetadataLoadContext` and never
executes your code.

## Install and run

```bash
dotnet tool install --global InertiaCore.TypeGen

inertiatsgen --assembly bin/Debug/net8.0/MyApp.dll \
             --output Client/types/inertia.generated.d.ts
```

For a record like this:

```csharp
[InertiaPage("Users/Index")]
public sealed record UsersIndexProps(IReadOnlyList<UserDto> Users, int Page);
```

the tool emits:

```ts
export interface UsersIndexProps {
  users: UserDto[];
  page: number;
}

export interface InertiaPageProps {
  'Users/Index': UsersIndexProps;
}
```

## Options

| Option | Description |
| --- | --- |
| `--assembly <path>` | Compiled assembly to read. Required. |
| `--output <path>` | Output file. Default: `Client/types/inertia.generated.d.ts`. |
| `--reference <path>` | Additional assembly used to resolve references. Repeatable. |
| `--enum-mode <mode>` | `auto` (default), `string` or `number`. |
| `--no-camel-case` | Disable camelCase property naming. |
| `--emit-react-helper` | Emit a typed `usePageProps` helper for `@inertiajs/react`. |
| `--check` | Exit non-zero if the output is out of date (no write). |
| `--watch` | Regenerate whenever the assembly changes. |

## Type mapping

Type mapping follows the InertiaCore serializer defaults:

| C# | TypeScript |
| --- | --- |
| `string`, `Guid`, `DateTime`, `DateTimeOffset`, `TimeSpan` | `string` |
| numeric types | `number` |
| `bool` | `boolean` |
| `T?` | `T \| null` |
| `IEnumerable<T>` / `T[]` / `List<T>` | `T[]` |
| `Dictionary<string, T>` | `Record<string, T>` |
| enum without a string converter | numeric union (`0 \| 1`) |
| enum with `JsonStringEnumConverter` | string-literal union (`'A' \| 'B'`) |
| record / class | `interface` |

camelCase naming, `[JsonPropertyName]` and `[JsonIgnore]` are respected. Members that are `Lazy`,
`Defer` or `Optional` props, and members marked
`[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`, are emitted as optional properties
(`name?: T`). Cyclic references are handled; output is deterministic so diffs stay clean.

## Use in CI

Use `--check` to fail when the declarations are stale:

```bash
inertiatsgen --assembly bin/Release/net10.0/MyApp.dll \
             --output Client/types/inertia.generated.d.ts --check
```

## Shared props

Mark the record that describes your shared props with `[InertiaSharedProps]` and the tool emits it
as a dedicated `InertiaSharedProps` interface:

```csharp
[InertiaSharedProps]
public sealed record SharedProps(string AppName, TypedUser? Auth);
```

```ts
export interface InertiaSharedProps {
  appName: string;
  auth: TypedUser | null;
}
```

The attribute is a code-generation marker only. It does not share anything at runtime. Keep using
`Inertia.Share(...)` to populate the values; the record just describes their shape. At most one type
per assembly may be annotated.

## React helper (opt-in)

Pass `--emit-react-helper` (or set `TypeScriptGeneratorOptions.EmitReactHelper = true`) to add a
typed accessor for the props of a page component:

```bash
inertiatsgen --assembly bin/Release/net10.0/MyApp.dll \
             --output Client/types/inertia.generated.d.ts --emit-react-helper
```

```ts
import { usePage } from '@inertiajs/react';

// ...

export function usePageProps<K extends keyof InertiaPageProps>(component: K): InertiaPageProps[K] & InertiaSharedProps {
  void component;
  return usePage().props as InertiaPageProps[K] & InertiaSharedProps;
}
```

The helper is omitted by default so projects that do not use React hooks are unaffected. When a
shared props record is present, the return type intersects `InertiaSharedProps` because Inertia
merges shared props into every page's props.

## Typing the page resolver

`InertiaPageProps` maps each component name to its props, which lets you type the resolver and the
page components themselves:

```tsx
import { createInertiaApp } from '@inertiajs/react';
import type { ComponentType } from 'react';
import type { InertiaPageProps } from './types/inertia.generated';
import UsersIndex from './Pages/Users/Index';

type PageComponent<K extends keyof InertiaPageProps> = ComponentType<InertiaPageProps[K]>;

// Every component name must have a matching entry, bound to its declared props.
const pages: { [K in keyof InertiaPageProps]: PageComponent<K> } = {
  'Users/Index': UsersIndex,
};

createInertiaApp({
  // The framework types `name` as `string`; narrow it to the registry keys here.
  resolve: (name) => ({ default: pages[name as keyof InertiaPageProps] }),
});
```

A page component can then take its props straight from the registry:

```tsx
export default function UsersIndex(props: InertiaPageProps['Users/Index']) {
  return <ul>{props.users.map((u) => <li key={u}>{u}</li>)}</ul>;
}
```

Because the page's props are now typed, renaming a props member in C# and regenerating produces a
compile error in every component that read the old name. A working project that compiles under
`tsc --strict` lives in
[`samples/react`](https://github.com/Creator-Solutions/InertiaCore/tree/main/samples/react).
