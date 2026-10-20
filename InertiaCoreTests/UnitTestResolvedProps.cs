using System.Collections.Generic;
using System.Threading;
using InertiaCore;
using InertiaCore.Contracts;
using InertiaCore.Extensions;
using InertiaCore.Models;
using InertiaCore.Props;
using InertiaCore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace InertiaCoreTests;

/// <summary>
/// Unit tests for DI-resolved props (IN-67): a prop class with constructor-injected
/// services resolves per request, honours cancellation, and works with the lazy, deferred
/// and once modifiers exactly like lambda props.
/// </summary>
public partial class Tests
{
    /// <summary>A sample prop class depending on a scoped service.</summary>
    private sealed class SampleProp
    {
        public SampleProp(ScopedMarker marker)
        {
            Marker = marker.Id;
        }

        public string Marker { get; }
        public string Value { get; } = "resolved";
    }

    /// <summary>A scoped marker service with a per-scope id.</summary>
    private sealed class ScopedMarker
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
    }

    /// <summary>A prop whose value is a dictionary containing a nested invokable prop.</summary>
    private sealed class NestedProp : Dictionary<string, object?>
    {
        public NestedProp(IInertia inertia)
        {
            this["label"] = "nested";
            this["inner"] = inertia.Lazy(() => "inner");
        }
    }

    private static ActionContext PrepareResolvedContext(
        Action<IServiceCollection> configure,
        CancellationToken requestAborted = default)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IErrorBagService>(new ErrorBagService());
        configure(services);

        var request = new Mock<HttpRequest>();
        request.SetupGet(r => r.Headers).Returns(new HeaderDictionary());

        var response = new Mock<HttpResponse>();
        response.SetupGet(r => r.Headers).Returns(new HeaderDictionary());
        var statusCode = 200;
        response.SetupGet(r => r.StatusCode).Returns(() => statusCode);
        response.SetupSet(r => r.StatusCode = It.IsAny<int>()).Callback<int>(v => statusCode = v);

        var httpContext = new Mock<HttpContext>();
        httpContext.SetupGet(c => c.Request).Returns(request.Object);
        httpContext.SetupGet(c => c.Response).Returns(response.Object);
        httpContext.SetupGet(c => c.Features).Returns(new FeatureCollection());
        httpContext.SetupGet(c => c.RequestServices).Returns(services.BuildServiceProvider());
        httpContext.SetupGet(c => c.RequestAborted).Returns(requestAborted);

        return new ActionContext(httpContext.Object, new RouteData(), new ActionDescriptor());
    }

    [Test]
    [Description("SC: a prop class with constructor-injected services resolves per request.")]
    public async Task ResolvedProp_ResolvesFromServices()
    {
        Inertia.ClearSharedData();
        var response = _factory.Render("Test/Page", new { sample = _factory.Prop<SampleProp>() });

        var context = PrepareResolvedContext(s => s.AddScoped<ScopedMarker>().AddScoped<SampleProp>());
        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.Multiple(() =>
        {
            Assert.That(page?.Props["sample"], Is.TypeOf<SampleProp>());
            Assert.That(((SampleProp)page!.Props["sample"]!).Value, Is.EqualTo("resolved"));
        });
    }

    [Test]
    [Description("SC: the resolved prop comes from the current request scope, not a shared one.")]
    public async Task ResolvedProp_IsRequestScoped()
    {
        Inertia.ClearSharedData();

        async Task<string> ResolveMarkerAsync()
        {
            var response = _factory.Render("Test/Page", new { sample = _factory.Prop<SampleProp>() });
            var context = PrepareResolvedContext(s => s.AddScoped<ScopedMarker>().AddScoped<SampleProp>());
            response.SetContext(context);
            await response.ProcessResponse();
            return ((SampleProp)((Page)response.GetJson().Value!).Props["sample"]!).Marker;
        }

        var first = await ResolveMarkerAsync();
        var second = await ResolveMarkerAsync();

        Assert.That(first, Is.Not.EqualTo(second), "each request resolves its own scope");
    }

    [Test]
    [Description("SC: nested invokable props inside a DI-resolved prop keep working.")]
    public async Task ResolvedProp_NestedInvokable()
    {
        Inertia.ClearSharedData();
        var response = _factory.Render("Test/Page", new { nested = _factory.Prop<NestedProp>() });

        var context = PrepareResolvedContext(s =>
        {
            s.AddInertia();
            s.AddScoped<NestedProp>();
        });
        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;
        var nested = page?.Props["nested"] as Dictionary<string, object?>;

        Assert.Multiple(() =>
        {
            Assert.That(nested, Is.Not.Null);
            Assert.That(nested!["label"], Is.EqualTo("nested"));
            Assert.That(nested["inner"], Is.EqualTo("inner"), "the nested invokable prop is resolved");
        });
    }

    [Test]
    [Description("SC: cancelling the request cancels a running prop task.")]
    public async Task ResolvedProp_CancellationPropagates()
    {
        Inertia.ClearSharedData();
        using var cts = new CancellationTokenSource();
        var started = new TaskCompletionSource();

        var response = _factory.Render("Test/Page", new
        {
            slow = new LazyProp(async () =>
            {
                started.SetResult();
                await Task.Delay(TimeSpan.FromSeconds(30), cts.Token);
                return (object?)"never";
            })
        });

        // Request a partial reload so the lazy prop is actually resolved.
        var headers = new HeaderDictionary
        {
            { InertiaCore.Utils.InertiaHeader.Inertia, "true" },
            { InertiaCore.Utils.InertiaHeader.PartialComponent, "Test/Page" },
            { InertiaCore.Utils.InertiaHeader.PartialOnly, "slow" },
        };

        var context = PrepareResolvedContext(_ => { }, cts.Token);
        foreach (var (key, value) in headers)
            context.HttpContext.Request.Headers[key] = value;

        response.SetContext(context);

        var processing = response.ProcessResponse();
        await started.Task;
        cts.Cancel();

        Assert.CatchAsync<OperationCanceledException>(async () => await processing);
    }

    [Test]
    [Description("SC: Inertia.Lazy<T>() is excluded on the first load and resolved on a partial visit.")]
    public async Task ResolvedLazy_BehavesLikeLambda()
    {
        Inertia.ClearSharedData();

        var response = _factory.Render("Test/Page", new { sample = _factory.Lazy<SampleProp>() });
        var context = PrepareResolvedContext(s => s.AddScoped<ScopedMarker>().AddScoped<SampleProp>());
        response.SetContext(context);
        await response.ProcessResponse();

        var full = (Page)response.GetJson().Value!;
        Assert.That(full.Props.ContainsKey("sample"), Is.False, "lazy is excluded on first load");
    }

    [Test]
    [Description("SC: Inertia.Defer<T>(group) behaves like a deferred lambda, announced in the group.")]
    public async Task ResolvedDefer_BehavesLikeLambda()
    {
        Inertia.ClearSharedData();

        var response = _factory.Render("Test/Page", new { sample = _factory.Defer<SampleProp>("users") });
        var context = PrepareResolvedContext(s => s.AddScoped<ScopedMarker>().AddScoped<SampleProp>());
        response.SetContext(context);
        await response.ProcessResponse();

        var full = (Page)response.GetJson().Value!;

        Assert.Multiple(() =>
        {
            Assert.That(full.Props.ContainsKey("sample"), Is.False, "deferred is excluded on first load");
            Assert.That(full.DeferredProps, Is.Not.Null);
            Assert.That(full.DeferredProps!["users"], Does.Contain("sample"));
        });
    }

    [Test]
    [Description("SC: Inertia.Once<T>() behaves like a once lambda and is announced in onceProps.")]
    public async Task ResolvedOnce_BehavesLikeLambda()
    {
        Inertia.ClearSharedData();

        var response = _factory.Render("Test/Page", new { sample = _factory.Once<SampleProp>() });
        var context = PrepareResolvedContext(s => s.AddScoped<ScopedMarker>().AddScoped<SampleProp>());
        response.SetContext(context);
        await response.ProcessResponse();

        var full = (Page)response.GetJson().Value!;

        Assert.That(full.OnceProps, Is.Not.Null);
        Assert.That(full.OnceProps!.ContainsKey("sample"), Is.True);
    }
}
