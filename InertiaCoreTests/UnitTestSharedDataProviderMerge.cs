using System.Security.Claims;
using InertiaCore;
using InertiaCore.Extensions;
using InertiaCore.Models;
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
/// Unit tests for the shared-data provider merge order (IN-66): global → providers
/// (registration order) → request-scoped → component, and for the built-in claims provider.
/// </summary>
public partial class Tests
{
    /// <summary>A provider that contributes a fixed key/value.</summary>
    private sealed class FixedProvider : IInertiaSharedDataProvider
    {
        private readonly Dictionary<string, object?> _data;

        public FixedProvider(Dictionary<string, object?> data) => _data = data;

        public ValueTask<IReadOnlyDictionary<string, object?>> GetSharedDataAsync(
            HttpContext context, CancellationToken cancellationToken)
            => new(_data);
    }

    /// <summary>A provider that throws when asked for data.</summary>
    private sealed class ThrowingProvider : IInertiaSharedDataProvider
    {
        public ValueTask<IReadOnlyDictionary<string, object?>> GetSharedDataAsync(
            HttpContext context, CancellationToken cancellationToken)
            => throw new InvalidOperationException("kaboom");
    }

    private static ActionContext PrepareProviderContext(
        IEnumerable<IInertiaSharedDataProvider> providers,
        ClaimsPrincipal? user = null)
    {
        var request = new Mock<HttpRequest>();
        request.SetupGet(r => r.Headers).Returns(new HeaderDictionary());

        var response = new Mock<HttpResponse>();
        response.SetupGet(r => r.Headers).Returns(new HeaderDictionary());
        var statusCode = 200;
        response.SetupGet(r => r.StatusCode).Returns(() => statusCode);
        response.SetupSet(r => r.StatusCode = It.IsAny<int>()).Callback<int>(v => statusCode = v);

        var services = new ServiceCollection();
        foreach (var provider in providers)
            services.AddSingleton(provider);
        services.AddSingleton<IErrorBagService>(new ErrorBagService());

        var httpContext = new Mock<HttpContext>();
        httpContext.SetupGet(c => c.Request).Returns(request.Object);
        httpContext.SetupGet(c => c.Response).Returns(response.Object);
        httpContext.SetupGet(c => c.Features).Returns(new FeatureCollection());
        httpContext.SetupGet(c => c.RequestServices).Returns(services.BuildServiceProvider());
        if (user is not null)
            httpContext.SetupGet(c => c.User).Returns(user);

        return new ActionContext(httpContext.Object, new RouteData(), new ActionDescriptor());
    }

    [Test]
    [Description("SC: providers merge in registration order, and a later provider overrides an earlier one.")]
    public async Task Providers_MergeInRegistrationOrder()
    {
        Inertia.ClearSharedData();
        _factory.Share("key", "request");

        var response = _factory.Render("Test/Page", new { component = "component" });

        var context = PrepareProviderContext(new IInertiaSharedDataProvider[]
        {
            new FixedProvider(new Dictionary<string, object?> { ["key"] = "first", ["onlyFirst"] = "a" }),
            new FixedProvider(new Dictionary<string, object?> { ["key"] = "second" }),
        });

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.Multiple(() =>
        {
            // Request-scoped overrides providers.
            Assert.That(page?.Props["key"], Is.EqualTo("request"));
            // Component props override everything.
            Assert.That(page?.Props["component"], Is.EqualTo("component"));
            // A key only the first provider had survives.
            Assert.That(page?.Props["onlyFirst"], Is.EqualTo("a"));
        });
    }

    [Test]
    [Description("SC: global shared props are overridden by providers.")]
    public async Task Providers_OverrideGlobal()
    {
        Inertia.ClearSharedData();
        Inertia.Share("key", "global");

        var response = _factory.Render("Test/Page", new { });
        var context = PrepareProviderContext(new IInertiaSharedDataProvider[]
        {
            new FixedProvider(new Dictionary<string, object?> { ["key"] = "provider" }),
        });

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.That(page?.Props["key"], Is.EqualTo("provider"));
    }

    [Test]
    [Description("SC: a provider failure throws an error naming the provider type.")]
    public async Task Provider_FailureNamesType()
    {
        Inertia.ClearSharedData();
        var response = _factory.Render("Test/Page", new { });
        var context = PrepareProviderContext(new IInertiaSharedDataProvider[] { new ThrowingProvider() });

        response.SetContext(context);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => response.ProcessResponse());
        Assert.That(ex!.Message, Does.Contain(nameof(ThrowingProvider)));
        Assert.That(ex.InnerException, Is.TypeOf<InvalidOperationException>());
    }

    [Test]
    [Description("SC: the claims provider shares auth.user from mapped claims.")]
    public async Task ClaimsProvider_MapsClaims()
    {
        Inertia.ClearSharedData();

        var options = new InertiaOptions();
        options.ClaimsSharedData.Enabled = true;
        options.ClaimsSharedData.ClaimMapping["name"] = ClaimTypes.Name;
        options.ClaimsSharedData.ClaimMapping["role"] = ClaimTypes.Role;

        var provider = new ClaimsSharedDataProvider(new StaticOptions(options));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "Ada"),
            new Claim(ClaimTypes.Role, "admin"),
        }, "Test"));

        var data = await provider.GetSharedDataAsync(
            new DefaultHttpContext { User = principal }, CancellationToken.None);

        var auth = (Dictionary<string, object?>)data["auth"]!;
        var user = (Dictionary<string, object?>)auth["user"]!;

        Assert.Multiple(() =>
        {
            Assert.That(user["name"], Is.EqualTo("Ada"));
            Assert.That(user["role"], Is.EqualTo("admin"));
        });
    }

    [Test]
    [Description("SC: the claims provider is a no-op when disabled.")]
    public async Task ClaimsProvider_DisabledIsNoOp()
    {
        var options = new InertiaOptions();
        options.ClaimsSharedData.Enabled = false;
        options.ClaimsSharedData.ClaimMapping["name"] = ClaimTypes.Name;

        var provider = new ClaimsSharedDataProvider(new StaticOptions(options));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "Ada"),
        }, "Test"));

        var data = await provider.GetSharedDataAsync(
            new DefaultHttpContext { User = principal }, CancellationToken.None);

        Assert.That(data, Is.Empty);
    }

    [Test]
    [Description("SC: the claims provider contributes nothing for an unauthenticated user.")]
    public async Task ClaimsProvider_AnonymousIsNoOp()
    {
        var options = new InertiaOptions();
        options.ClaimsSharedData.Enabled = true;
        options.ClaimsSharedData.ClaimMapping["name"] = ClaimTypes.Name;

        var provider = new ClaimsSharedDataProvider(new StaticOptions(options));

        var data = await provider.GetSharedDataAsync(
            new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }, CancellationToken.None);

        Assert.That(data, Is.Empty);
    }

    private sealed class StaticOptions : IOptions<InertiaOptions>
    {
        public StaticOptions(InertiaOptions value) => Value = value;
        public InertiaOptions Value { get; }
    }
}
