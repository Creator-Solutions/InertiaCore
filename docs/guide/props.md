# Props

Props are the data you pass to a page component. InertiaCore supports several prop wrappers that
control *when* and *how* data is sent to the client.

```csharp
return Inertia.Render("Posts", new
{
    Posts = posts,
    Total = Inertia.Always(() => posts.Count)
});
```

## Lazy and async props

Load expensive data only when required. A lazy prop is resolved on partial reloads that request it,
or on a full visit.

```csharp
public IActionResult Index()
{
    return Inertia.Render("Posts", new
    {
        Posts = new LazyProp(async () =>
        {
            return await _context.Posts.ToListAsync();
        })
    });
}
```

## Resolving props from the container

Instead of computing a prop inline, you can register a class with the DI container and let
InertiaCore resolve it from the request scope. The class receives its dependencies through
constructor injection, so endpoints stay free of data-access code.

```csharp
public sealed class UsersProp
{
    private readonly AppDbContext _context;

    public UsersProp(AppDbContext context) => _context = context;

    public IReadOnlyList<User> Users => _context.Users.ToList();
}

// Program.cs
builder.Services.AddScoped<UsersProp>();

// Endpoint
return Inertia.Render("Users/Index", new
{
    Users = Inertia.Prop<UsersProp>()
});
```

`Inertia.Prop<T>()` resolves `T` from `HttpContext.RequestServices` for each request and uses the
property name (`Users`) as the prop key. The `IInertiaProp<T>` interface backs every resolved prop,
so you can also accept it as a parameter or return it from your own helpers.

The lazy, deferred and once modifiers accept a resolved prop too, so DI stays available on those
paths:

```csharp
return Inertia.Render("Users/Index", new
{
    Users = Inertia.Lazy<UsersProp>(),
    Stats = Inertia.Defer<StatsProp>("stats"),
    Permissions = Inertia.Once<PermissionsProp>()
});
```

Props resolve sequentially by default, so a request-scoped service shared by several props behaves
deterministically. To resolve independent props concurrently, opt in to parallel resolution:

```csharp
builder.Services.AddInertia(options =>
{
    options.PropResolution = PropResolution.Parallel;
});
```

All prop resolution (including `Func<object?>`, `Func<Task<object?>>`, and DI-resolved props)
observes `HttpContext.RequestAborted`, so client disconnects cancel in-flight work.

## Partial reloads

A partial reload lets the client request a subset of props without re-rendering the page. Send the
`X-Inertia-Partial-Data` and `X-Inertia-Partial-Component` headers, and InertiaCore resolves only
the requested props (including lazy, deferred and once props) while preserving the rest of the page
data client-side.

## Merging props

Merge props tell the client to combine incoming data with the existing page data during partial
reloads instead of replacing it (useful for "load more" pagination). A full visit always replaces
the prop.

```csharp
public IActionResult Index(int page = 1)
{
    var tags = _allTags.Skip((page - 1) * 5).Take(5);

    return Inertia.Render("Tags/Index", new
    {
        Tags = Inertia.Merge(tags)
    });
}
```

Append at the root (the default), prepend, deep merge, or target a nested path:

```csharp
Inertia.Merge(items);                        // append at the root
Inertia.Merge(items).Prepend();              // prepend at the root
Inertia.Merge(paginator).Append("data");     // merge only the "data" array
Inertia.DeepMerge(chat);                     // deep merge the whole structure
Inertia.Merge(posts).MatchOn("id");          // update items matched by id
Inertia.DeepMerge(chat).MatchOn("messages.id");
```

Merge props compose with the other prop wrappers, so a prop can be deferred and mergeable at the
same time:

```csharp
public IActionResult Index()
{
    return await _inertia.Render("Users/Index", new
    {
        Results = _inertia.Defer(() => LoadUsers()).Merge()
    });
}
```

To have the client replace a prop before merging new data, send its key in the `X-Inertia-Reset`
header (for example `X-Inertia-Reset: results`). Reset props are still returned in `props`, but
omitted from every merge array.

## Infinite scroll

`Inertia.Scroll` configures a paginated prop for Inertia's `<InfiniteScroll>` component. It emits
the pagination cursor under `scrollProps` and marks the inner array (`data` by default) for merge,
appending when loading forward and prepending when loading backward.

```csharp
public IActionResult Index(int page = 1)
{
    var users = _db.Users.OrderBy(u => u.Id).ToPagedList(page, 20);

    return Inertia.Render("Users/Index", new
    {
        Users = Inertia.Scroll(
            new { data = users.Items },
            new ScrollMetadata(
                PageName: "page",
                PreviousPage: page > 1 ? page - 1 : null,
                NextPage: users.HasMore ? page + 1 : null,
                CurrentPage: page))
    });
}
```

- The prop value is emitted as-is; the array at `data` is merged (`mergeProps: ["users.data"]`).
- The direction comes from the client's `X-Inertia-Infinite-Scroll-Merge-Intent` header (`prepend`
  or `append`).
- Cursor tokens are nullable and may be numbers or strings; `null` is serialized as `null`, never
  omitted.
- `X-Inertia-Reset: users` marks `scrollProps.users.reset = true` and removes the merge label so the
  client replaces instead of merging.
- Pass a custom wrapper as the third argument when the array key is not `data` (for example
  `Inertia.Scroll(paginator, metadata, "items")`).

## Deferred props

Deferred props are excluded from the initial response and fetched afterwards. The page object
announces them under `deferredProps`, grouped so the client can fetch groups in parallel.

```csharp
return Inertia.Render("Users/Index", new
{
    Users = _db.Users.ToList(),
    Permissions = Inertia.Defer(() => LoadPermissions()),
    Teams = Inertia.Defer(() => LoadTeams(), "attributes"),
    Projects = Inertia.Defer(() => LoadProjects(), "attributes")
});
```

The response contains `deferredProps: { "default": ["permissions"], "attributes": ["teams",
"projects"] }` and omits the values. The client requests each group with a partial reload (`only`),
and the callbacks are only invoked when requested.

## Once props

Once props are resolved a single time and remembered by the client, then reused on subsequent pages
that include them.

```csharp
return Inertia.Render("Billing", new
{
    Plans = Inertia.Once(() => Plan.All()).Until(TimeSpan.FromHours(1))
});
```

The page object emits `onceProps` (`{ "plans": { "prop": "plans", "expiresAt": ... } }`). On later
visits the client lists the once keys it already holds in the `X-Inertia-Except-Once-Props` header,
and the callback is skipped and the prop omitted. A partial reload always resolves a requested once
prop. Use `.As("key")` to share data under a different name and `.Fresh()` to force a refresh.

## Rescuing deferred props

A deferred prop can be resolved with rescue enabled, so a failure is logged, the value omitted, and
the key reported through `rescuedProps` instead of failing the whole request.

```csharp
return Inertia.Render("Users/Index", new
{
    Permissions = Inertia.Defer(() => LoadPermissions(), "default", rescue: true)
    // or: Inertia.Defer(() => LoadPermissions()).Rescue()
});
```

A response with `rescuedProps: ["permissions"]` and a `200` status lets the client render an error
state. Exceptions from props without rescue still fail the request.
