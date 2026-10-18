# Pages & responses

An Inertia response is produced by `Inertia.Render`, which returns a page object the client turns
into a component render. It works identically from controllers and endpoint handlers.

::: code-group

```csharp [MVC]
public IActionResult Index()
{
    return Inertia.Render("Dashboard", new
    {
        User = user
    });
}
```

```csharp [Minimal API]
app.MapGet("/dashboard", (UserService users) =>
{
    return Inertia.Render("Dashboard", new
    {
        User = users.Current
    });
});
```

:::

## Redirects

Inertia-compliant redirects are handled automatically for every verb:

- GET redirects
- POST redirects
- PUT redirects
- PATCH redirects
- DELETE redirects

Non-GET requests automatically use `303 See Other`, so the browser follows the redirect with a
`GET`.

```csharp
public IActionResult Store(CreateUserRequest request)
{
    _db.Users.Add(request.ToUser());
    _db.SaveChanges();

    return RedirectToAction("Index");
}
```

## Location redirects

`Inertia.Location(url)` performs a full-page, cross-origin redirect outside the Inertia visit. Use
it when you intend a real navigation (for example to an external OAuth provider).

::: warning Open redirects
`Inertia.Location(url)` and the redirect helpers do **not** validate the destination. Passing a
user-supplied value straight through creates an open redirect. Validate the target before
redirecting. For example, only accept local URLs, or an allow-listed set of hosts:

```csharp
public IActionResult AfterLogin(string? returnUrl)
{
    if (Url.IsLocalUrl(returnUrl))
        return Redirect(returnUrl!);

    return RedirectToAction("Index", "Home");
}
```

Use `Inertia.Location(url)` only when the URL is a trusted constant.
:::

## Empty response handling

Empty responses from Inertia requests are automatically converted into redirects, so an action that
returns no body does not leave the client stuck.

Handled scenarios:

```csharp
return NoContent(); // 204
```

```csharp
Response.ContentLength = 0; // 200 with no body
```

The adapter redirects users back to the previous page using:

1. The `Referer` header, but only when it is a safe relative path or points at the current scheme,
   host and port
2. The current request URL as a fallback

A `200` response with no content type is treated as empty when its content length is `null` or `0`.
