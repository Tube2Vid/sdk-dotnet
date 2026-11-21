using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tube2Vid;

/// <summary>Options for <see cref="Tube2VidClient"/>.</summary>
public sealed record Tube2VidClientOptions
{
    /// <summary>A <c>tv_live_...</c> key. Null reads <c>TUBE2VID_API_KEY</c>; an empty string means no key.</summary>
    public string? ApiKey { get; init; }

    /// <summary>API origin, default https://tube2vid.com.</summary>
    public string BaseUrl { get; init; } = Tube2VidClient.DefaultBaseUrl;

    /// <summary>Extra headers sent with every API request.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }
}

/// <summary>
/// Client for the Tube2Vid media download API (https://tube2vid.com/docs). Thread-safe; share one instance.
/// </summary>
public sealed class Tube2VidClient : IDisposable
{
    /// <summary>The production API origin.</summary>
    public const string DefaultBaseUrl = "https://tube2vid.com";

    /// <summary>Environment variable read when no API key is passed.</summary>
    public const string ApiKeyEnvironmentVariable = "TUBE2VID_API_KEY";

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly string? _apiKey;
    private readonly IReadOnlyDictionary<string, string> _headers;

    /// <summary>Base URL without a trailing slash.</summary>
    public string BaseUrl { get; }

    /// <summary>True when requests carry an API key.</summary>
    public bool HasApiKey => _apiKey is not null;

    /// <summary>Creates a client using <c>TUBE2VID_API_KEY</c> and https://tube2vid.com.</summary>
    public Tube2VidClient() : this(new Tube2VidClientOptions()) { }

    /// <summary>Creates a client with the given key and https://tube2vid.com.</summary>
    public Tube2VidClient(string apiKey) : this(new Tube2VidClientOptions { ApiKey = apiKey }) { }

    /// <summary>
    /// Creates a client. A supplied <paramref name="httpClient"/> must not follow redirects
    /// (<c>AllowAutoRedirect = false</c>), since the SDK resolves the signed download URL itself, and is not disposed by this client.
    /// </summary>
    public Tube2VidClient(Tube2VidClientOptions options, HttpClient? httpClient = null)
    {
        BaseUrl = options.BaseUrl.TrimEnd('/');
        var key = options.ApiKey ?? Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable);
        _apiKey = string.IsNullOrWhiteSpace(key) ? null : key.Trim();
        _headers = options.Headers ?? new Dictionary<string, string>();
        _ownsHttp = httpClient is null;
        _http = httpClient ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
    }

    // Public, no API key needed.

    /// <summary>Service liveness.</summary>
    public Task<ServiceHealth> GetHealthAsync(CancellationToken ct = default) => GetAsync<ServiceHealth>("/healthz", ct);

    /// <summary>First-class platforms and their providers.</summary>
    public Task<IReadOnlyList<Platform>> ListPlatformsAsync(CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<Platform>>("/v1/platforms", ct);

    /// <summary>Live status per platform.</summary>
    public Task<IReadOnlyList<PlatformHealth>> GetPlatformHealthAsync(CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<PlatformHealth>>("/v1/platforms/health", ct);

    /// <summary>Platform health check history, optionally for one platform slug.</summary>
    public Task<IReadOnlyList<StatusHistoryItem>> GetStatusHistoryAsync(int limit = 50, string? platform = null, CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<StatusHistoryItem>>(
            $"/v1/status/history?limit={limit}" + (string.IsNullOrEmpty(platform) ? "" : "&platform=" + Uri.EscapeDataString(platform)), ct);

    /// <summary>Queue and job metrics.</summary>
    public Task<OperationalMetrics> GetOperationalMetricsAsync(CancellationToken ct = default) =>
        GetAsync<OperationalMetrics>("/v1/ops/metrics", ct);

    /// <summary>Anonymous submission, as the website does it. Requires a solved Atlas <c>CaptchaToken</c>; scripts should use <see cref="SubmitJobAsync"/> with an API key.</summary>
    public Task<QueuedJob> SubmitPublicJobAsync(JobSubmitRequest request, CancellationToken ct = default) =>
        PostAsync<QueuedJob>("/v1/public/jobs", request, ct);

    /// <summary>Status of an anonymous job.</summary>
    public Task<PublicJobStatus> GetPublicJobStatusAsync(string jobId, CancellationToken ct = default) =>
        GetAsync<PublicJobStatus>($"/v1/public/jobs/{Uri.EscapeDataString(jobId)}", ct);

    /// <summary>Direct link to an anonymous job's file; it redirects to a short-lived signed URL.</summary>
    public string GetPublicDownloadUrl(string jobId) => $"{BaseUrl}/v1/public/jobs/{Uri.EscapeDataString(jobId)}/download";

    /// <summary>Direct link to an anonymous job's thumbnail.</summary>
    public string GetPublicThumbnailUrl(string jobId) => $"{BaseUrl}/v1/public/jobs/{Uri.EscapeDataString(jobId)}/thumbnail";

    // Authenticated, needs an API key.

    /// <summary>Submits a download with the API key (POST /v1/jobs).</summary>
    public Task<QueuedJob> SubmitAuthenticatedJobAsync(JobSubmitRequest request, CancellationToken ct = default) =>
        PostAsync<QueuedJob>("/v1/jobs", request, ct);

    /// <summary>Uses /v1/jobs with an API key, /v1/public/jobs without one.</summary>
    public Task<QueuedJob> SubmitJobAsync(JobSubmitRequest request, CancellationToken ct = default) =>
        HasApiKey ? SubmitAuthenticatedJobAsync(request, ct) : SubmitPublicJobAsync(request, ct);

    /// <summary>Status of a job.</summary>
    public Task<JobStatus> GetJobStatusAsync(string jobId, CancellationToken ct = default) =>
        GetAsync<JobStatus>($"/v1/jobs/{Uri.EscapeDataString(jobId)}", ct);

    /// <summary>A job's event log.</summary>
    public Task<IReadOnlyList<JobEvent>> GetJobEventsAsync(string jobId, CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<JobEvent>>($"/v1/jobs/{Uri.EscapeDataString(jobId)}/events", ct);

    /// <summary>
    /// Polls <see cref="GetJobStatusAsync"/> until the job is completed or failed and returns the final job either way.
    /// Defaults: every 2 seconds, for up to 15 minutes.
    /// </summary>
    /// <exception cref="TimeoutException">The timeout passed first.</exception>
    public async Task<JobStatus> WaitForJobAsync(string jobId, TimeSpan? interval = null, TimeSpan? timeout = null,
        Action<JobStatus>? onUpdate = null, CancellationToken ct = default)
    {
        var every = interval ?? TimeSpan.FromSeconds(2);
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromMinutes(15));
        while (true)
        {
            var job = await GetJobStatusAsync(jobId, ct).ConfigureAwait(false);
            onUpdate?.Invoke(job);
            if (job.Status.IsTerminal()) return job;
            if (DateTime.UtcNow + every > deadline) throw new TimeoutException($"Timed out waiting for job {jobId}");
            await Task.Delay(every, ct).ConfigureAwait(false);
        }
    }

    /// <summary>The authenticated download endpoint. It needs the bearer header, so prefer <see cref="GetDownloadLinkAsync"/> or <see cref="DownloadJobAsync"/>.</summary>
    public string GetDownloadUrl(string jobId) => $"{BaseUrl}/v1/jobs/{Uri.EscapeDataString(jobId)}/download";

    /// <summary>Resolves a completed job to its short-lived signed file URL, which needs no auth.</summary>
    public async Task<string> GetDownloadLinkAsync(string jobId, CancellationToken ct = default)
    {
        using var response = await SendAsync(Request(HttpMethod.Get, GetDownloadUrl(jobId)), HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (Location(response) is { } location) return location.ToString();
        if (!response.IsSuccessStatusCode) throw await ErrorAsync(response, ct).ConfigureAwait(false);
        throw new Tube2VidApiException(response.StatusCode, "This API serves the file directly; use DownloadJobAsync instead.");
    }

    /// <summary>Fetches a completed job's file. Dispose the response; read it with <c>Content.ReadAsStreamAsync</c>.</summary>
    public async Task<HttpResponseMessage> DownloadJobAsync(string jobId, CancellationToken ct = default)
    {
        var response = await SendAsync(Request(HttpMethod.Get, GetDownloadUrl(jobId)), HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (Location(response) is { } location)
        {
            response.Dispose();
            // The signed URL carries its own auth; the API key must not be sent to the storage host.
            response = await SendAsync(new HttpRequestMessage(HttpMethod.Get, location), HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        }
        if (response.IsSuccessStatusCode) return response;
        using (response) throw await ErrorAsync(response, ct).ConfigureAwait(false);
    }

    /// <summary>Credit balance and job counts.</summary>
    public Task<Usage> GetUsageAsync(CancellationToken ct = default) => GetAsync<Usage>("/v1/usage", ct);

    /// <summary>Lists API keys. Needs the keys:read scope.</summary>
    public Task<IReadOnlyList<ApiKeySummary>> ListApiKeysAsync(CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<ApiKeySummary>>("/v1/api-keys", ct);

    /// <summary>Creates an API key. Needs the keys:write scope. The returned <c>Key</c> is shown only once.</summary>
    public Task<CreatedApiKey> CreateApiKeyAsync(ApiKeyCreateRequest request, CancellationToken ct = default) =>
        PostAsync<CreatedApiKey>("/v1/api-keys", request, ct);

    /// <summary>Starts a credit purchase. Needs the billing:write scope. Open <c>CheckoutUrl</c> to pay.</summary>
    public Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken ct = default) =>
        PostAsync<CheckoutSession>("/v1/billing/checkout", request, ct);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }

    private Task<T> GetAsync<T>(string path, CancellationToken ct) =>
        ReadAsync<T>(Request(HttpMethod.Get, BaseUrl + path), ct);

    private Task<T> PostAsync<T>(string path, object body, CancellationToken ct)
    {
        var request = Request(HttpMethod.Post, BaseUrl + path);
        request.Content = JsonContent.Create(body, body.GetType(), options: Json);
        return ReadAsync<T>(request, ct);
    }

    private async Task<T> ReadAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await SendAsync(request, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) throw await ErrorAsync(response, ct).ConfigureAwait(false);
        try
        {
            return (await response.Content.ReadFromJsonAsync<T>(Json, ct).ConfigureAwait(false))!;
        }
        catch (JsonException e)
        {
            throw new Tube2VidException("Could not parse Tube2Vid API response", e);
        }
    }

    private HttpRequestMessage Request(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (_apiKey is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        foreach (var (name, value) in _headers)
        {
            request.Headers.Remove(name);
            request.Headers.TryAddWithoutValidation(name, value);
        }
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completion, CancellationToken ct)
    {
        using (request)
        {
            try
            {
                return await _http.SendAsync(request, completion, ct).ConfigureAwait(false);
            }
            catch (HttpRequestException e)
            {
                throw new Tube2VidException($"Tube2Vid request failed: {e.Message}", e);
            }
        }
    }

    private Uri? Location(HttpResponseMessage response)
    {
        var status = (int)response.StatusCode;
        if (status < 300 || status >= 400 || response.Headers.Location is not { } location) return null;
        // On Unix a relative "/path" can parse as an absolute file:// URI, so check the scheme rather than IsAbsoluteUri.
        return location.IsAbsoluteUri && location.Scheme is "http" or "https" ? location : new Uri(new Uri(BaseUrl + "/"), location.OriginalString);
    }

    private static async Task<Tube2VidApiException> ErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        string body;
        try { body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false); }
        catch (HttpRequestException) { body = ""; }
        return new Tube2VidApiException(response.StatusCode, body);
    }
}
