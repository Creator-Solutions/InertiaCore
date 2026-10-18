# Advanced

## History state

`encryptHistory` and `clearHistory` are emitted in the page object when set, so the client can
encrypt the current history state or clear any encrypted state:

```csharp
public IActionResult Index()
{
    Inertia.EncryptHistory(); // emit "encryptHistory": true
    return Inertia.Render("Dashboard");
}
```

When both are left unset they are omitted from the page object.

## Preserving fragments

If a redirect target contains a URL fragment (`/article/new#section`), the adapter returns a
`409 Conflict` with `X-Inertia-Redirect`, and the client performs a fresh Inertia `GET` to that URL
instead of a full-page reload. Prefetch requests are excluded.

To carry the **original** request's fragment to a redirect target that has none, call
`PreserveInertiaFragment()` before redirecting:

```csharp
public IActionResult Show(string slug)
{
    HttpContext.PreserveInertiaFragment();
    return Redirect("/article/new-slug"); // client ends up on /article/new-slug#section
}
```

The following request's page object carries `preserveFragment: true`. The flag is transported
through `TempData`, so a TempData/session provider must be configured.

## Prefetching

Inertia prefetch requests set the `Purpose: prefetch` header. Detect them with
`HttpContext.IsInertiaPrefetch()`. Prefetch requests do not consume flash data, so the eventual real
visit still receives it.

```csharp
if (!HttpContext.IsInertiaPrefetch())
{
    // Only run side effects for real visits.
    await _service.MarkSeenAsync();
}
```
