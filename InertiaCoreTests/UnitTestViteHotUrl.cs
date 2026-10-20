using System.IO.Abstractions.TestingHelpers;
using InertiaCore.Models;
using InertiaCore.Utils;
using Microsoft.Extensions.Options;
using Moq;

namespace InertiaCoreTests;

/// <summary>
/// Tests for the orchestrator-supplied Vite dev server URL (<see cref="ViteOptions.HotUrl"/>),
/// used by the .NET Aspire hosting integration.
/// </summary>
public partial class Tests
{
    private static ViteBuilder CreateBuilder(ViteOptions options, MockFileSystem fileSystem)
    {
        var wrapped = new Mock<IOptions<ViteOptions>>();
        wrapped.SetupGet(x => x.Value).Returns(options);

        var builder = new Mock<ViteBuilder>(wrapped.Object);
        builder.Object.UseFileSystem(fileSystem);
        return builder.Object;
    }

    [Test]
    [Description("SC: the Vite helper is in hot mode when HotUrl is set, even without a hot file.")]
    public void TestHotUrlEnablesHotMode()
    {
        var options = new ViteOptions { HotUrl = "http://127.0.0.1:3000" };
        var builder = CreateBuilder(options, new MockFileSystem(new Dictionary<string, MockFileData>()));

        Assert.That(builder.IsRunningHot(), Is.True);
    }

    [Test]
    [Description("SC: assets resolve against HotUrl and its trailing slash is normalized.")]
    public async Task TestHotUrlResolvesAssets()
    {
        var options = new ViteOptions { HotUrl = "http://127.0.0.1:3000/" };
        var builder = CreateBuilder(options, new MockFileSystem(new Dictionary<string, MockFileData>()));

        var result = await builder.ReactRefreshAsync();

        Assert.That(result.ToString(), Does.Contain("http://127.0.0.1:3000/@react-refresh"));
    }

    [Test]
    [Description("SC: the hot file drives hot mode when HotUrl is unset.")]
    public void TestHotFileStillWorksWithoutHotUrl()
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { @"/wwwroot/hot", new MockFileData("http://127.0.0.1:5174") },
        });
        var builder = CreateBuilder(new ViteOptions(), fileSystem);

        Assert.That(builder.IsRunningHot(), Is.True);
    }

    [Test]
    [Description("SC: neither HotUrl nor a hot file leaves the helper in the non-hot manifest path.")]
    public void TestNoHotUrlAndNoFileIsNotHot()
    {
        var builder = CreateBuilder(new ViteOptions(), new MockFileSystem(new Dictionary<string, MockFileData>()));

        Assert.That(builder.IsRunningHot(), Is.False);
    }

    [Test]
    [Description("SC: the manifest hash is null while running through HotUrl.")]
    public void TestHotUrlSuppressesManifestHash()
    {
        var options = new ViteOptions { HotUrl = "http://127.0.0.1:3000" };
        var builder = CreateBuilder(options, new MockFileSystem(new Dictionary<string, MockFileData>()));

        Assert.That(builder.GetManifestHash(), Is.Null);
    }
}
