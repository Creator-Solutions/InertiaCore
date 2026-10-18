# Validation

InertiaCore maps ASP.NET Core `ModelState` errors into the Inertia validation response format, so
your frontend receives a predictable error payload.

## Error response shape

```json
{
  "errors": {
    "default": {
      "email": [
        "The email field is required."
      ]
    }
  }
}
```

Features:

- Automatic validation error collection from `ModelState`
- Named validation error bags
- `X-Inertia-Error-Bag` support
- Standardized string-array error responses

## Error bags

By default errors are placed in the `default` bag. To place errors in a named bag, send the
`X-Inertia-Error-Bag` header from the client; the adapter reads it and nests the errors under that
key, so forms on the same page can track their errors independently.

## Minimal APIs

Call `WithInertiaValidation()` on an endpoint to validate its bound arguments with
DataAnnotations before the handler runs. Apply it to a route group to opt in every endpoint in the group.

```csharp
app.MapPost("/orders", (OrderInput input) => Results.Redirect("/orders"))
    .WithInertiaValidation();

app.MapGroup("/settings")
    .WithInertiaValidation()
    .MapPost("/profile", (ProfileInput input) => Results.Ok());
```

Failures are reported as follows:

- **Inertia requests** write the errors to the bag named by `X-Inertia-Error-Bag` (or `default`)
  and receive a `303 See Other` back to the referrer. The errors are flashed through
  `IInertiaFlashProvider` and restored on the next page render. Without a flash provider the
  response is a `422` validation problem instead.
- **Other requests** receive a `422` validation problem.
- **Valid requests** reach the handler unchanged. Endpoints without validation attributes are not
  affected.

Error keys use camelCase field names, dotted paths for nested objects and numeric indexes for
collection elements:

```json
{
  "errors": {
    "name": ["The Name field is required."],
    "address.street": ["The Street field is required."],
    "items.0.name": ["The Name field is required."]
  }
}
```

### .NET 10 and built-in validation

On .NET 10 and later, `builder.Services.AddValidation()` runs the framework's own endpoint
validation. `WithInertiaValidation()` does not validate the arguments a second time in that case.
`AddInertia()` registers an Inertia problem-details writer that answers the framework's failure
with the same behaviour described above. This applies only to endpoints that opted in, and the
writer needs no configuration.

Non-Inertia failures on these endpoints are written through the other registered
`IProblemDetailsWriter`s, so a custom writer still applies to the `422` body.

### Hosts without MVC

Flash data is stored in TempData. MVC provides TempData when `AddControllers` or
`AddControllersWithViews` is called. Without it, `AddInertia()` stores the flash in an encrypted
cookie, so a `303` redirect still carries its errors on a Minimal API-only host. The cookie is
written when the response starts.

### Composition

- `WithInertiaPrecognition()` and `WithInertiaValidation()` can be applied in either order. A
  precognitive request is answered by the Precognition response (`204` or `422`) in both cases,
  and on .NET 10 the framework's validation failure is answered the same way.
- Precognition endpoints do not need `DisableValidation()` on .NET 10.

## Client usage

Errors are read from the `errors` page prop, keyed by field name. With Vue:

```vue
<script setup>
import { usePage } from '@inertiajs/vue3'

const page = usePage()
</script>

<template>
  <input v-model="form.email" />
  <p v-if="page.props.errors.email">{{ page.props.errors.email[0] }}</p>
</template>
```

With React you can read the same prop through the typed page hook. See
[Typed declarations](/typescript/declarations).

## Related

- [Precognition](/guide/precognition) validates a form against the real server-side rules without
  running the action.
