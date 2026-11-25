using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using InertiaCore.Logging;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InertiaCoreTests;

[TestFixture]
public class UnitTestLoggingEventIds
{
    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly ConcurrentBag<EventId> _ids;
        public CapturingLogger(ConcurrentBag<EventId> ids) => _ids = ids;

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => _ids.Add(eventId);
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentBag<EventId> _ids;
        public CapturingLoggerProvider(ConcurrentBag<EventId> ids) => _ids = ids;
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(_ids);
        public void Dispose() { }
    }

    [Test]
    public async Task PropResolutionFailure_LogsStableEventId()
    {
        var captured = new ConcurrentBag<EventId>();

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
                services.AddSingleton<ILoggerProvider>(new CapturingLoggerProvider(captured)));
        });

        using var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/rescue");
        request.Headers.Add("X-Inertia", "true");
        request.Headers.Add("X-Inertia-Partial-Data", "permissions,other");
        request.Headers.Add("X-Inertia-Partial-Component", "Test/Page");

        await client.SendAsync(request);

        Assert.That(captured.Any(e => e.Id == InertiaLogEvents.PropResolutionFailed), Is.True);
    }
}
