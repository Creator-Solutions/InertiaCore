# Error pages

InertiaCore can render your Inertia **error component** for the status codes that matter
(`401`, `403`, `404`, `419`, `500` and `503` out of the box) instead of leaving the client
with an empty response. The component receives the status code and a message as props.

Error pages are **opt-in** and only ever apply to Inertia requests. A plain browser request
keeps the host's default handling (an empty body, or the developer exception page in
Development).

## Enabling error pages

```csharp
builder.Services.AddInertia(options =>
{
    options.StatusPages.Enabled = true;
});
```

```csharp
var app = builder.Build();
app.UseRouting();

app.UseInertia();

// After UseRouting and before authentication/authorization, so a short-circuiting
// 401/403 is still observed.
app.UseInertiaStatusPages();

app.UseAuthentication();
app.UseAuthorization();
```

> Register `UseInertiaStatusPages()` **before** `UseAuthentication()` and
> `UseAuthorization()`. Authorization short-circuits with the final status and does not call
> further middleware, so the status-page middleware must wrap it to see a `401` or `403`.

## The error component

For each handled status the configured component is rendered with two props:

```json
{
  "component": "Error",
  "props": {
    "status": 404,
    "message": "Not Found"
  }
}
```

Render it like any other page. The component name defaults to `Error`; declare it with
`[assembly: InertiaPage("Error")]` if you use `ValidatePages`.

```tsx
export default function Error({ status, message }: { status: number; message: string }) {
  return (
    <div>
      <h1>{status}</h1>
      <p>{message}</p>
    </div>
  )
}
```

## Customising

Change the component name, or override it (and the message) per status:

```csharp
builder.Services.AddInertia(options =>
{
    options.StatusPages.Enabled = true;

    options.StatusPages.Component = "Errors/Generic";
    options.StatusPages.Components[503] = "Errors/Maintenance";
    options.StatusPages.Messages[503] = "We'll be right back.";

    // Or resolve the message dynamically:
    options.StatusPages.MessageFactory = (httpContext, status) =>
        status == 500 ? "Something went wrong." : $"Error {status}";
});
```

The handled set can be replaced by assigning `StatusCodes`:

```csharp
options.StatusPages.StatusCodes = new[] { 404, 500 };
```

### Built-in messages

| Status | Message |
| --- | --- |
| `401` | `Unauthorized` |
| `403` | `Forbidden` |
| `404` | `Not Found` |
| `419` | `Page Expired` |
| `500` | `Server Error` |
| `503` | `Service Unavailable` |

## Authentication redirects

When the client is an Inertia request, a cookie-auth **challenge** (an unauthenticated
request to an `[Authorize]` endpoint) is turned into a `409` carrying
`X-Inertia-Location: <login url>` instead of a bare `302`. The Inertia client follows this as
a full-page visit to the login URL; a `302` would instead be followed as an XHR redirect and
show an empty page.

This conversion is registered by `AddInertia()` and is active whenever
`StatusPages.Enabled` is `true`. Non-Inertia requests keep the ordinary redirect, and a
**forbidden** request (`403`) is rendered through the error component.

`AddInertia()` replaces the registered `IAuthorizationMiddlewareResultHandler`. If you need a
custom handler, register it **after** `AddInertia()`.

## Development exceptions

In Development, an unhandled exception produces a `500`. By default the framework's
developer exception page is preserved for Inertia requests too, so you keep the stack trace
while developing. Set `RespectDeveloperExceptionPage = false` to always render the Inertia
error page for `500`.

## Notes

- Only Inertia requests (`X-Inertia: true`) are affected. Non-Inertia requests and responses
  that already wrote a body are left untouched.
- A response is only rewritten when nothing has been written yet, so the developer exception
  page, `ProblemDetails` and streamed responses are preserved.
