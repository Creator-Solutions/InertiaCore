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

## Managed process

Instead of running the SSR server yourself, you can let the adapter start and manage it. Enable
`Ssr.Managed` and point it at your bundle:

```csharp
builder.Services.AddInertia(options =>
{
    options.SsrEnabled = true;
    options.Ssr.Managed = true;
    options.Ssr.NodePath = "node";
    options.Ssr.ScriptPath = "ssr.mjs";
    options.Ssr.Port = 13714;
});
```

On start the adapter launches the Node process with `NodePath ScriptPath`, waits for
`http://127.0.0.1:{Port}/health`, and forwards the process's standard output and error to
`ILogger`. When the application stops it calls `http://127.0.0.1:{Port}/shutdown` and, if the
process does not exit in time, kills the whole process tree.

If the bundle exposes the port through the `SSR_PORT` environment variable it is set from
`Port` automatically; any keys you add to `Ssr.EnvironmentVariables` are applied too and take
precedence.

Management is disabled automatically when `SsrEnabled` is `false` or when the Vite dev server is
running (the dev server renders the bundle itself). When `Ssr.Managed` is on and `SsrUrl` is left
at its default, the gateway URL is aligned with `Port`.

### Restart policy

A crash is handled by the configured `RestartPolicy`:

```csharp
builder.Services.AddInertia(options =>
{
    options.Ssr.Managed = true;
    options.Ssr.RestartPolicy = RestartPolicy.OnFailure;
    options.Ssr.InitialBackoff = TimeSpan.FromMilliseconds(500);
    options.Ssr.MaxBackoff = TimeSpan.FromSeconds(10);
    options.Ssr.MaxRestarts = 5; // -1 to restart indefinitely
});
```

With `OnFailure` the process is restarted after an exponential backoff (`InitialBackoff` doubling
until `MaxBackoff`, up to `MaxRestarts` attempts). While it is down, requests fall back to
client-side rendering. Set `RestartPolicy.None` to never restart.

The options are validated when the application starts, so an invalid `Port`, `StartupTimeout` or
backoff fails fast rather than at the first request.

