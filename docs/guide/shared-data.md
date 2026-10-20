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

The merge order is **global → shared-data providers → request-scoped → component props**, so
component props always win. When injecting `IInertia` via DI, `Share` is always request-scoped.

## Shared data providers

A shared-data provider computes shared props per request from services, without touching
controllers. Implement `IInertiaSharedDataProvider` and register it with
`AddInertiaSharedData<TProvider>()`:

```csharp
public sealed class TeamSharedDataProvider : IInertiaSharedDataProvider
{
    private readonly ITeamService _teams;

    public TeamSharedDataProvider(ITeamService teams) => _teams = teams;

    public async ValueTask<IReadOnlyDictionary<string, object?>> GetSharedDataAsync(
        HttpContext context, CancellationToken cancellationToken)
    {
        var team = await _teams.GetCurrentAsync(context.User, cancellationToken);
        return new Dictionary<string, object?>
        {
            ["team"] = new { team.Id, team.Name }
        };
    }
}
```

```csharp
// Program.cs
builder.Services.AddInertiaSharedData<TeamSharedDataProvider>();
```

Providers are resolved **per request** from the request's service provider, so they may depend on
scoped services such as `DbContext`. Register multiple providers to split responsibilities; they
merge in **registration order**, so a provider registered later overrides an earlier one:
`global → providers (registration order) → request-scoped → component props`.

Provider keys are announced in `sharedProps` alongside the keys registered via `Inertia.Share`.
A provider may return any value the pipeline understands, including a lazy prop, which is deferred
on the first load and resolved on a partial visit:

```csharp
["notifications"] = inertia.Lazy(() => _notifications.CountFor(userId))
```

A provider that throws fails the request with an `InvalidOperationException` that names the
provider type.

### Built-in claims provider

Enable `InertiaOptions.ClaimsSharedData` to share `auth.user` from the current
`ClaimsPrincipal`. Map claim types to prop names; the result is nested under `Key`
(default `auth`):

```csharp
builder.Services.AddInertia(options =>
{
    options.ClaimsSharedData.Enabled = true;
    options.ClaimsSharedData.Key = "auth";
    options.ClaimsSharedData.ClaimMapping["name"] = ClaimTypes.Name;
    options.ClaimsSharedData.ClaimMapping["role"] = ClaimTypes.Role;
});
```

This contributes:

```json
{ "auth": { "user": { "name": "Ada", "role": "admin" } } }
```

The provider contributes nothing for an anonymous user, and `auth.user` overrides any global or
provider `auth` prop but is itself overridden by a request-scoped or component `auth` prop.

## Shared props metadata

The keys registered via `Inertia.Share` (global and request-scoped) and contributed by shared-data
providers are emitted as `sharedProps`, which the client uses to carry shared props over during
instant visits:

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
