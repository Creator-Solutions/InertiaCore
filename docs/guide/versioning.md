# Asset versioning

Inertia compares an asset version between the client and the server. When it changes, the client
performs a full page reload so users never run stale JavaScript against new backend code.

## Version providers

Built-in providers:

- Default static version provider
- Delegate-based version provider
- Custom provider implementations

The version defaults to an empty string, which is a valid Inertia protocol value meaning "this
server does not track asset versions". `AddInertia()` therefore works out of the box without
configuring a version.

```csharp
builder.Services.AddInertia(options =>
{
    options.VersionResolver = () =>
    {
        return "1.0.0";
    };
});
```

## With Vite

When the [Vite integration](/guide/vite) is registered, `Vite.GetManifestHash()` gives you a
version that tracks the built manifest. Wire it into the version resolver so a new frontend build
triggers a reload:

```csharp
builder.Services.AddInertia(options =>
{
    options.VersionResolver = () => Vite.GetManifestHash() ?? "dev";
});
```

`Vite.GetManifestHash()` returns `null` while running hot, so the asset version does not change
during development.
