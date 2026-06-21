using InertiaCore;
using InertiaCore.Extensions;
using InertiaCore.Ssr;
using InertiaCore.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InertiaCoreTests;

public partial class Tests
{
    [Test]
    [Description("Test if the configuration registers properly necessary services and filters.")]
    public void TestConfiguration()
    {
        var builder = WebApplication.CreateBuilder();

        Assert.DoesNotThrow(() => builder.Services.AddInertia());

        Assert.Multiple(() =>
        {
            Assert.That(builder.Services.Any(s => s.ServiceType == typeof(IHttpContextAccessor)), Is.True);
            Assert.That(builder.Services.Any(s => s.ServiceType == typeof(IHttpClientFactory)), Is.True);

            Assert.That(builder.Services.Any(s => s.ServiceType == typeof(IResponseFactory)), Is.True);
            Assert.That(builder.Services.Any(s => s.ServiceType == typeof(IGateway)), Is.True);
        });

        var mvcConfiguration =
            builder.Services.FirstOrDefault(s => s.ServiceType == typeof(IConfigureOptions<MvcOptions>));

        var mvcOptions = new MvcOptions();
        (mvcConfiguration?.ImplementationInstance as ConfigureNamedOptions<MvcOptions>)?.Action?.Invoke(mvcOptions);

        Assert.That(
            mvcOptions.Filters.Any(f => (f as TypeFilterAttribute)?.ImplementationType == typeof(InertiaActionFilter)),
            Is.True);

        var app = builder.Build();
        Assert.DoesNotThrow(() => app.UseInertia());

        // Verify IResponseFactory resolves from DI and GetVersion() works after pipeline is configured.
        var factory = app.Services.GetRequiredService<IResponseFactory>();
        Assert.DoesNotThrow(() => factory.GetVersion());
    }
}
