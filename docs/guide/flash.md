# Flash Messages

Inertia ships with a cross-request "flash" bag. You can write one or more values in a request (for example after a form submit), redirect, and the next Inertia page render receives those values exactly once under the `flash` prop. Flash is automatically cleared after that render.

## Basic usage

### From controllers (IInertia)

```csharp
public class PostsController : Controller
{
    public async Task<IActionResult> Store(CreatePostRequest request, IInertia inertia)
    {
        // save post
        await inertia.Flash("success", "Post created.");
        return RedirectToAction("Index");
    }

    public async Task<IActionResult> Index(IInertia inertia)
    {
        return await inertia.Render("Posts/Index");
    }
}
```

### From Minimal APIs

```csharp
app.MapPost("/posts", async (CreatePostRequest req, IInertia inertia, HttpContext ctx) =>
{
    // save post
    await inertia.Flash("success", "Post created.");
    ctx.Response.Headers.Location = "/posts";
    return Results.StatusCode(StatusCodes.Status303SeeOther);
});

app.MapGet("/posts", async (IInertia inertia) =>
    (IResult)await inertia.Render("Posts/Index", new { posts }));
```

### Static helpers

Outside of constructor injection, you can use the static helpers:

```csharp
app.MapPost("/logout", (HttpContext ctx) =>
{
    Inertia.Flash("notice", "You have been logged out.");
    ctx.Response.Headers.Location = "/";
    return Results.StatusCode(StatusCodes.Status303SeeOther);
});

Inertia.ClearFlash(); // clears pending flash for this request
```

## Accessing flash in the frontend

Flashed values are exposed on the page props as a single `flash` object. They are consumed after the render that receives them.

```tsx
import { usePage } from '@inertiajs/react'

export default function PostsIndex() {
  const { flash } = usePage().props
  return flash?.success ? <div>{flash.success}</div> : null
}
```

> Note: Flash is exposed as `props.flash` (not top-level flash keys). If you previously relied on top-level keys, update your components to read from `flash.*`.

## Named flash bags

The flash provider stores values in the bag named by the `X-Inertia-Error-Bag` header when present, otherwise `default`. Most applications only need the default bag. If you use multiple bags, the same naming applies.

## Prefetch behaviour

Inertia prefetch requests (`X-Inertia-Purpose: prefetch`) do **not** consume flash data. The real visit still receives the flash exactly once.

## Storage

By default, flash data is stored in an encrypted cookie (cookie-backed `InertiaFlashProvider`). This is suitable for small messages (strings, small objects).

### Large payloads (distributed cache)

If you need to flash large payloads (lists, files metadata, etc.), register the distributed-cache provider:

```csharp
builder.Services.AddDistributedMemoryCache(); // or your IDistributedCache
builder.Services.AddInertiaDistributedCacheFlash();
```

`AddInertiaDistributedCacheFlash()` replaces the default provider with `DistributedCacheFlashProvider`. It stores flash in `IDistributedCache` keyed by an encrypted `InertiaFlashCacheId` cookie with a 1-hour lifetime. Prefetch does not consume flash.

### Size limits and errors

`InertiaFlashProvider` enforces a maximum serialized payload size (UTF-8 bytes). The default is `4096` bytes. If exceeded, it throws `InertiaFlashPayloadTooLargeException`. The exception message points to `AddInertiaDistributedCacheFlash()` as the remedy.

You can raise the limit if appropriate:

```csharp
builder.Services.Configure<InertiaFlashOptions>(options =>
{
    options.MaxCookiePayloadBytes = 8192;
});
```

## Notes

- Flash integrates with validation: when `WithInertiaValidation()` (or framework validation on .NET 10) produces errors on an Inertia request, errors are flashed and the request is redirected back with `303`. They appear in `page.props.errors` on the next render.
- No breaking change to public signatures; the only behavioral change is the `flash` prop shape (single object) introduced with IN-63.