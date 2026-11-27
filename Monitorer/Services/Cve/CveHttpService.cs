using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Monitorer.Services.Cve;

/// <summary>HTTP status of a CVE source that is not 2xx.</summary>
public class CveHttpException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public CveHttpException(HttpStatusCode statusCode, string url)
        : base($"HTTP {(int)statusCode} {statusCode} ({new Uri(url).Host})") => StatusCode = statusCode;
}

/// <summary>
/// All GETs of the CVE sources. Every response is cached per URL (default 5 min, shared by all checks), so
/// checks that are due at the same time download a source once. Responses are limited to 10 MB.
/// </summary>
public class CveHttpService
{
    public const string ClientName = "Cve";
    private const long MaxBytes = 10 * 1024 * 1024;
    private static readonly TimeSpan DefaultCacheTime = TimeSpan.FromMinutes(5);

    private readonly IHttpClientFactory _httpClients;
    private readonly IMemoryCache _cache;
    private readonly CveSettings _settings;

    public CveHttpService(IHttpClientFactory httpClients, IMemoryCache cache, IOptions<CveSettings> settings)
    {
        _httpClients = httpClients;
        _cache = cache;
        _settings = settings.Value;
    }

    /// <summary>GET as text. cacheKey / cacheTime: own key (e.g. URL + version) and lifetime for documents that change rarely.</summary>
    public async Task<string> GetString(string url, CancellationToken ct, string? cacheKey = null, TimeSpan? cacheTime = null)
    {
        var key = "cve-http:" + (cacheKey ?? url);
        if (_cache.TryGetValue(key, out string? cached) && cached is not null)
            return cached;

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (new Uri(url).Host == "api.github.com")
        {
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            if (!string.IsNullOrWhiteSpace(_settings.GitHubToken))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.GitHubToken);
        }
        using var response = await _httpClients.CreateClient(ClientName)
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
            throw new CveHttpException(response.StatusCode, url);
        if (response.Content.Headers.ContentLength > MaxBytes)
            throw new InvalidOperationException($"Response of {new Uri(url).Host} is larger than {MaxBytes / 1024 / 1024} MB");

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new LimitedMemoryStream(MaxBytes, $"Response of {new Uri(url).Host} is larger than {MaxBytes / 1024 / 1024} MB");
        await stream.CopyToAsync(buffer, ct);
        var text = Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);

        _cache.Set(key, text, cacheTime ?? DefaultCacheTime);
        return text;
    }

    /// <summary>null on 404 (e.g. a CVE that is not in the opencve-kb yet).</summary>
    public async Task<string?> TryGetString(string url, CancellationToken ct, string? cacheKey = null, TimeSpan? cacheTime = null)
    {
        try
        {
            return await GetString(url, ct, cacheKey, cacheTime);
        }
        catch (CveHttpException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<JsonDocument> GetJson(string url, CancellationToken ct, string? cacheKey = null, TimeSpan? cacheTime = null) =>
        JsonDocument.Parse(await GetString(url, ct, cacheKey, cacheTime));
}
