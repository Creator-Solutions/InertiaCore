using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
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

    public Gateway(IHttpClientFactory httpClientFactory)
        : this(httpClientFactory, null)
    {
    }

    public Gateway(IHttpClientFactory httpClientFactory, IOptions<JsonOptions>? jsonOptions)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

        JsonSerializerOptions hostOptions = jsonOptions?.Value?.JsonSerializerOptions ?? new JsonSerializerOptions();
        _jsonOptions = new JsonSerializerOptions(hostOptions)
        {
            PropertyNamingPolicy = hostOptions.PropertyNamingPolicy ?? JsonNamingPolicy.CamelCase,
            ReferenceHandler = hostOptions.ReferenceHandler ?? ReferenceHandler.IgnoreCycles
        };
    }

    public async Task<SsrResponse?> Dispatch(object model, string url)
    {
        var json = JsonSerializer.Serialize(model, _jsonOptions);
        var content = new StringContent(json.ToString(), Encoding.UTF8, "application/json");

        var client = _httpClientFactory.CreateClient();
        var response = await client.PostAsync(url, content);
        return await response.Content.ReadFromJsonAsync<SsrResponse>(_jsonOptions);
    }
}
