# Typed routes

`inertiatsgen routes` generates a small `routes.ts` module (a Ziggy-style API) from your named
Minimal API endpoints, so URLs are built from a typed helper instead of hand-written strings:

```csharp
app.MapGet("/users/{id:int}", GetUser).WithName("users.show");
app.MapGet("/search/{*rest}", Search).WithName("search");
```

## Generate the module

Generate from the Development route dump and pass it to the tool:

```bash
# Run the app in Development and fetch the metadata dump.
curl -s http://localhost:5000/_inertia/routes -o routes.json

inertiatsgen routes --input routes.json --output Client/types/routes.ts
```

## Use the helper

The generated module exports a literal union of route names, a params map and the `route()` helper:

```ts
import { route } from './types/routes';

route('users.index');                            // '/users'
route('users.show', { id: 5 });                  // '/users/5'
route('users.show', { id: 5, page: 2 });         // '/users/5?page=2'
route('search', { rest: ['posts', 'latest'] });  // '/search/posts/latest'

route('users.show');        // compile error: missing required 'id'
route('nope.nope');         // compile error: unknown route name
```

Constraints drive the parameter types: numeric constraints (for example `{id:int}`) accept
`string | number`, `{id:guid}` stays a `string`, and catch-all parameters accept a scalar or an
array of segments. Endpoints without `WithName(...)` are ignored, and extra values that are not part
of the route are emitted as a query string.

## The dump endpoint

`MapInertiaRoutes()` maps the Development-only dump endpoint (default `/_inertia/routes`); it
returns `404` outside Development:

```csharp
app.MapInertiaRoutes();
```

Two ways to obtain the metadata are supported, with different trade-offs:

| Approach | How | Trade-off |
| --- | --- | --- |
| Runtime dump (used above) | `MapInertiaRoutes()` + fetch `/_inertia/routes` | Routes are read from the *running* app, so it reflects exactly what is mapped, but generation needs the app to boot (and to be in Development). |
| Startup host dump | Boot the app host in-process and read `EndpointDataSource` before serving | No HTTP round-trip, but pulling this into the tool couples it to your application's hosting setup and dependencies. |

## Keep it in sync

`inertiatsgen routes --check` fails (exit code `1`) if the output file is out of date, which is how
CI keeps the committed `routes.ts` in sync with the endpoints.
