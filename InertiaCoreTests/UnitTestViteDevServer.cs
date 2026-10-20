using System.Net;
using System.Net.Http;
using System.IO.Abstractions.TestingHelpers;
using System.Threading;
using System.Threading.Tasks;
using InertiaCore.DevServer;
using InertiaCore.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InertiaCoreTests;

/// <summary>
/// Unit tests for <see cref="ViteDevServer"/> using a fake launcher, an in-memory file
/// system and a stub HTTP handler, so the start/ready/no-op/timeout lifecycle is exercised
/// without Node or a real dev server.
/// </summary>
[TestFixture]
public class UnitTestViteDevServer
{
    private const string ContentRoot = "/app";
    private const string HotPath = "/app/wwwroot/hot";

    private static ViteDevServer Create(
        FakeLauncher launcher,
        MockFileSystem fileSystem,
        IHttpClientFactory httpClientFactory,
        ViteDevServerOptions? options = null,
        ViteOptions? viteOptions = null) =>
        new(
            launcher,
            fileSystem,
            httpClientFactory,
            Options.Create(options ?? new ViteDevServerOptions { ReadyTimeout = TimeSpan.FromSeconds(2) }),
            Options.Create(viteOptions ?? new ViteOptions()),
            NullLogger<ViteDevServer>.Instance,
            ContentRoot);

    [Test]
    [Description("SC: starts the process and reports ready when the hot file appears.")]
    public async Task Starts_AndWaitsForHotFile()
    {
        var launcher = new FakeLauncher();
        var fileSystem = new MockFileSystem();

        var server = Create(launcher, fileSystem, new OkFactory());

        launcher.OnStart = () => fileSystem.AddFile(HotPath, new MockFileData("http://localhost:5173"));

        var ready = await server.StartAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(ready, Is.True);
            Assert.That(launcher.Created, Has.Count.EqualTo(1));
        });

        await server.DisposeAsync();
    }

    [Test]
    [Description("SC: is a no-op when the dev server is already running (hot file present and reachable).")]
    public async Task AlreadyRunning_DoesNotStart()
    {
        var launcher = new FakeLauncher();
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile(HotPath, new MockFileData("http://localhost:5173"));

        var server = Create(launcher, fileSystem, new OkFactory());

        var ready = await server.StartAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(ready, Is.True);
            Assert.That(launcher.Created, Is.Empty, "no new process is launched when Vite is already up");
        });

        await server.DisposeAsync();
    }

    [Test]
    [Description("SC: a stale hot file whose URL is unreachable does not stop the helper from starting its own process.")]
    public async Task StaleHotFile_StartsProcess()
    {
        var launcher = new FakeLauncher();
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile(HotPath, new MockFileData("http://localhost:5173"));

        var server = Create(launcher, fileSystem, new UnreachableFactory());

        launcher.OnStart = () => fileSystem.RemoveFile(HotPath);

        var ready = await server.StartAsync(CancellationToken.None);

        Assert.That(launcher.Created, Has.Count.EqualTo(1));

        await server.DisposeAsync();
    }

    [Test]
    [Description("SC: the process is stopped when the hot file never appears within the timeout.")]
    public async Task Timeout_StopsProcess()
    {
        var launcher = new FakeLauncher();
        var fileSystem = new MockFileSystem();

        var server = Create(
            launcher,
            fileSystem,
            new OkFactory(),
            options: new ViteDevServerOptions { ReadyTimeout = TimeSpan.FromMilliseconds(200) });

        var ready = await server.StartAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(ready, Is.False);
            Assert.That(launcher.Created, Has.Count.EqualTo(1));
            Assert.That(launcher.Created[0].KillCount, Is.EqualTo(1), "the process tree is killed on timeout");
        });

        await server.DisposeAsync();
    }

    [Test]
    [Description("SC: disposing the helper kills the process tree so no dev server is left behind.")]
    public async Task Dispose_KillsProcess()
    {
        var launcher = new FakeLauncher();
        var fileSystem = new MockFileSystem();

        var server = Create(launcher, fileSystem, new OkFactory());
        launcher.OnStart = () => fileSystem.AddFile(HotPath, new MockFileData("http://localhost:5173"));

        await server.StartAsync(CancellationToken.None);
        await server.DisposeAsync();

        Assert.That(launcher.Created[0].KillCount, Is.EqualTo(1));
    }

    [Test]
    [Description("The configured script name and package manager are passed to the launcher.")]
    public async Task PassesOptions_ToLauncher()
    {
        var launcher = new FakeLauncher();
        var fileSystem = new MockFileSystem();

        var server = Create(
            launcher,
            fileSystem,
            new OkFactory(),
            options: new ViteDevServerOptions
            {
                PackageManager = VitePackageManager.Pnpm,
                ScriptName = "dev:hot",
                ReadyTimeout = TimeSpan.FromMilliseconds(200)
            });

        launcher.OnStart = () => fileSystem.AddFile(HotPath, new MockFileData("http://localhost:5173"));

        await server.StartAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(launcher.LastOptions!.PackageManager, Is.EqualTo(VitePackageManager.Pnpm));
            Assert.That(launcher.LastOptions.ScriptName, Is.EqualTo("dev:hot"));
        });

        await server.DisposeAsync();
    }

    [Test]
    [Description("The working directory is resolved to the nearest ancestor containing package.json.")]
    public async Task WorkingDirectory_WalksUpToPackageJson()
    {
        var launcher = new FakeLauncher();
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile("/app/package.json", new MockFileData("{}"));

        var server = Create(launcher, fileSystem, new OkFactory());
        launcher.OnStart = () => fileSystem.AddFile(HotPath, new MockFileData("http://localhost:5173"));

        await server.StartAsync(CancellationToken.None);

        Assert.That(launcher.LastWorkingDirectory, Is.EqualTo("/app"));

        await server.DisposeAsync();
    }

    private sealed class FakeLauncher : IViteDevProcessLauncher
    {
        public List<FakeProcess> Created { get; } = new();

        public ViteDevServerOptions? LastOptions { get; private set; }

        public string? LastWorkingDirectory { get; private set; }

        public Action? OnStart { get; set; }

        public IViteDevProcess Create(ViteDevServerOptions options, string workingDirectory)
        {
            LastOptions = options;
            LastWorkingDirectory = workingDirectory;
            var process = new FakeProcess(() => OnStart?.Invoke());
            Created.Add(process);
            return process;
        }
    }

    private sealed class FakeProcess : IViteDevProcess
    {
        private readonly Action _onStart;

        public FakeProcess(Action onStart) => _onStart = onStart;

        public bool HasExited { get; private set; }

        public int Id => 4242;

        public int KillCount { get; private set; }

        public event EventHandler? Exited;

        public void Start() => _onStart();

        public Task<string?> ReadStandardOutputLineAsync(CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);

        public Task<string?> ReadStandardErrorLineAsync(CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);

        public void Kill()
        {
            KillCount++;
            HasExited = true;
            Exited?.Invoke(this, EventArgs.Empty);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class OkFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(new StubHandler(HttpStatusCode.OK));
    }

    private sealed class UnreachableFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(new StubHandler(HttpStatusCode.ServiceUnavailable));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;

        public StubHandler(HttpStatusCode status) => _status = status;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(_status));
    }
}
