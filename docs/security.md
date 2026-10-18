# Security

This page covers the security-sensitive parts of the adapter. For reporting vulnerabilities, see the
[security policy](https://github.com/Creator-Solutions/InertiaCore/blob/main/SECURITY.md).

## CSRF / antiforgery

Inertia sends every visit as an `XMLHttpRequest`/`fetch` call, so requests are not submitted as HTML
forms and ASP.NET Core's automatic form-field antiforgery no longer sees a hidden field. **The
adapter does not wire up CSRF protection for you**: you must do it explicitly.

ASP.NET Core's antiforgery service issues a request token and validates it from a request header.
Configure the header name to the one your frontend will send and expose the token to the page (for
example as a shared prop):

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
