using InertiaCore;
using InertiaCore.Contracts;
using InertiaCore.Models;
using InertiaCore.Props;
using InertiaCore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace InertiaCoreTests;

public sealed record TypedUsersProps(IReadOnlyList<string> Users, int Page);

public sealed record TypedUnmappedProps(string Name);

public sealed record TypedWrapperProps(string Title, object? Lazy, object? Always);

public partial class Tests
{
    private InertiaService CreateInertiaService() =>
        new(_factory, new InertiaState(), Options.Create(new InertiaOptions()));

    [Test]
    [Description("The registry maps a props type to a component name.")]
    public void Registry_TypeMapping()
    {
        InertiaPageRegistry.Register(typeof(TypedUsersProps), "Users/Index");

        Assert.Multiple(() =>
        {
            Assert.That(InertiaPageRegistry.GetComponent(typeof(TypedUsersProps)), Is.EqualTo("Users/Index"));
            Assert.That(InertiaPageRegistry.TryGetComponent(typeof(TypedUsersProps), out var component), Is.True);
            Assert.That(component, Is.EqualTo("Users/Index"));
            Assert.That(InertiaPageRegistry.GetComponent(typeof(TypedUnmappedProps)), Is.Null);
        });
    }

    [Test]
    [Description("Render<T> resolves the component from the props type.")]
    public async Task TypedRender_UsesResolvedComponent()
    {
        InertiaPageRegistry.Register(typeof(TypedUsersProps), "Users/Index");
        var service = CreateInertiaService();

        var result = await service.Render(new TypedUsersProps(new[] { "a" }, 1));
        var response = (Response)result;
        response.SetContext(PrepareContext());
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.Multiple(() =>
        {
            Assert.That(page?.Component, Is.EqualTo("Users/Index"));
            Assert.That(page?.Props.ContainsKey("users"), Is.True);
            Assert.That(page?.Props.ContainsKey("page"), Is.True);
        });
    }

    [Test]
    [Description("Render<T>(props) produces the same page as Render(component, props).")]
    public async Task TypedRender_MatchesStringOverload()
    {
        InertiaPageRegistry.Register(typeof(TypedUsersProps), "Users/Index");
        var service = CreateInertiaService();

        var typedResult = await service.Render(new TypedUsersProps(new[] { "a", "b" }, 2));
        var typedResponse = (Response)typedResult;
        typedResponse.SetContext(PrepareContext());
        await typedResponse.ProcessResponse();
        var typedPage = typedResponse.GetJson().Value as Page;

        var stringResponse = _factory.Render("Users/Index", new { Users = new[] { "a", "b" }, Page = 2 });
        stringResponse.SetContext(PrepareContext());
        await stringResponse.ProcessResponse();
        var stringPage = stringResponse.GetJson().Value as Page;

        Assert.Multiple(() =>
        {
            Assert.That(typedPage?.Component, Is.EqualTo(stringPage?.Component));
            Assert.That(typedPage?.Props, Is.EqualTo(stringPage?.Props));
        });
    }

    [Test]
    [Description("Render<T> for an unmapped type throws a clear InvalidOperationException.")]
    public void TypedRender_UnmappedType_Throws()
    {
        var service = CreateInertiaService();

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.Render(new TypedUnmappedProps("x")));

        Assert.That(exception!.Message, Does.Contain(nameof(TypedUnmappedProps)));
    }

    [Test]
    [Description("IInertia.Render<T> default method delegates to Render(component, props).")]
    public async Task InterfaceDefault_RenderT_Delegates()
    {
        InertiaPageRegistry.Register(typeof(TypedUsersProps), "Users/Index");

        var fake = new RecordingInertia();
        var props = new TypedUsersProps(new[] { "a" }, 1);

        // Call through the interface so the default interface method runs.
        await ((IInertia)fake).Render(props);

        Assert.Multiple(() =>
        {
            Assert.That(fake.Component, Is.EqualTo("Users/Index"));
            Assert.That(fake.Props, Is.SameAs(props));
        });
    }

    /// <summary>
    /// Minimal IInertia implementation that records the call to
    /// <see cref="IInertia.Render(string, object?)"/>; every other member is unused.
    /// </summary>
    private sealed class RecordingInertia : IInertia
    {
        public string? Component { get; private set; }
        public object? Props { get; private set; }

        public Task<IActionResult> Render(string component, object? props = null)
        {
            Component = component;
            Props = props;
            return Task.FromResult<IActionResult>(null!);
        }

        public IActionResult Location(string url) => throw new NotImplementedException();
        public void Share(string key, object? value) => throw new NotImplementedException();
        public void Share(IDictionary<string, object?> data) => throw new NotImplementedException();
        public void Flash(string key, object? value) => throw new NotImplementedException();
        public void Version(string? version) => throw new NotImplementedException();
        public void Version(Func<string?> versionFunc) => throw new NotImplementedException();
        public string? GetVersion() => null;
        public AlwaysProp Always(object? value) => throw new NotImplementedException();
        public AlwaysProp Always(Func<object?> callback) => throw new NotImplementedException();
        public AlwaysProp Always(Func<Task<object?>> callback) => throw new NotImplementedException();
        public LazyProp Lazy(Func<object?> callback) => throw new NotImplementedException();
        public LazyProp Lazy(Func<Task<object?>> callback) => throw new NotImplementedException();
        public DeferredProp Defer(Func<object?> factory) => throw new NotImplementedException();
        public DeferredProp Defer(Func<Task<object?>> factory) => throw new NotImplementedException();
        public DeferredProp Defer(Func<object?> factory, string group) => throw new NotImplementedException();
        public DeferredProp Defer(Func<object?> factory, string group, bool rescue) => throw new NotImplementedException();
        public DeferredProp Defer(Func<Task<object?>> factory, string group) => throw new NotImplementedException();
        public DeferredProp Defer(Func<Task<object?>> factory, string group, bool rescue) => throw new NotImplementedException();
        public OnceProp Once(Func<object?> callback) => throw new NotImplementedException();
        public OnceProp Once(Func<Task<object?>> callback) => throw new NotImplementedException();
        public MergeProp Merge(object? value) => throw new NotImplementedException();
        public MergeProp Merge(Func<object?> callback) => throw new NotImplementedException();
        public MergeProp Merge(Func<Task<object?>> callback) => throw new NotImplementedException();
        public MergeProp DeepMerge(object? value) => throw new NotImplementedException();
        public MergeProp DeepMerge(Func<object?> callback) => throw new NotImplementedException();
        public MergeProp DeepMerge(Func<Task<object?>> callback) => throw new NotImplementedException();
        public ScrollProp Scroll(object? items, ScrollMetadata metadata, string wrapper = "data") => throw new NotImplementedException();
        public ScrollProp Scroll(Func<object?> items, ScrollMetadata metadata, string wrapper = "data") => throw new NotImplementedException();
        public ScrollProp Scroll(Func<Task<object?>> items, ScrollMetadata metadata, string wrapper = "data") => throw new NotImplementedException();
        public void EncryptHistory(bool encrypt = true) => throw new NotImplementedException();
        public void ClearHistory(bool clear = true) => throw new NotImplementedException();
    }

    [Test]
    [Description("Deferred/Lazy/Always wrapper members on a props record keep working.")]
    public async Task TypedRender_WrapperMembers_Work()
    {
        InertiaPageRegistry.Register(typeof(TypedWrapperProps), "Wrapper/Page");
        var service = CreateInertiaService();

        var props = new TypedWrapperProps(
            "Title",
            _factory.Lazy(() => "lazy-value"),
            _factory.Always("always-value"));

        var fullResult = await service.Render(props);
        var fullResponse = (Response)fullResult;
        fullResponse.SetContext(PrepareContext());
        await fullResponse.ProcessResponse();
        var fullPage = fullResponse.GetJson().Value as Page;

        Assert.Multiple(() =>
        {
            Assert.That(fullPage?.Props["title"], Is.EqualTo("Title"));
            Assert.That(fullPage?.Props["always"], Is.EqualTo("always-value"));
            Assert.That(fullPage?.Props.ContainsKey("lazy"), Is.False, "lazy props are excluded on a full visit");
        });

        var partialResult = await service.Render(props);
        var partialResponse = (Response)partialResult;
        partialResponse.SetContext(PrepareContext(new HeaderDictionary
        {
            { "X-Inertia", "true" },
            { "X-Inertia-Partial-Component", "Wrapper/Page" },
            { "X-Inertia-Partial-Data", "lazy" }
        }));
        await partialResponse.ProcessResponse();
        var partialPage = partialResponse.GetJson().Value as Page;

        Assert.That(partialPage?.Props["lazy"], Is.EqualTo("lazy-value"));
    }
}
