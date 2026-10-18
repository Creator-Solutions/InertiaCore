# Installation

## Install the package

Install `InertiaCore.AspNetCore` from NuGet:

::: code-group

```bash [.NET CLI]
dotnet add package InertiaCore.AspNetCore
```

```powershell [Package Manager]
Install-Package InertiaCore.AspNetCore
```

:::

## Register services and middleware

Add Inertia services:

```csharp
using InertiaCore.Extensions;

builder.Services.AddInertia();
```

Add middleware:

```csharp
app.UseInertia();
```

Your application is now ready to serve Inertia responses.

## Create your root view

The root view is the single HTML document the client mounts into. Create `Views/App.cshtml`:

```html
@using InertiaCore

<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8"/>
    <meta name="viewport" content="width=device-width, initial-scale=1.0"/>

    <title inertia>
        My Application
    </title>

    @await Inertia.Head(Model)
</head>

<body>

@await Inertia.Html(Model)

<script type="module" src="/src/main.ts"></script>

</body>
</html>
```

The `src/main.ts` entry point is your Inertia client bootstrap:

::: code-group

```ts [Vue]
import { createApp, h } from 'vue'
import { createInertiaApp } from '@inertiajs/vue3'

createInertiaApp({
  resolve: (name) => {
    const pages = import.meta.glob('./Pages/**/*.vue', { eager: true })
    return pages[`./Pages/${name}.vue`]
  },
  setup({ el, App, props, plugin }) {
    createApp({ render: () => h(App, props) })
      .use(plugin)
      .mount(el)
  }
})
```

```tsx [React]
import { createRoot } from 'react-dom/client'
import { createInertiaApp } from '@inertiajs/react'

createInertiaApp({
  resolve: (name) => {
    const pages = import.meta.glob('./Pages/**/*.tsx', { eager: true })
    return pages[`./Pages/${name}.tsx`]
  },
  setup({ el, App, props }) {
    createRoot(el).render(<App {...props} />)
  }
})
```

:::

## Render your first page

Render an Inertia page:

```csharp
public IActionResult Index()
{
    return Inertia.Render("Dashboard", new
    {
        User = user
    });
}
```

This renders `Pages/Dashboard.vue` (or `Pages/Dashboard.tsx`) with a `user` prop.

## Configure Inertia

Customize the adapter through `AddInertia`:

```csharp
builder.Services.AddInertia(options =>
{
    options.RootView = "~/Views/App.cshtml";

    options.SsrEnabled = true;

    options.SsrUrl =
        "http://127.0.0.1:13714/render";
});
```

See the [Configuration reference](/reference/configuration) for every option.

## Next steps

- Learn how to render pages and handle responses in [Pages & responses](/guide/responses).
- Work with data using [Props](/guide/props).
- Add Vite-powered asset loading with the [Vite integration](/guide/vite).
- Get compile-time safety on your frontend with [TypeScript](/typescript/).
