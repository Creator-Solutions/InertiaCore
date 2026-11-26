# Serialization

InertiaCore serializes page props with `System.Text.Json`. Configure the options on
`InertiaOptions`:

```csharp
builder.Services.AddInertia(options =>
{
    options.JsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        TypeInfoResolver = MyJsonContext.Default
    };
});
```

Anonymous objects and dictionaries continue to work because the consumer's resolver is
chained with the default reflection-based resolver.
