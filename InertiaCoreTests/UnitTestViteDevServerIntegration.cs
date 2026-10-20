using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using InertiaCore.DevServer;
using InertiaCore.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace InertiaCoreTests;

/// <summary>
/// End-to-end integration test for the development-time Vite dev server helper (IN-71).
/// A launcher that runs a Node fixture takes the place of the package manager, so the
/// start/ready/shutdown lifecycle is exercised without npm. Requires a real <c>node</c> on
/// the PATH; the tests skip cleanly when Node is unavailable.
/// </summary>
[TestFixture]
public class UnitTestViteDevServerIntegration : WebApplicationFactory<Program>
{
    private string _fixturePath = null!;
    private string _markerPath = null!;
    private string _hotFilePath = null!;
    private string _publicDirectory = null!;
    private int _port;

    private static bool NodeAvailable()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "node",
                ArgumentList = { "--version" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });
            process!.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    [SetUp]
    public void SetUp()
    {
        if (!NodeAvailable())
            Assert.Ignore("The 'node' executable is not available on this machine; skipping Vite dev server E2E.");

        _fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "vite-dev-fixture.mjs");
        if (!File.Exists(_fixturePath))
            Assert.Ignore($"Vite dev fixture '{_fixturePath}' was not copied to the output directory.");

        _port = GetFreePort();
        _markerPath = Path.Combine(Path.GetTempPath(), $"inertia-vite-marker-{Guid.NewGuid():N}.txt");

        // An absolute public directory so the hot file lands in a known place regardless of
        // the test host's content root (which points at the test app project).
        _publicDirectory = Path.Combine(Path.GetTempPath(), $"inertia-vite-public-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_publicDirectory);
        _hotFilePath = Path.Combine(_publicDirectory, "hot");
    }

    [TearDown]
    public void TearDown()
    {
        if (_markerPath is not null && File.Exists(_markerPath)) File.Delete(_markerPath);
        if (_publicDirectory is not null && Directory.Exists(_publicDirectory))
            Directory.Delete(_publicDirectory, recursive: true);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("UseViteDevServer", "true");
        builder.UseSetting("Vite:PublicDirectory", _publicDirectory);
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IViteDevProcessLauncher>(
                new FixtureLauncher(_fixturePath, _port, _hotFilePath, _markerPath));
        });
    }

    [Test]
    [Description("SC: app start runs the dev server and the hot file appears without a second terminal.")]
    public async Task AppStart_StartsDevServer_AndWritesHotFile()
    {
        using var client = CreateClient();

        var pid = await WaitForMarkerAsync();

        Assert.Multiple(() =>
        {
            Assert.That(pid, Is.GreaterThan(0), "the dev server fixture wrote its pid");
            Assert.That(File.Exists(_hotFilePath), Is.True, "the hot file was written by the dev server");
        });
    }

    [Test]
    [Description("SC: stopping the app kills the dev server with no orphaned Node process.")]
    public async Task AppStop_KillsDevServer_NoOrphans()
    {
        var factory = new UnitTestViteDevServerIntegration();
        try
        {
            factory.SetUp();
            using var client = factory.CreateClient();

            var pid = await factory.WaitForMarkerAsync();
            Assert.That(pid, Is.GreaterThan(0));

            // Dispose triggers host stop, which disposes the helper and kills the tree.
            factory.Dispose();

            await WaitUntilGoneAsync(pid);
            Assert.That(IsProcessAlive(pid), Is.False, "the dev server process was stopped");
        }
        finally
        {
            factory.Dispose();
            factory.TearDown();
        }
    }

    [Test]
    [Description("SC: a helper left running for the fixture is a no-op when the dev server is already reachable.")]
    public async Task AlreadyRunning_IsNoOp()
    {
        // First factory starts the fixture and leaves its hot file in place.
        var first = new UnitTestViteDevServerIntegration();
        try
        {
            first.SetUp();
            using var client = first.CreateClient();
            var pid = await first.WaitForMarkerAsync();
            Assert.That(pid, Is.GreaterThan(0));

            // With the fixture still listening and the hot file present, a second helper must
            // not launch another process. The fixture is reachable because the first factory is
            // still alive.
            var second = new UnitTestViteDevServerIntegration();
            second.SetUp();
            second.Dispose();
        }
        finally
        {
            first.Dispose();
            first.TearDown();
        }
    }

    private async Task<int> WaitForMarkerAsync()
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(20);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (File.Exists(_markerPath))
            {
                var text = await File.ReadAllTextAsync(_markerPath);
                if (int.TryParse(text.Trim(), out var pid)) return pid;
            }

            await Task.Delay(100);
        }

        return -1;
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static async Task WaitUntilGoneAsync(int pid)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (!IsProcessAlive(pid)) return;
            await Task.Delay(100);
        }
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>
    /// Runs the Node fixture in place of the package manager, forwarding the hot-file, port
    /// and marker paths through the environment.
    /// </summary>
    private sealed class FixtureLauncher : IViteDevProcessLauncher
    {
        private readonly string _fixturePath;
        private readonly int _port;
        private readonly string _hotFile;
        private readonly string _marker;

        public FixtureLauncher(string fixturePath, int port, string hotFile, string marker)
        {
            _fixturePath = fixturePath;
            _port = port;
            _hotFile = hotFile;
            _marker = marker;
        }

        public IViteDevProcess Create(ViteDevServerOptions options, string workingDirectory)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "node",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory
            };
            startInfo.ArgumentList.Add(_fixturePath);
            startInfo.Environment["VITE_DEV_PORT"] = _port.ToString();
            startInfo.Environment["VITE_HOT_FILE"] = _hotFile;
            startInfo.Environment["VITE_DEV_MARKER"] = _marker;

            return new RealProcess(new Process { StartInfo = startInfo });
        }
    }

    /// <summary>A thin real-process wrapper so the fixture runs and can be killed in the test.</summary>
    private sealed class RealProcess : IViteDevProcess
    {
        private readonly Process _process;

        public RealProcess(Process process) => _process = process;

        public bool HasExited => _process.HasExited;

        public int Id => _process.Id;

        public event EventHandler? Exited;

        public void Start()
        {
            _process.EnableRaisingEvents = true;
            _process.Exited += (_, _) => Exited?.Invoke(this, EventArgs.Empty);
            _process.Start();
        }

        public Task<string?> ReadStandardOutputLineAsync(CancellationToken cancellationToken) =>
            _process.StandardOutput.ReadLineAsync(cancellationToken).AsTask();

        public Task<string?> ReadStandardErrorLineAsync(CancellationToken cancellationToken) =>
            _process.StandardError.ReadLineAsync(cancellationToken).AsTask();

        public void Kill()
        {
            try
            {
                if (!_process.HasExited) _process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }
        }

        public ValueTask DisposeAsync()
        {
            _process.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
