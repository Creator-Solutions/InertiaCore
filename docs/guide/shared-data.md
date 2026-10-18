# Shared data

Share data across Inertia responses. Shared props are merged into every page's props.

When called **during a request** (for example inside a controller), `Inertia.Share` is scoped to
that request and is never visible to other requests or users:

```csharp
public IActionResult Index()
{
    Inertia.Share("auth", new { UserId = userId });
    return Inertia.Render("Dashboard");
}
```

When called **outside a request** (for example during application startup), the value is registered
globally and merged into every response:

```csharp
// Program.cs
Inertia.Share("appName", "My App");

Inertia.Share(new Dictionary<string, object?>
{
    ["auth"] = new { UserId = userId }
});
```

The merge order is **global → request-scoped → component props**, so component props always win.
When injecting `IInertia` via DI, `Share` is always request-scoped.

## Shared props metadata

The keys registered via `Inertia.Share` (global and request-scoped) are emitted as `sharedProps`,
which the client uses to carry shared props over during instant visits:

```json
{
  "component": "Dashboard",
  "props": { "auth": { "user": "..." }, "stats": { } },
  "sharedProps": ["auth"]
}
```

## Typing shared props

To give the frontend a compile-time contract for your shared props, mark a record describing their
shape with `[InertiaSharedProps]` and let `inertiatsgen` emit it. See
[Typed declarations](/typescript/declarations#shared-props).
