# Native AOT

Minimal APIs can be published with `PublishAot=true`. InertiaCore sets `IsTrimmable`
and `IsAotCompatible` and ships an AOT-published sample under `samples/aot`.

Use a source-generated `JsonSerializerContext` and concrete props types:

```csharp
builder.Services.AddInertia(options =>
{
    options.JsonSerializerOptions = new JsonSerializerOptions
    {
        TypeInfoResolver = MyJsonContext.Default
    };
});
```

Unsupported under AOT includes anonymous object props without a context and other
reflection-based fallbacks.
