using System.Collections.Generic;
using InertiaCore.DevServer;
using InertiaCore.Models;

namespace InertiaCoreTests;

/// <summary>
/// Unit tests for <see cref="ViteDevProcessLauncher"/> that assert the command line built
/// for each package manager, including the Windows <c>.cmd</c> shim resolution, without
/// spawning a real process.
/// </summary>
[TestFixture]
public class UnitTestViteDevProcessLauncher
{
    private static ViteDevServerOptions Options(
        VitePackageManager packageManager = VitePackageManager.Npm,
        string script = "dev") =>
        new() { PackageManager = packageManager, ScriptName = script };

    [TestCase(VitePackageManager.Npm, "npm")]
    [TestCase(VitePackageManager.Pnpm, "pnpm")]
    [TestCase(VitePackageManager.Yarn, "yarn")]
    [TestCase(VitePackageManager.Bun, "bun")]
    [Description("Each package manager maps to its base executable name.")]
    public void ExecutableName_Maps(VitePackageManager packageManager, string expected)
    {
        Assert.That(ViteDevProcessLauncher.ExecutableName(packageManager), Is.EqualTo(expected));
    }

    [Test]
    [Description("The run arguments always spell out the run verb so every manager accepts them.")]
    public void RunArguments_IncludeRunVerb()
    {
        Assert.That(ViteDevProcessLauncher.RunArguments("dev"), Is.EqualTo(new[] { "run", "dev" }));
    }

    [Test]
    [Description("On Windows a bare package-manager name is resolved to its .cmd shim on the PATH.")]
    public void WindowsShim_ResolvesCmdOnPath()
    {
        var shim = Path.Combine(Path.GetTempPath(), "npm.cmd");
        var launcher = new FakePathLauncher(isWindows: true, searchPaths: new[] { Path.GetTempPath() })
        {
            ExistingFiles = { shim }
        };

        Assert.That(launcher.Resolve("npm"), Is.EqualTo(shim));
    }

    [Test]
    [Description("On Windows, when no shim exists the bare name is returned so cmd.exe resolves it.")]
    public void WindowsShim_FallsBackToBareName()
    {
        var launcher = new FakePathLauncher(isWindows: true, searchPaths: new[] { "/nonexistent" });

        Assert.That(launcher.Resolve("pnpm"), Is.EqualTo("pnpm"));
    }

    [Test]
    [Description("On Unix the package manager is executed directly with the run arguments.")]
    public void Unix_ExecutesDirectly()
    {
        var launcher = new FakePathLauncher(isWindows: false, searchPaths: Array.Empty<string>());

        var startInfo = launcher.BuildStartInfo(Options(VitePackageManager.Pnpm, "dev"), "/app/frontend");

        Assert.Multiple(() =>
        {
            Assert.That(startInfo.FileName, Is.EqualTo("pnpm"));
            Assert.That(startInfo.ArgumentList, Is.EqualTo(new[] { "run", "dev" }));
            Assert.That(startInfo.WorkingDirectory, Is.EqualTo("/app/frontend"));
            Assert.That(startInfo.Environment["BROWSER"], Is.EqualTo("none"));
        });
    }

    [Test]
    [Description("On Windows the package manager runs through cmd.exe with the resolved shim.")]
    public void Windows_RunsThroughCmd()
    {
        var tools = Path.Combine(Path.GetTempPath(), "tools");
        var shim = Path.Combine(tools, "npm.cmd");
        var launcher = new FakePathLauncher(isWindows: true, searchPaths: new[] { tools });
        launcher.ExistingFiles.Add(shim);

        var startInfo = launcher.BuildStartInfo(Options(VitePackageManager.Npm, "dev"), @"C:\app");

        Assert.Multiple(() =>
        {
            Assert.That(startInfo.FileName, Is.EqualTo("cmd.exe"));
            Assert.That(startInfo.ArgumentList, Is.EqualTo(new[] { "/c", shim, "run", "dev" }));
        });
    }

    [Test]
    [Description("A custom script name is passed through to the package manager.")]
    public void CustomScript_IsUsed()
    {
        var launcher = new FakePathLauncher(isWindows: false, searchPaths: Array.Empty<string>());

        var startInfo = launcher.BuildStartInfo(Options(VitePackageManager.Bun, "dev:hot"), "/app");

        Assert.That(startInfo.ArgumentList, Is.EqualTo(new[] { "run", "dev:hot" }));
    }

    /// <summary>
    /// Exposes the protected resolution seam of <see cref="ViteDevProcessLauncher"/> so the
    /// Windows shim lookup can be exercised on any platform.
    /// </summary>
    private sealed class FakePathLauncher : ViteDevProcessLauncher
    {
        private readonly bool _isWindows;
        private readonly IEnumerable<string> _searchPaths;

        public FakePathLauncher(bool isWindows, IEnumerable<string> searchPaths)
        {
            _isWindows = isWindows;
            _searchPaths = searchPaths;
        }

        public HashSet<string> ExistingFiles { get; } = new();

        protected internal override bool IsWindows => _isWindows;

        protected internal override IEnumerable<string> SearchPaths => _searchPaths;

        protected internal override bool FileExists(string path) => ExistingFiles.Contains(path);

        public string Resolve(string executable) => ResolveWindowsShim(executable);
    }
}
