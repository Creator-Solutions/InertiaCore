using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using InertiaCore.Logging;
using InertiaCore.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Timeout;

namespace InertiaCore.Ssr;

internal interface IGateway
{
    public Task<SsrResponse?> Dispatch(object model, string url);
}

internal partial class Gateway : IGateway{
    private readonly InertiaSsrClient _client;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly InertiaOptions _inertiaOptions;
    private readonly ILogger<Gateway>? _logger;

    public Gateway(
        InertiaSsrClient client,
        IOptions<JsonOptions>? jsonOptions,
        IOptions<InertiaOptions>? inertiaOptions,
        ILogger<Gateway>? logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _inertiaOptions = inertiaOptions?.Value ?? new InertiaOptions();
        _logger = logger;

        var hostOptions = jsonOptions?.Value?.JsonSerializerOptions;
        var userOptions = inertiaOptions?.Value?.JsonSerializerOptions ?? new JsonSerializerOptions();

        var effective = new JsonSerializerOptions(hostOptions ?? new JsonSerializerOptions());
        effective.PropertyNamingPolicy = userOptions.PropertyNamingPolicy
            ?? effective.PropertyNamingPolicy
            ?? JsonNamingPolicy.CamelCase;
        effective.ReferenceHandler = userOptions.ReferenceHandler
            ?? effective.ReferenceHandler
            ?? ReferenceHandler.IgnoreCycles;

        foreach (var converter in userOptions.Converters)
        {
            if (!effective.Converters.Contains(converter))
                effective.Converters.Add(converter);
        }

        var resolvers = new List<IJsonTypeInfoResolver>();
        if (userOptions.TypeInfoResolver is not null)
            resolvers.Add(userOptions.TypeInfoResolver);
        if (effective.TypeInfoResolver is not null && !ReferenceEquals(effective.TypeInfoResolver, userOptions.TypeInfoResolver))
            resolvers.Add(effective.TypeInfoResolver);
        resolvers.Add(new DefaultJsonTypeInfoResolver());
        effective.TypeInfoResolver = JsonTypeInfoResolver.Combine(resolvers.ToArray());

        _jsonOptions = effective;
    }

    public async Task<SsrResponse?> Dispatch(object model, string url)
    {
        if (!TryGetValidUri(url, out var uri))
        {
            SsrInvalidUrl(url);
            return null;
        }

        var json = JsonSerializer.Serialize(model, _jsonOptions);

        using var timeout = new CancellationTokenSource(_inertiaOptions.SsrTimeout);

        try
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _client.HttpClient.PostAsync(uri, content, timeout.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                SsrRequestFailedStatus(uri, (int)response.StatusCode);
                return null;
            }

            var bytes = await ReadBoundedAsync(response.Content, _inertiaOptions.SsrMaxResponseBytes, timeout.Token)
                .ConfigureAwait(false);

            if (bytes is null)
            {
                SsrResponseTooLarge(uri, _inertiaOptions.SsrMaxResponseBytes);
                return null;
            }

            var parsed = JsonSerializer.Deserialize<SsrResponse>(bytes, _jsonOptions);
            if (parsed?.Body is null)
            {
                SsrNoBody(uri);
                return null;
            }

            return parsed;
        }
        catch (BrokenCircuitException)
        {
            SsrCircuitOpen(uri);
            return null;
        }
        catch (TimeoutRejectedException)
        {
            SsrTimedOut(uri, _inertiaOptions.SsrTimeout);
            return null;
        }
        catch (RateLimiterRejectedException)
        {
            SsrRateLimited(uri);
            return null;
        }
        catch (OperationCanceledException)
        {
            SsrTimedOut(uri, _inertiaOptions.SsrTimeout);
            return null;
        }
        catch (HttpRequestException ex)
        {
            SsrHttpFailed(uri, ex);
            return null;
        }
        catch (JsonException ex)
        {
            SsrInvalidJson(uri, ex);
            return null;
        }
    }

    [LoggerMessage(EventId = InertiaLogEvents.SsrInvalidUrl, Level = LogLevel.Warning, Message = "Inertia SSR URL '{Url}' is not a valid absolute http/https URL; falling back to client-side rendering.")]
    private partial void SsrInvalidUrl(string url);

    [LoggerMessage(EventId = InertiaLogEvents.SsrRequestFailedStatus, Level = LogLevel.Warning, Message = "Inertia SSR request to {Url} failed with status {Status}; falling back to client-side rendering.")]
    private partial void SsrRequestFailedStatus(Uri url, int status);

    [LoggerMessage(EventId = InertiaLogEvents.SsrResponseTooLarge, Level = LogLevel.Warning, Message = "Inertia SSR response from {Url} exceeded the {Limit} byte limit; falling back to client-side rendering.")]
    private partial void SsrResponseTooLarge(Uri url, long limit);

    [LoggerMessage(EventId = InertiaLogEvents.SsrNoBody, Level = LogLevel.Warning, Message = "Inertia SSR response from {Url} had no body; falling back to client-side rendering.")]
    private partial void SsrNoBody(Uri url);

    [LoggerMessage(EventId = InertiaLogEvents.SsrCircuitOpen, Level = LogLevel.Warning, Message = "Inertia SSR circuit breaker for {Url} is open; skipping the request and rendering client-side.")]
    private partial void SsrCircuitOpen(Uri url);

    [LoggerMessage(EventId = InertiaLogEvents.SsrTimedOut, Level = LogLevel.Warning, Message = "Inertia SSR request to {Url} timed out after {Timeout}; falling back to client-side rendering.")]
    private partial void SsrTimedOut(Uri url, TimeSpan timeout);

    [LoggerMessage(EventId = InertiaLogEvents.SsrRateLimited, Level = LogLevel.Warning, Message = "Inertia SSR request to {Url} was rejected by the client rate limiter; falling back to client-side rendering.")]
    private partial void SsrRateLimited(Uri url);

    [LoggerMessage(EventId = InertiaLogEvents.SsrHttpFailed, Level = LogLevel.Warning, Message = "Inertia SSR request to {Url} failed; falling back to client-side rendering.")]
    private partial void SsrHttpFailed(Uri url, Exception exception);

    [LoggerMessage(EventId = InertiaLogEvents.SsrInvalidJson, Level = LogLevel.Warning, Message = "Inertia SSR response from {Url} was not valid JSON; falling back to client-side rendering.")]
    private partial void SsrInvalidJson(Uri url, Exception exception);

    private static bool TryGetValidUri(string url, out Uri uri)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
        {
            uri = parsed;
            return true;
        }

        uri = null!;
        return false;
    }

    private static async Task<byte[]?> ReadBoundedAsync(HttpContent content, long maxBytes, CancellationToken cancellationToken)
    {
        var limit = maxBytes > 0 ? maxBytes : long.MaxValue;

        var declaredLength = content.Headers.ContentLength;
        if (declaredLength is > 0 && declaredLength > limit)
            return null;

        await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];

        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > limit)
                return null;

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
