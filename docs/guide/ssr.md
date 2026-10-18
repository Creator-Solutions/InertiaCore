# Server-side rendering

InertiaCore has built-in support for Inertia SSR through a render gateway. Enable it and point the
adapter at your SSR server:

```csharp
builder.Services.AddInertia(options =>
{
    options.SsrEnabled = true;
});
```

Configure your SSR endpoint:

```csharp
builder.Services.AddInertia(options =>
{
    options.SsrUrl = "http://127.0.0.1:13714/render";
});
```

When SSR is enabled, the adapter sends the page object to the gateway and inlines the returned
HTML into the root view, so the first paint is server-rendered while the client hydrates normally.

::: tip SSR server
Run the SSR server as a separate process (typically a small Node app that imports your
`createInertiaApp` setup and renders to a string). See the
[Inertia.js SSR documentation](https://inertiajs.com/server-side-rendering) for the client side.
:::

::: warning Hardening
Treat the SSR gateway as a trusted, locally-reachable service. If the gateway is unreachable the
adapter falls back to a client-side render rather than failing the request. See
[Security](/security) for the hardening checklist.
:::
