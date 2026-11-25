# Logging

InertiaCore emits structured, allocation-free log messages through `ILogger` with stable
event ids. Categories are prefixed with `InertiaCore.` (for example
`InertiaCore.Ssr.Gateway`), so adapter logs can be filtered independently of the rest of
the application.

## Usage

Logging is enabled by default through the standard ASP.NET Core logging pipeline. Filter
by category or event id:

```csharp
builder.Logging.AddFilter("InertiaCore", LogLevel.Debug);
```

Key events:

- Asset version mismatches (debug) trigger a client reload.
- SSR failures (warnings) fall back to client-side rendering.
- Prop resolution failures (errors) rescue the prop and list it in `rescuedProps`.

Event id ranges are defined in `InertiaCore.Logging.InertiaLogEvents` to avoid collisions.
No prop values, credential headers, or request bodies are ever logged.
