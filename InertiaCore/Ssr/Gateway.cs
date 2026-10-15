using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using InertiaCore.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InertiaCore.Ssr;

internal interface IGateway
{
    public Task<SsrResponse?> Dispatch(object model, string url);
}

internal class Gateway : IGateway
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly InertiaOptions _inertiaOptions;
    private readonly ILogger<Gateway>? _logger;

    public Gateway(IHttpClientFactory httpClientFactory)
        : this(httpClientFactory, null)
    {
    }

    public Gateway(IHttpClientFactory httpClientFactory, IOptions<JsonOptions>? jsonOptions)
        : this(httpClientFactory, jsonOptions, null, null)
    {
    }

    public Gateway(
        IHttpClientFactory httpClientFactory,
        IOptions<JsonOptions>? jsonOptions,
        IOptions<InertiaOptions>? inertiaOptions,
        ILogger<Gateway>? logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _inertiaOptions = inertiaOptions?.Value ?? new InertiaOptions();
        _logger = logger;

        JsonSerializerOptions hostOptions = jsonOptions?.Value?.JsonSerializerOptions ?? new JsonSerializerOptions();
        _jsonOptions = new JsonSerializerOptions(hostOptions)
        {
            PropertyNamingPolicy = hostOptions.PropertyNamingPolicy ?? JsonNamingPolicy.CamelCase,
            ReferenceHandler = hostOptions.ReferenceHandler ?? ReferenceHandler.IgnoreCycles
        };
    }

    public async Task<SsrResponse?> Dispatch(object model, string url)
    {
        if (!TryGetValidUri(url, out var uri))
        {
            _logger?.LogWarning(
                "Inertia SSR URL '{Url}' is not a valid absolute http/https URL; falling back to client-side rendering.",
                url);
            return null;
        }

        var json = JsonSerializer.Serialize(model, _jsonOptions);

        using var timeout = new CancellationTokenSource(_inertiaOptions.SsrTimeout);

        try
        {
            var client = _httpClientFactory.CreateClient();
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await client.PostAsync(uri, content, timeout.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning(
                    "Inertia SSR request to {Url} failed with status {Status}; falling back to client-side rendering.",
                    uri,
                    (int)response.StatusCode);
                return null;
            }

            var bytes = await ReadBoundedAsync(response.Content, _inertiaOptions.SsrMaxResponseBytes, timeout.Token)
                .ConfigureAwait(false);

            if (bytes is null)
            {
                _logger?.LogWarning(
                    "Inertia SSR response from {Url} exceeded the {Limit} byte limit; falling back to client-side rendering.",
                    uri,
                    _inertiaOptions.SsrMaxResponseBytes);
                return null;
            }

            var parsed = JsonSerializer.Deserialize<SsrResponse>(bytes, _jsonOptions);
            if (parsed?.Body is null)
            {
                _logger?.LogWarning(
                    "Inertia SSR response from {Url} had no body; falling back to client-side rendering.",
                    uri);
                return null;
            }

            return parsed;
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning(
                "Inertia SSR request to {Url} timed out after {Timeout}; falling back to client-side rendering.",
                uri,
                _inertiaOptions.SsrTimeout);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogWarning(
                ex,
                "Inertia SSR request to {Url} failed; falling back to client-side rendering.",
                uri);
            return null;
        }
        catch (JsonException ex)
        {
            _logger?.LogWarning(
                ex,
                "Inertia SSR response from {Url} was not valid JSON; falling back to client-side rendering.",
                uri);
            return null;
        }
    }

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
