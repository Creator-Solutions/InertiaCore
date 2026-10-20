using InertiaCore.Extensions;
using InertiaCore.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InertiaCoreTests;

/// <summary>
/// Tests for the configuration-aware <c>AddInertia</c> and <c>AddViteHelper</c> overloads used
/// by orchestrators such as .NET Aspire to supply values through environment variables.
/// </summary>
public partial class Tests
{
    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Test]
    [Description("SC: AddInertia binds InertiaOptions from the Inertia configuration section.")]
    public void TestAddInertiaBindsFromConfiguration()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["Inertia:SsrEnabled"] = "true",
            ["Inertia:SsrUrl"] = "http://ssr:8080/render",
        });

        var services = new ServiceCollection();
        services.AddInertia(configuration);

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<InertiaOptions>>().Value;

        Assert.Multiple(() =>
        {
            Assert.That(options.SsrEnabled, Is.True);
            Assert.That(options.SsrUrl, Is.EqualTo("http://ssr:8080/render"));
        });
    }

    [Test]
    [Description("SC: the AddInertia delegate is applied after configuration binding.")]
    public void TestAddInertiaDelegateRunsAfterConfiguration()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["Inertia:SsrUrl"] = "http://ssr:8080/render",
        });

        var services = new ServiceCollection();
        services.AddInertia(configuration, options => options.SsrUrl = "http://override/render");

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<InertiaOptions>>().Value;

        Assert.That(options.SsrUrl, Is.EqualTo("http://override/render"));
    }

    [Test]
    [Description("SC: AddViteHelper binds ViteOptions from the Vite configuration section.")]
    public void TestAddViteHelperBindsFromConfiguration()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["Vite:HotUrl"] = "http://127.0.0.1:3000",
        });

        var services = new ServiceCollection();
        services.AddViteHelper(configuration);

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ViteOptions>>().Value;

        Assert.That(options.HotUrl, Is.EqualTo("http://127.0.0.1:3000"));
    }

    [Test]
    [Description("SC: an empty configuration section leaves the option defaults unchanged.")]
    public void TestEmptyConfigurationKeepsDefaults()
    {
        var services = new ServiceCollection();
        services.AddInertia(Configuration(new Dictionary<string, string?>()));

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<InertiaOptions>>().Value;

        Assert.Multiple(() =>
        {
            Assert.That(options.SsrEnabled, Is.False);
            Assert.That(options.SsrUrl, Is.EqualTo("http://127.0.0.1:13714/render"));
        });
    }
}
