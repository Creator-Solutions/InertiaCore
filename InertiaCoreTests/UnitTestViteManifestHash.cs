using System.IO.Abstractions.TestingHelpers;
using InertiaCore.Models;
using InertiaCore.Utils;
using Microsoft.Extensions.Options;
using Moq;

namespace InertiaCoreTests;

/// <summary>
/// SC-10: the manifest hash used for asset versioning must be served from the
/// manifest cache rather than re-reading and hashing the file on every call.
/// </summary>
[TestFixture]
public class UnitTestViteManifestHash
{
    private const string ManifestPath = @"/wwwroot/build/manifest.json";

    private static (ViteBuilder Builder, MockFileSystem FileSystem) CreateBuilder()
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { ManifestPath, new MockFileData("{\"app.tsx\": {\"file\": \"assets/main-aaaa.js\"}}") }
        });

        var options = new Mock<IOptions<ViteOptions>>();
        options.SetupGet(x => x.Value).Returns(new ViteOptions());

        var builder = new Mock<ViteBuilder>(options.Object);
        builder.Object.UseFileSystem(fileSystem);
        return (builder.Object, fileSystem);
    }

    [Test]
    [Description("SC-10: repeated calls are served from the cache while the manifest is unchanged.")]
    public void GetManifestHash_IsCached()
    {
        var (builder, fileSystem) = CreateBuilder();

        var first = builder.GetManifestHash();

        // Change the manifest on disk; within the cache window the hash must not change.
        fileSystem.AddFile(ManifestPath, new MockFileData("{\"app.tsx\": {\"file\": \"assets/main-bbbb.js\"}}"));

        var second = builder.GetManifestHash();

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.EqualTo(first), "hash should be served from the cache");
        });
    }

    [Test]
    [Description("SC-10: clearing the cache picks up a changed manifest.")]
    public void GetManifestHash_CacheClear_ReflectsNewManifest()
    {
        var (builder, fileSystem) = CreateBuilder();

        var first = builder.GetManifestHash();

        fileSystem.AddFile(ManifestPath, new MockFileData("{\"app.tsx\": {\"file\": \"assets/main-bbbb.js\"}}"));
        builder.ClearManifestCache();

        var second = builder.GetManifestHash();

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Not.Null);
            Assert.That(second, Is.Not.EqualTo(first), "cleared cache should read the new manifest");
        });
    }

    [Test]
    [Description("SC-10: a missing manifest yields a null hash.")]
    public void GetManifestHash_MissingManifest_ReturnsNull()
    {
        var (builder, fileSystem) = CreateBuilder();
        fileSystem.File.Delete(ManifestPath);

        Assert.That(builder.GetManifestHash(), Is.Null);
    }
}
