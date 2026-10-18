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
