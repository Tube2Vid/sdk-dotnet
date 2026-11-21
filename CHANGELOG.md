# Changelog

## 0.2.0 (2026-11-21)

- The anonymous browser flow now uses an Atlas captcha: `JobSubmitRequest.CaptchaToken` replaces `Pow` and `TurnstileToken`, and a failed check comes back with code `captcha_failed`.
- Removed `GetPowChallengeAsync`, `PowChallenge` and `ProofOfWork`: the API no longer serves `GET /v1/challenges/pow`. Calls with an API key are unchanged.

## 0.1.0 - 2026-11-21

- First release: async, typed .NET 8+ client for the Tube2Vid API (`/v1`), built on `HttpClient` and `System.Text.Json` with no dependencies.
- Jobs: `SubmitJobAsync`, `GetJobStatusAsync`, `GetJobEventsAsync`, `WaitForJobAsync`, `GetDownloadLinkAsync`, `DownloadJobAsync`.
- Account: `GetUsageAsync`, `ListApiKeysAsync`, `CreateApiKeyAsync`, `CreateCheckoutAsync`.
- Public: platforms, platform health, status history, metrics and the anonymous job flow.
- `Tube2VidApiException` with HTTP status, API error message and error code.
