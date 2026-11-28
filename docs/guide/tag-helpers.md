# Tag helpers

Razor tag helpers provide an idiomatic way to render Inertia root views.

```razor
@addTagHelper *, InertiaCore.AspNetCore

<!DOCTYPE html>
<html>
<head>
    <vite-react-refresh />
    <vite-input src="src/main.tsx" />
    <inertia-head />
</head>
<body>
    <inertia />
</body>
</html>
```

These are thin wrappers over `Inertia.Html`, `Inertia.Head`, `Vite.Input` and `Vite.ReactRefresh`.
