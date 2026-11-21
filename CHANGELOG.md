# Changelog

## 0.1.0 - 2026-11-21

- First release: async, typed .NET 8+ client for the Tube2Vid API (`/v1`), built on `HttpClient` and `System.Text.Json` with no dependencies.
- Jobs: `SubmitJobAsync`, `GetJobStatusAsync`, `GetJobEventsAsync`, `WaitForJobAsync`, `GetDownloadLinkAsync`, `DownloadJobAsync`.
- Account: `GetUsageAsync`, `ListApiKeysAsync`, `CreateApiKeyAsync`, `CreateCheckoutAsync`.
- Public: platforms, platform health, status history, metrics, recent downloads and the anonymous job flow.
- `Tube2VidApiException` with HTTP status, API error message and error code.
