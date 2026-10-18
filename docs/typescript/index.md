# TypeScript

InertiaCore can give your frontend a compile-time contract for the props each page receives, and a
typed helper for building URLs (both generated from your C# code).

- [Declarations](/typescript/declarations): generate TypeScript types for every `[InertiaPage]`
  type with `InertiaCore.TypeGen`.
- [Typed routes](/typescript/routes): generate a Ziggy-style `route()` helper from your named
  Minimal API endpoints.

Both are driven by declaring your pages so the adapter and tooling know about them.

## Page registry

Declare your page components so the adapter can validate them and tooling can reference them as
strongly-typed constants:

```csharp
[assembly: InertiaPage("Dashboard")]
[assembly: InertiaPage("Users/Edit")]
```

or on a class:

```csharp
[InertiaPage("Account/Settings")]
public class AccountController { }
```

The source generator emits an `InertiaCore.Generated.InertiaPages` class with a constant per page
and registers each component with `InertiaPageRegistry` at startup. Enable runtime validation to
fail fast on unknown components:

```csharp
builder.Services.AddInertia(options =>
{
    options.ValidatePages = true;
});
```

## Typed props (`Render<T>`)

A props record can be the page contract: annotate it with `[InertiaPage]` and render it by type, so
callers never type the component string:

```csharp
[InertiaPage("Users/Index")]
public sealed record UsersIndexProps(IReadOnlyList<UserDto> Users, Pagination Page);

public IActionResult Index()
{
    var props = new UsersIndexProps(users, pagination);
    return Inertia.Render(props); // resolves the component "Users/Index" from the type
}
```

The generator emits a type-to-component table (a dictionary keyed by `Type`, no runtime reflection)
alongside the constants, and registers it at startup. `Render<T>` returns a page object identical to
`Render("Users/Index", props)`.

- If the type has no `[InertiaPage]` attribute, `Render<T>` is a compile-time error (`INERTIA006`)
  and throws `InvalidOperationException` at runtime.
- Two types declaring the same component name is a compile-time error (`INERTIA005`).
- A type with **multiple** `[InertiaPage]` attributes (for example a controller) has no unambiguous
  mapping, so it is left out of the type table and `Render<T>` throws. Use the string overload for
  those.
- Wrapper members (`DeferredProp`, `LazyProp`, `AlwaysProp`, `MergeProp`, `OnceProp`, `ScrollProp`)
  are allowed on the props record and keep their behaviour (`[Deferred]`/`[Lazy]` resolve on partial
  reloads, `Always` on every response).

The existing `Render(string component, props)` overloads are unchanged.
