# Tube2Vid C# SDK

**C# / .NET video downloader API client for YouTube, TikTok, Instagram and 1,800+ sites.** Download MP4 video and MP3 audio from .NET 8+ with the [Tube2Vid](https://tube2vid.com) API: async, typed, built on `HttpClient` and `System.Text.Json`, zero dependencies.

[![CI](https://github.com/Tube2Vid/sdk-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/Tube2Vid/sdk-dotnet/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Tube2Vid)](https://www.nuget.org/packages/Tube2Vid)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/Tube2Vid/sdk-dotnet/blob/main/LICENSE)

## Install

```bash
dotnet add package Tube2Vid
```

You need an API key (`tv_live_...`). [Get a Tube2Vid API key](https://tube2vid.com/pricing) and export it as `TUBE2VID_API_KEY`.

## Quickstart

Submit a download, poll until it finishes, then get a short-lived signed link to the file:

```csharp
using Tube2Vid;

using var client = new Tube2VidClient(); // reads TUBE2VID_API_KEY
var job = await client.SubmitJobAsync(new JobSubmitRequest("https://www.youtube.com/watch?v=dQw4w9WgXcQ")
    { Mode = JobMode.Video, Format = "mp4", Quality = "best" });
var done = await client.WaitForJobAsync(job.JobId);
if (done.Status == JobState.Completed)
    Console.WriteLine(await client.GetDownloadLinkAsync(job.JobId));
else
    Console.Error.WriteLine($"{done.ErrorCode}: {done.ErrorMessage}");
```

For audio only, use `Mode = JobMode.Audio, Format = "mp3"`. To stream the file yourself:

```csharp
using var response = await client.DownloadJobAsync(job.JobId);
await using var file = File.Create("video.mp4");
await response.Content.CopyToAsync(file);
```

## Error handling

Every non-2xx response throws `Tube2VidApiException`, carrying the HTTP status, the API's error message and, when sent, a machine-readable code. Network failures throw its base type, `Tube2VidException`; `WaitForJobAsync` throws `TimeoutException` when its timeout passes.

```csharp
try
{
    await client.SubmitJobAsync(new JobSubmitRequest(url));
}
catch (Tube2VidApiException e) when (e.StatusCode == HttpStatusCode.PaymentRequired)
{
    Console.Error.WriteLine($"Out of credits: {e.Message}");
}
catch (Tube2VidApiException e)
{
    Console.Error.WriteLine($"{(int)e.StatusCode} {e.Code}: {e.Message}");
}
```

| Status | Meaning |
| --- | --- |
| 400 | Invalid body or unsupported platform |
| 401 | Missing or invalid API key |
| 402 | Not enough credits (`e.JsonData` has `creditBalance` and `requiredCredits`) |
| 403 | The key lacks the scope, or the job is not yours |
| 404 | Unknown job |
| 409 | The job has not completed yet |
| 410 | The file has expired |

A job that fails reports `JobState.Failed` with `ErrorCode` and `ErrorMessage`, and its reserved credits are refunded.

## Configuration

```csharp
var client = new Tube2VidClient(new Tube2VidClientOptions
{
    ApiKey = "tv_live_...",                  // default: TUBE2VID_API_KEY
    BaseUrl = "https://tube2vid.com",        // default
    Headers = new Dictionary<string, string> { ["x-request-source"] = "my-app" },
});

await client.WaitForJobAsync(jobId, interval: TimeSpan.FromSeconds(5), timeout: TimeSpan.FromMinutes(30),
    onUpdate: j => Console.WriteLine(j.Progress), ct: cancellationToken);
```

You can pass your own `HttpClient` (for example from `IHttpClientFactory`) as the second constructor argument. Its handler must set `AllowAutoRedirect = false`: the SDK reads the signed-URL redirect itself and never sends your API key to the storage host. Every method takes a `CancellationToken`. The client is thread-safe; share one instance.

## API coverage

| Area | Methods |
| --- | --- |
| Jobs (API key) | `SubmitJobAsync`, `SubmitAuthenticatedJobAsync`, `GetJobStatusAsync`, `GetJobEventsAsync`, `WaitForJobAsync`, `GetDownloadUrl`, `GetDownloadLinkAsync`, `DownloadJobAsync` |
| Account | `GetUsageAsync`, `ListApiKeysAsync`, `CreateApiKeyAsync`, `CreateCheckoutAsync` |
| Platforms and status (no key) | `ListPlatformsAsync`, `GetPlatformHealthAsync`, `GetStatusHistoryAsync`, `GetOperationalMetricsAsync`, `GetRecentDownloadsAsync`, `GetHealthAsync` |
| Anonymous browser flow | `GetPowChallengeAsync`, `SubmitPublicJobAsync`, `GetPublicJobStatusAsync`, `GetPublicDownloadUrl`, `GetPublicThumbnailUrl` |

## Links

- [Tube2Vid online video downloader](https://tube2vid.com)
- [Tube2Vid REST API documentation](https://tube2vid.com/docs)
- [Pricing and API keys](https://tube2vid.com/pricing)
- [Live platform status for YouTube, TikTok, Instagram and more](https://tube2vid.com/status)

Other languages: [TypeScript](https://github.com/Tube2Vid/sdk), [Python](https://github.com/Tube2Vid/sdk-python), [Go](https://github.com/Tube2Vid/sdk-go), [Java](https://github.com/Tube2Vid/sdk-java), [C#](https://github.com/Tube2Vid/sdk-dotnet), [PHP](https://github.com/Tube2Vid/sdk-php), [Ruby](https://github.com/Tube2Vid/sdk-ruby), [Rust](https://github.com/Tube2Vid/sdk-rust), [Swift](https://github.com/Tube2Vid/sdk-swift).

## Development

```bash
dotnet test tests/Tube2Vid.Tests
```

Tests use a fake `HttpMessageHandler`; no network or API key needed.

## License

[MIT](https://github.com/Tube2Vid/sdk-dotnet/blob/main/LICENSE) © 2026 Tube2Vid
