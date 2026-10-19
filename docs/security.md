# Security

This page covers the security-sensitive parts of the adapter. For reporting vulnerabilities, see the
[security policy](https://github.com/Creator-Solutions/InertiaCore/blob/main/SECURITY.md).

## CSRF / antiforgery

Inertia sends every visit as an `XMLHttpRequest`/`fetch` call, so requests are not submitted as HTML
forms and ASP.NET Core's automatic form-field antiforgery no longer sees a hidden field. The adapter
ships an opt-in bridge that issues a JavaScript-readable `XSRF-TOKEN` cookie and configures
anti-forgery to validate the `X-XSRF-TOKEN` request header the Inertia (axios) client sends
automatically.

Enable the bridge with a flag and one middleware call, placed **after** authentication and
authorization:

```csharp
builder.Services.AddInertia(options =>
{
    options.Antiforgery.Enabled = true;
});

var app = builder.Build();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseInertia();
app.UseInertiaAntiforgery();
```

The bridge writes the cookie only when a request needs a token: a mutating request, or an Inertia
page render. Static files, health probes and other plain GETs are left untouched.

Protect endpoints the usual ways: `[AutoValidateAntiforgeryToken]` on MVC controllers and actions,
or the `WithInertiaAntiforgery()` endpoint filter on Minimal APIs (Minimal APIs have no
`RequireAntiforgery()` equivalent):

```csharp
app.MapPost("/posts", () => Results.Text("ok"))
    .WithInertiaAntiforgery();
```

Requests without a valid token get a `400`.

After signing a user in or out, rotate the token so a value issued to the previous principal cannot
be replayed:

```csharp
app.MapPost("/logout", () =>
{
    // sign the user out
    Inertia.RotateAntiforgery();
    return Results.Text("ok");
});
```

### CORS and cookie domain

The Inertia client reads the `XSRF-TOKEN` cookie in JavaScript, so the cookie and the header only
work when the browser is allowed to send credentials cross-origin. If the front end is served from a
different origin than the API:

- Set `Access-Control-Allow-Credentials: true` and use an explicit `Access-Control-Allow-Origin`
  (the wildcard `*` is rejected when credentials are allowed).
- Add `X-XSRF-TOKEN` to `Access-Control-Allow-Headers`.
- Do not set a cross-site `Cookie.Domain` for the token cookie; leave it host-only unless the
  front end and API share a registrable domain. For subdomain setups, set the cookie domain to the
  shared parent domain and remember that this widens where the cookie is sent.

### Manual wiring

If you manage anti-forgery yourself instead of using the bridge, ASP.NET Core's antiforgery service
can issue a request token that you validate from a header. Configure the header name to the one your
frontend sends and expose the token to the page (for example as a shared prop):

```csharp
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    // Inertia's Laravel-style convention uses an XSRF-TOKEN cookie plus an
    // X-XSRF-TOKEN header. Either naming works; the cookie must stay readable by
    // JavaScript, so do not set HttpOnly on it.
    options.Cookie.Name = "XSRF-TOKEN";
    options.Cookie.HttpOnly = false;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

var app = builder.Build();
app.UseInertia();

app.Use(async (context, next) =>
{
    var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
    var tokens = antiforgery.GetAndStoreTokens(context);
    Inertia.Share("csrfToken", tokens.RequestToken);
    await next();
});
```

Send the token back on every mutating request. With Inertia's `XSRF-TOKEN` cookie / `X-XSRF-TOKEN`
header convention, set `options.HeaderName = "X-XSRF-TOKEN"` and the Inertia client will attach the
cookie value automatically; otherwise set the header from your HTTP client:

```js
import axios from "axios";

axios.defaults.headers.common["X-CSRF-TOKEN"] =
  window.initialPage.props.csrfToken;
```

Finally, validate the token server-side on every `POST`/`PUT`/`PATCH`/`DELETE` (for example with a
global `AutoValidateAntiforgeryToken` filter, an endpoint filter, or
`IAntiforgery.ValidateRequestAsync`). Keep `Secure` on in production and set `SameSite`
appropriately; only the readable XSRF token cookie should omit `HttpOnly`.

## Redirect validation

`Inertia.Location(url)` and the redirect helpers do **not** validate the destination. Never pass a
user-supplied value straight through: validate it first (for example with `Url.IsLocalUrl`) or use
an allow-list of hosts. See [Location redirects](/guide/responses#location-redirects) for an
example.

## Server-side rendering

Treat the SSR gateway (see [Server-side rendering](/guide/ssr)) as a trusted, locally-reachable
service, not something exposed to the internet. If the gateway is unreachable the adapter falls back
to a client-side render rather than failing the request.

## Shared data

Request-scoped shared data (`Inertia.Share` called during a request) is never visible to other
requests or users. Global shared data (`Inertia.Share` outside a request) is merged into every
response: do not put user-specific values there.
