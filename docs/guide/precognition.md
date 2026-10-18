# Precognition

[Precognition](https://inertiajs.com/docs/v2/the-basics/forms#precognition) lets the client validate
a form against the real server-side rules without running the action. InertiaCore detects
`Precognition: true` requests, validates them, and short-circuits with `204`/`422`. The
endpoint/action body never runs.

The response follows the Inertia protocol:

- **Valid**: `204 No Content` with `Precognition-Success: true`.
- **Invalid**: `422 Unprocessable Entity` with `{ "errors": { field: [messages] } }`.
- `Precognition-Validate-Only: field1,field2` limits the returned errors to those fields.
- `Vary: Precognition` is added to the response.

## MVC controllers

Precognition is enabled globally; no per-action setup is required. When the request carries
`Precognition: true`, the action filter validates the model state and short-circuits before the
action runs (and before MVC's automatic `400`):

```csharp
public class UsersController : Controller
{
    [HttpPost]
    public IActionResult Store(CreateUserRequest request)
    {
        // Not reached for precognitive or invalid requests.
        _db.Users.Add(request.ToUser());
        return RedirectToAction("Index");
    }
}
```

## Minimal APIs

Endpoint filters run after argument binding, so keep side effects inside the handler body. Opt in
per endpoint with `WithInertiaPrecognition()`:

```csharp
app.MapPost("/users", (CreateUserRequest request) =>
{
    // Not reached for precognitive requests.
    db.Users.Add(request.ToUser());
    return Results.Created("/users", null);
})
.WithInertiaPrecognition();
```

## Validation engine

The default validator applies
[`System.ComponentModel.DataAnnotations`](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations)
(including `IValidatableObject`) to the bound arguments. To use a different engine (for example
FluentValidation), register your own `IInertiaRequestValidator` after `AddInertia()`:

```csharp
builder.Services.AddInertia();
builder.Services.AddScoped<IInertiaRequestValidator, FluentValidationRequestValidator>();
```
