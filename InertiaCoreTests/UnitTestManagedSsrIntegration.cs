using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace InertiaCoreTests;

/// <summary>
/// End-to-end integration test for the managed Node SSR process (IN-68). Requires a real
/// <c>node</c> on the PATH; the tests skip cleanly when Node is unavailable so suites running
/// without a JavaScript toolchain stay green.
/// </summary>
[TestFixture]
public class UnitTestManagedSsrIntegration : WebApplicationFactory<Program>
{
    private string _fixturePath = null!;
    private string _markerPath = null!;
    private int _port;

    private static bool NodeAvailable(out string nodePath)
    {
        nodePath = "node";
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = nodePath,
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
        if (!NodeAvailable(out _))
            Assert.Ignore("The 'node' executable is not available on this machine; skipping managed SSR E2E.");

        _fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ssr-fixture.mjs");
        if (!File.Exists(_fixturePath))
            Assert.Ignore($"SSR fixture '{_fixturePath}' was not copied to the output directory.");

        _port = GetFreePort();
        _markerPath = Path.Combine(Path.GetTempPath(), $"inertia-ssr-marker-{Guid.NewGuid():N}.txt");
    }

    [TearDown]
    public void TearDown()
    {
        if (_markerPath is not null && File.Exists(_markerPath)) File.Delete(_markerPath);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("UseManagedSsr", "true");
        builder.UseSetting("ManagedSsr:NodePath", "node");
        builder.UseSetting("ManagedSsr:ScriptPath", _fixturePath ?? "");
        builder.UseSetting("ManagedSsr:Port", _port.ToString());
        builder.UseSetting("ManagedSsr:Marker", _markerPath ?? "");
    }

    private static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Test]
    [Description("SC: app start launches the SSR process and requests render server-side.")]
    public async Task AppStart_LaunchesSsr_AndRendersServerSide()
    {
        using var client = CreateClient();

        var pid = await WaitForMarkerAsync();

        var response = await client.GetAsync("/ssr/html");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(pid, Is.GreaterThan(0), "the SSR fixture wrote its pid");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(html, Does.Contain("<div>fixture</div>"), "the response was rendered by the SSR process");
        });
    }

    [Test]
    [Description("SC: stopping the app shuts the SSR process down with no orphaned Node process.")]
    public async Task AppStop_ShutsDownSsr_NoOrphans()
    {
        var factory = new UnitTestManagedSsrIntegration();
        try
        {
            factory.SetUp();
            using var client = factory.CreateClient();

            var pid = await factory.WaitForMarkerAsync();
            Assert.That(pid, Is.GreaterThan(0));

            using var probe = factory.CreateClient();
            await probe.GetAsync("/ssr/html");

            // Dispose triggers the host StopAsync, which should shut the process down gracefully.
            factory.Dispose();

            await WaitUntilGoneAsync(pid);
            Assert.That(IsProcessAlive(pid), Is.False, "the Node SSR process was stopped");
        }
        finally
        {
            factory.Dispose();
            factory.TearDown();
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
}
