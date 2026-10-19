using InertiaCore.Extensions;
using InertiaCore.Services;
using InertiaCore.Utils;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InertiaCoreTests;

/// <summary>
/// Tests for the opt-in distributed-cache flash provider used for large payloads.
/// </summary>
[TestFixture]
public class UnitTestDistributedCacheFlashProvider
{
    private const string CookieName = "InertiaFlashCacheId";

    private readonly MemoryDistributedCache _cache =
        new(Options.Create(new MemoryDistributedCacheOptions()));

    private readonly IDataProtectionProvider _protection =
        DataProtectionProvider.Create("InertiaCoreTests");

    private (DistributedCacheFlashProvider Provider, DefaultHttpContext Context) CreateContext(string? cookie = null)
    {
        var context = new DefaultHttpContext();
        if (cookie is not null)
            context.Request.Headers.Cookie = cookie;

        var accessor = new HttpContextAccessor { HttpContext = context };
        return (new DistributedCacheFlashProvider(accessor, _cache, _protection), context);
    }

    /// <summary>Returns the "Name=Value" cookie pair the provider wrote, if any.</summary>
    private static string? ReadCookie(DefaultHttpContext context)
    {
        var header = context.Response.Headers.SetCookie.ToString();
        if (string.IsNullOrEmpty(header))
            return null;

        foreach (var segment in header.Split(';'))
        {
            var trimmed = segment.Trim();
            if (trimmed.StartsWith(CookieName + "=", StringComparison.Ordinal))
                return trimmed;
        }

        return null;
    }

    [Test]
    [Description("Flash written in one request is read back on the next and then consumed.")]
    public void Flash_ThenPull_RoundTripsAndConsumes()
    {
        var (writer, writerContext) = CreateContext();
        writer.Flash("success", "Saved");
        var cookie = ReadCookie(writerContext);

        var (reader, _) = CreateContext(cookie);

        Assert.Multiple(() =>
        {
            Assert.That(cookie, Is.Not.Null);
            Assert.That(reader.Pull()["success"]!.ToString(), Is.EqualTo("Saved"));
            Assert.That(reader.Pull(), Is.Empty, "the entry is consumed after the first pull");
        });
    }

    [Test]
    [Description("Prefetch requests do not consume the cached flash.")]
    public void Pull_OnPrefetch_DoesNotConsume()
    {
        var (writer, writerContext) = CreateContext();
        writer.Flash("success", "Saved");
        var cookie = ReadCookie(writerContext);

        var (prefetch, prefetchContext) = CreateContext(cookie);
        prefetchContext.Request.Headers[InertiaHeader.Purpose] = "prefetch";
        Assert.That(prefetch.Pull(), Is.Empty);

        var (reader, _) = CreateContext(cookie);
        Assert.That(reader.Pull()["success"]!.ToString(), Is.EqualTo("Saved"));
    }

    [Test]
    [Description("Clear discards the cached entry so it is not shown.")]
    public void Clear_RemovesCachedFlash()
    {
        var (writer, writerContext) = CreateContext();
        writer.Flash("success", "Saved");
        var cookie = ReadCookie(writerContext);

        var (clearer, _) = CreateContext(cookie);
        clearer.Clear();

        var (reader, _) = CreateContext(cookie);
        Assert.That(reader.Pull(), Is.Empty);
    }

    [Test]
    [Description("With no active request the provider is a no-op.")]
    public void NoContext_IsNoOp()
    {
        var provider = new DistributedCacheFlashProvider(new HttpContextAccessor(), _cache, _protection);

        Assert.Multiple(() =>
        {
            Assert.DoesNotThrow(() => provider.Flash("k", "v"));
            Assert.That(provider.Pull(), Is.Empty);
            Assert.DoesNotThrow(() => provider.Reflash());
            Assert.DoesNotThrow(() => provider.Clear());
        });
    }

    [Test]
    [Description("Registering the distributed store without an IDistributedCache throws a clear error.")]
    public void AddInertiaDistributedCacheFlash_WithoutCache_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() => services.AddInertiaDistributedCacheFlash());
    }

    [Test]
    [Description("The distributed store replaces the default regardless of registration order.")]
    public void AddInertiaDistributedCacheFlash_ReplacesDefault()
    {
        var after = new ServiceCollection();
        after.AddInertia();
        after.AddDistributedMemoryCache();
        after.AddInertiaDistributedCacheFlash();

        var before = new ServiceCollection();
        before.AddDistributedMemoryCache();
        before.AddInertiaDistributedCacheFlash();
        before.AddInertia();

        Assert.Multiple(() =>
        {
            Assert.That(after.BuildServiceProvider().GetRequiredService<IInertiaFlashProvider>(),
                Is.InstanceOf<DistributedCacheFlashProvider>());
            Assert.That(before.BuildServiceProvider().GetRequiredService<IInertiaFlashProvider>(),
                Is.InstanceOf<DistributedCacheFlashProvider>());
        });
    }
}
