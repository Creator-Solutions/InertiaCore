# Vite integration

InertiaCore includes helpers for Vite-powered applications with feature parity to Laravel's `@vite`
directive: hot-module-reload detection, manifest resolution, module preloading, CSS handling, CSP
nonces, integrity hashes and prefetching.

## Register

```csharp
builder.Services.AddViteHelper();
```

## Use in your layout

```html
@Vite.Input("src/main.ts")
```

Multiple entry points:

```html
@Vite.Input("src/main.ts", "src/admin.ts")
```

Async rendering (uses the cached manifest read):

```html
@await Vite.InputAsync("src/main.ts")
```

React HMR support:

```html
@Vite.ReactRefresh()
```

Resolve a manifest entry to its public URL, or read its content:

```html
<img src="@Vite.Asset("src/logo.svg")" />
```

```csharp
var svg = Vite.Content("src/logo.svg");
```

## Configuration

```csharp
builder.Services.AddViteHelper(options =>
{
    options.HotFile = "hot";
    options.BuildDirectory = "build";
    options.ManifestFilename = "manifest.json";
    options.PublicDirectory = "wwwroot";

    // CSP nonce — static, or resolved per request.
    options.CspNonce = "nonce-value";
    options.NonceResolver = () => httpContextAccessor.HttpContext?.Items["csp-nonce"] as string;

    // Subresource integrity (null disables it).
    options.IntegrityKey = "integrity";

    // Prefetch dynamic imports after load.
    options.PrefetchStrategy = PrefetchStrategy.Aggressive;
    options.PrefetchConcurrency = 3;   // waterfall only
    options.PrefetchEvent = "load";

    // Append custom attributes to generated tags.
    options.ScriptTagAttributes.Add((src, url, chunk) =>
        new Dictionary<string, object?> { ["defer"] = true });
});
```

Each rendered entry point produces a module preload link plus its script tag, walks manifest
`imports` recursively, and emits stylesheet preloads before scripts.

`Vite.GetManifestHash()` returns `null` while running hot so the asset version does not change
during development. See [Asset versioning](/guide/versioning) to wire it into the version resolver.

## Dev server

`UseViteDevServer()` starts the dev server as part of `dotnet run`, so the frontend comes up with
HMR without a second terminal. It runs the configured package-manager script, waits for the hot
file to appear and stops the process when the application shuts down.

```csharp
var app = builder.Build();

app.UseRouting();
app.UseInertia();

// Development only: a no-op outside the Development environment.
app.UseViteDevServer();
```

Configure the package manager, script and working directory when the defaults do not fit:

```csharp
app.UseViteDevServer(options =>
{
    options.PackageManager = VitePackageManager.Pnpm;
    options.ScriptName = "dev";
    options.WorkingDirectory = "frontend";
    options.ReadyTimeout = TimeSpan.FromSeconds(60);
});
```

The same values can come from the `Vite:DevServer` configuration section, which is bound before
the delegate runs:

```json
{
  "Vite": {
    "DevServer": {
      "PackageManager": "pnpm",
      "ScriptName": "dev",
      "WorkingDirectory": "frontend"
    }
  }
}
```

The helper is a no-op when the dev server is already running: the hot file exists and the URL it
contains is reachable, so a Vite instance you started by hand is left alone. By default the
working directory is the nearest ancestor of the content root that contains a `package.json`; the
hot file is looked up under `ViteOptions.PublicDirectory`.

On Windows the package manager is run through `cmd.exe` so the `npm.cmd`/`pnpm.cmd`/`yarn.cmd`/`bun`
shims resolve. Shutdown kills the whole process tree, so `Ctrl+C` leaves no `node` process behind.
