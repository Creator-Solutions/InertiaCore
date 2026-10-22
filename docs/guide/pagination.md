# Pagination

InertiaCore includes built-in pagination helpers for `IQueryable<T>` that produce the same shape as Inertia's SSR response format, with `data`, `links`, and `meta`.

## Overview

The `ToInertiaPageAsync()` extension method paginates a query source with clamping and link generation:

```csharp
var result = db.Todos
    .OrderBy(t => t.Title)
    .ToInertiaPageAsync(page: 2, pageSize: 10);
```

The result follows the Inertia pattern of returning:

- **`data`** – the page of items
- **`links`** – navigation to First/Last/Prev/Next pages
- **`meta`** – pagination metadata (current page, total pages, item range)

## Quick Start

### Installation

```bash
dotnet add package InertiaCore.Pagination
```

### Setup

In `Program.cs`:

```csharp
builder.Services.AddInertiaPagination();
builder.Services.AddDbContext<MyDbContext>();

app.UseInertiaPagination();
```

### Usage

```csharp
// Entity set
var result = await db.Todos
    .OrderBy(t => t.Title)
    .ToInertiaPageAsync(page: 1, pageSize: 15);

// With projection for DTO mapping
var result = await db.Todos
    .OrderBy(t => t.Title)
    .ToInertiaPageAsync(t => new TodoDto(t.Id, t.Title), page: 2);

// With ambient request for link generation (use IHttpContextAccessor)
var request = httpContextAccessor.HttpContext?.Request;
var result = await db.Todos
    .OrderBy(t => t.Title)
    .ToInertiaPageAsync(request, page: 1);
```

### Result Shape

The result type is generic:

```csharp
public sealed record InertiaPagedResult<T>
{
    public IReadOnlyList<T> Data { get; init; }
    public InertiaPaginationLinks Links { get; init; }
    public InertiaPaginationMeta Meta { get; init; }
}
```

**Links**: Navigation buttons (may be null)
- `First` – first page (null if result set empty)
- `Last` – last page (null if result set empty)
- `Prev` – previous page (null on first page)
- `Next` – next page (null on last page)

**Meta**: Pagination metadata
- `CurrentPage` – current page (1‑based)
- `LastPage` – total number of pages
- `PerPage` – items per page
- `Total` – total items in result set
- `From`/`To` – inclusive range of displayed items (null if empty)
- `Path` – normalized endpoint path

## Configuration

### Options

Configure pagination behavior via `InertiaPaginationOptions`:

```csharp
builder.Services.AddInertiaPagination(options =>
{
    options.DefaultPageSize = 20;     // Default items per page
    options.MaxPageSize = 100;        // Maximum items per page
    options.PageParameterName = "page"; // Query param for page number
    options.RequireOrdering = true;   // Throw if query is not ordered
});
```

### Page Clamping

Both page number and page size are clamped to valid ranges:

- **Page numbers** – clamped to 1..LastPage
- **Page sizes** – clamped to Min(1, Max(MaxPageSize, DefaultPageSize))

Example: `MaxPageSize=50`, `DefaultPageSize=20`
- `pageSize=5` → 5 (within range)
- `pageSize=0` → 20 (uses default)
- `pageSize=100` → 50 (clamped to max)

### Ordering Requirement

```csharp
options.RequireOrdering = false; // Allow unordered queries
```

When enabled (default), `ToInertiaPageAsync()` throws:

```
InvalidOperationException: The query is not ordered. Apply OrderBy/OrderByDescending before ToInertiaPageAsync so page boundaries are stable; set InertiaPaginationOptions.RequireOrdering to false to allow unordered queries.
```

## URL Generation

Pagination links preserve the current request path and all query parameters except the page parameter:

```
GET /todos?page=2&filter=active
```

Generated links:

```
First  → /todos?filter=active
Prev   → /todos?page=1&filter=active
Next   → /todos?page=3&filter=active
Last   → /todos?page=10&filter=active
```

The ambient request context is used when calling the overload without an explicit `HttpRequest`:

```csharp
var result = await db.Todos.ToInertiaPageAsync(page: 2); // Uses IHttpContextAccessor
```

## Integration Tests

The test project includes comprehensive integration tests with SQLite in-memory database:

- **Test data**: 25 `Todo` items
- **Scenarios**:
  - First/middle/last pages
  - Out-of-range page numbers
  - Different page sizes
  - Unordered query scenarios
  - Link generation and normalization

Run the tests:

```bash
dotnet test InertiaCoreTests.Pagination
```

## TypeScript Generation

If you use InertiaCore.TypeGen, the pagination result types are automatically included in the generated TypeScript declarations, providing full IntelliSense in your frontend application.

## Migration

**From custom pagination**: If you previously had custom pagination logic, replace it with:

```csharp
var result = query.ToInertiaPageAsync(page: req.Query.Int32("page"), pageSize: req.Query.Int32("pageSize", 20));
```

The pagination helpers provide the same `data`/`links`/`meta` shape used by SSR, eliminating the need for custom pagination adapters.

## See Also

- [SSR documentation](/guide/ssr) – For SSR response handling
- [InertiaCore on NuGet](https://www.nuget.org/packages/InertiaCore.AspNetCore) – Main package
- [GitHub repository](https://github.com/Creator-Solutions/InertiaCore) – Source code and issues
