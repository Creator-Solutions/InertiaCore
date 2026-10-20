# InertiaCore Aspire sample

A runnable .NET Aspire application showing the `InertiaCore.Aspire.Hosting` integration. The
AppHost runs three resources:

- `vite` — the Vite dev server for hot module reload.
- `ssr` — the Node server-side rendering process.
- `web` — the Inertia web application, wired to the other two through environment variables.

## Run

Install the frontend dependencies, then start the AppHost:

```bash
npm install
dotnet run --project InertiaCore.Sample.AppHost
```

The Aspire dashboard opens and shows all three resources with their endpoints, health and logs.

## Layout

- `InertiaCore.Sample.AppHost` — the orchestrator. Adds the Vite and SSR resources and wires the
  web project with `WithInertiaReferences`.
- `InertiaCore.Sample.Web` — the ASP.NET Core app. Registers Inertia and the Vite helper with the
  configuration-aware overloads so Aspire's app settings apply.
- `frontend` — the Vite app and the `ssr.mjs` render server.
