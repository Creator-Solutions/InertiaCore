using InertiaCore;
using InertiaCore.Contracts;
using InertiaCore.Models;
using InertiaCore.Services;
using InertiaCore.Utils;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Text;
using System.Text.Json;

namespace InertiaCoreTests;

[TestFixture]
public class UnitTestFlashProvider
{
    private static (InertiaFlashProvider Provider, DefaultHttpContext Context) Create()
    {
        var context = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = context };
        return (new InertiaFlashProvider(accessor), context);
    }

    [Test]
    [Description("Flash is stored and returned once, then cleared.")]
    public void Flash_ThenPull_ReturnsAndConsumes()
    {
        var (provider, _) = Create();

        provider.Flash("status", "ok");

        Assert.Multiple(() =>
        {
            Assert.That(provider.Pull()["status"], Is.EqualTo("ok"));
            Assert.That(provider.Pull(), Is.Empty, "flash is consumed after the first pull");
        });
    }

    [Test]
    [Description("Prefetch requests do not consume flash data.")]
    public void Pull_OnPrefetch_DoesNotConsume()
    {
        var (provider, context) = Create();
        provider.Flash("status", "ok");

        context.Request.Headers[InertiaHeader.Purpose] = "prefetch";
        Assert.That(provider.Pull(), Is.Empty, "a prefetch must not receive (or consume) flash");

        context.Request.Headers.Remove(InertiaHeader.Purpose);
        Assert.That(provider.Pull()["status"], Is.EqualTo("ok"), "the real visit still receives the flash");
    }

    [Test]
    [Description("Reflash does not throw and keeps flash available.")]
    public void Reflash_KeepsFlashAvailable()
    {
        var (provider, _) = Create();
        provider.Flash("status", "ok");

        Assert.DoesNotThrow(() => provider.Reflash());
        Assert.That(provider.Pull()["status"], Is.EqualTo("ok"));
    }

    [Test]
    [Description("With no active request the provider is a no-op.")]
    public void NoContext_IsNoOp()
    {
        var provider = new InertiaFlashProvider(new HttpContextAccessor());

        Assert.Multiple(() =>
        {
            Assert.DoesNotThrow(() => provider.Flash("k", "v"));
            Assert.That(provider.Pull(), Is.Empty);
            Assert.DoesNotThrow(() => provider.Reflash());
        });
    }

    [Test]
    [Description("Clear discards flash that has not been pulled yet.")]
    public void Clear_RemovesPendingFlash()
    {
        var (provider, _) = Create();
        provider.Flash("status", "ok");

        provider.Clear();

        Assert.That(provider.Pull(), Is.Empty);
    }

    [Test]
    [Description("A payload that exactly fills the cookie limit is stored.")]
    public void Flash_AtCookieLimit_IsAllowed()
    {
        var max = SerializedBagBytes();
        var (provider, _) = CreateWithCookieStore(max);

        Assert.DoesNotThrow(() => provider.Flash("k", "v"));
    }

    [Test]
    [Description("A payload above the cookie limit throws the documented exception.")]
    public void Flash_AboveCookieLimit_ThrowsDocumentedException()
    {
        var max = SerializedBagBytes() - 1;
        var (provider, _) = CreateWithCookieStore(max);

        var exception = Assert.Throws<InertiaFlashPayloadTooLargeException>(() => provider.Flash("k", "v"));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.ActualBytes, Is.EqualTo(max + 1));
            Assert.That(exception.MaxBytes, Is.EqualTo(max));
            Assert.That(exception.Message, Does.Contain("AddInertiaDistributedCacheFlash"));
        });
    }

    private static int SerializedBagBytes() =>
        Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(
            new Dictionary<string, object?> { ["k"] = "v" }));

    private static (InertiaFlashProvider Provider, DefaultHttpContext Context) CreateWithCookieStore(int maxBytes)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.Configure<InertiaFlashOptions>(options => options.MaxCookiePayloadBytes = maxBytes);

        var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        var accessor = new HttpContextAccessor { HttpContext = context };
        return (new InertiaFlashProvider(accessor), context);
    }

    [Test]
    [Description("IInertia.Flash routes through the registered provider.")]
    public void InertiaService_Flash_UsesProvider()
    {
        var provider = new Mock<IInertiaFlashProvider>();
        var service = new InertiaService(
            new Mock<IResponseFactory>().Object,
            new InertiaState(),
            Options.Create(new InertiaOptions()),
            provider.Object);

        service.Flash("StatusMessage", "Saved");

        provider.Verify(p => p.Flash("statusMessage", "Saved"), Times.Once);
    }
}
