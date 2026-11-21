using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tube2Vid;

/// <summary>Lifecycle state of a download job.</summary>
[JsonConverter(typeof(LenientEnumConverter<JobState>))]
public enum JobState
{
    /// <summary>A state this SDK version does not know yet.</summary>
    Unknown,
    /// <summary>Waiting for a worker.</summary>
    Queued,
    /// <summary>Downloading.</summary>
    Running,
    /// <summary>The file is ready.</summary>
    Completed,
    /// <summary>The job failed; reserved credits were refunded.</summary>
    Failed,
}

/// <summary>What to extract from the URL.</summary>
[JsonConverter(typeof(LenientEnumConverter<JobMode>))]
public enum JobMode
{
    /// <summary>A mode this SDK version does not know yet.</summary>
    Unknown,
    /// <summary>Pick the best output for the URL.</summary>
    Auto,
    /// <summary>Video file.</summary>
    Video,
    /// <summary>Audio only.</summary>
    Audio,
    /// <summary>Image gallery.</summary>
    Gallery,
}

/// <summary>Extension helpers for <see cref="JobState"/>.</summary>
public static class JobStateExtensions
{
    /// <summary>True for <see cref="JobState.Completed"/> and <see cref="JobState.Failed"/>.</summary>
    public static bool IsTerminal(this JobState state) => state is JobState.Completed or JobState.Failed;
}

/// <summary>A solved <see cref="PowChallenge"/>.</summary>
public sealed record ProofOfWork(string Nonce, string ExpiresAt, int Difficulty, string Signature, string Solution);

/// <summary>Body of a job submission.</summary>
public sealed record JobSubmitRequest(string Url)
{
    /// <summary>Force a platform slug instead of detecting it from the URL.</summary>
    public string? Platform { get; init; }
    /// <summary>What to extract, default auto.</summary>
    public JobMode? Mode { get; init; }
    /// <summary>Output format, e.g. mp4 or mp3.</summary>
    public string? Format { get; init; }
    /// <summary>Quality, e.g. best or 720p.</summary>
    public string? Quality { get; init; }
    /// <summary>Browser-only human check; not needed with an API key.</summary>
    public string? TurnstileToken { get; init; }
    /// <summary>Browser-only work challenge; not needed with an API key.</summary>
    public ProofOfWork? Pow { get; init; }
}

/// <summary>Response to a job submission.</summary>
public sealed record QueuedJob
{
    /// <summary>Job id.</summary>
    public string JobId { get; init; } = "";
    /// <summary>Queued for a new job; a deduplicated public submission returns the existing job's state.</summary>
    public JobState Status { get; init; }
    /// <summary>Detected platform slug.</summary>
    public string Platform { get; init; } = "";
    /// <summary>Credits reserved for the job.</summary>
    public int EstimatedCredits { get; init; }
    /// <summary>True when a public submission was folded into an identical job from the last minute.</summary>
    public bool? Deduped { get; init; }
}

/// <summary>A provider in a platform's chain.</summary>
public sealed record PlatformProvider(string Id, string Kind, int Priority);

/// <summary>A first-class platform and its provider chain.</summary>
public sealed record Platform
{
    /// <summary>Slug, e.g. youtube.</summary>
    public string Slug { get; init; } = "";
    /// <summary>Display name.</summary>
    public string Name { get; init; } = "";
    /// <summary>Category.</summary>
    public string Category { get; init; } = "";
    /// <summary>Support level.</summary>
    public string SupportLevel { get; init; } = "";
    /// <summary>Whether support is publicly claimed.</summary>
    public bool PublicClaim { get; init; }
    /// <summary>Capabilities.</summary>
    public IReadOnlyList<string> Capabilities { get; init; } = [];
    /// <summary>Providers, by priority.</summary>
    public IReadOnlyList<PlatformProvider> Providers { get; init; } = [];
}

/// <summary>Live health of one platform.</summary>
public sealed record PlatformHealth
{
    /// <summary>Platform slug.</summary>
    public string Platform { get; init; } = "";
    /// <summary>Display name.</summary>
    public string Name { get; init; } = "";
    /// <summary>Category.</summary>
    public string Category { get; init; } = "";
    /// <summary>Support level.</summary>
    public string SupportLevel { get; init; } = "";
    /// <summary>operational, degraded, partial_outage, down or beta.</summary>
    public string Status { get; init; } = "";
    /// <summary>Audit category.</summary>
    public string AuditCategory { get; init; } = "";
    /// <summary>Provider in use.</summary>
    public string? Provider { get; init; }
    /// <summary>Provider kind.</summary>
    public string? ProviderKind { get; init; }
    /// <summary>Recent success rate, 0-1.</summary>
    public double? SuccessRate { get; init; }
    /// <summary>Median extraction time.</summary>
    public double? MedianExtractMs { get; init; }
    /// <summary>Last probe time.</summary>
    public DateTimeOffset? LastCheckedAt { get; init; }
    /// <summary>Operator note.</summary>
    public string? Note { get; init; }
    /// <summary>Status reason.</summary>
    public string? Reason { get; init; }
    /// <summary>Whether the platform needs auth.</summary>
    public bool? AuthNeeded { get; init; }
    /// <summary>Whether it was recently repaired.</summary>
    public bool? Repaired { get; init; }
}

/// <summary>A recent public download.</summary>
public sealed record RecentDownload
{
    /// <summary>Platform slug.</summary>
    public string Platform { get; init; } = "";
    /// <summary>Media title.</summary>
    public string? Title { get; init; }
    /// <summary>Source URL.</summary>
    public string? SourceUrl { get; init; }
    /// <summary>Mode.</summary>
    public string Mode { get; init; } = "";
    /// <summary>Format.</summary>
    public string Format { get; init; } = "";
    /// <summary>Status.</summary>
    public string Status { get; init; } = "";
    /// <summary>Thumbnail URL.</summary>
    public string? ThumbnailUrl { get; init; }
    /// <summary>Creation time.</summary>
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>One entry in a job's event log.</summary>
public sealed record JobEvent
{
    /// <summary>Event id.</summary>
    public string Id { get; init; } = "";
    /// <summary>Job id.</summary>
    public string JobId { get; init; } = "";
    /// <summary>Pipeline stage.</summary>
    public string Stage { get; init; } = "";
    /// <summary>Message.</summary>
    public string Message { get; init; } = "";
    /// <summary>Free-form details.</summary>
    public JsonElement? Details { get; init; }
    /// <summary>Creation time.</summary>
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Latest event of an anonymous job.</summary>
public sealed record PublicJobEvent
{
    /// <summary>Pipeline stage.</summary>
    public string Stage { get; init; } = "";
    /// <summary>Message.</summary>
    public string? Message { get; init; }
    /// <summary>Free-form details.</summary>
    public JsonElement? Details { get; init; }
    /// <summary>Creation time.</summary>
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Status of an anonymous job.</summary>
public sealed record PublicJobStatus
{
    /// <summary>Job id.</summary>
    public string JobId { get; init; } = "";
    /// <summary>Platform slug.</summary>
    public string Platform { get; init; } = "";
    /// <summary>State.</summary>
    public JobState Status { get; init; }
    /// <summary>Progress, 0-100.</summary>
    public double Progress { get; init; }
    /// <summary>Provider that ran the job.</summary>
    public string? Provider { get; init; }
    /// <summary>True when the file came from a reduced-quality last-resort provider.</summary>
    public bool? Fallback { get; init; }
    /// <summary>Media title.</summary>
    public string? Title { get; init; }
    /// <summary>Error code on failure.</summary>
    public string? ErrorCode { get; init; }
    /// <summary>Error message on failure.</summary>
    public string? ErrorMessage { get; init; }
    /// <summary>Completion time.</summary>
    public DateTimeOffset? CompletedAt { get; init; }
    /// <summary>Creation time.</summary>
    public DateTimeOffset CreatedAt { get; init; }
    /// <summary>Last update time.</summary>
    public DateTimeOffset UpdatedAt { get; init; }
    /// <summary>Latest event.</summary>
    public PublicJobEvent? LatestEvent { get; init; }
}

/// <summary>Full status of an authenticated job.</summary>
public sealed record JobStatus
{
    /// <summary>Job id.</summary>
    public string Id { get; init; } = "";
    /// <summary>Job id (alias).</summary>
    public string? JobId { get; init; }
    /// <summary>Owner.</summary>
    public string? UserId { get; init; }
    /// <summary>API key that submitted it.</summary>
    public string? ApiKeyId { get; init; }
    /// <summary>Source URL.</summary>
    public string Url { get; init; } = "";
    /// <summary>Platform slug.</summary>
    public string Platform { get; init; } = "";
    /// <summary>Mode.</summary>
    public JobMode Mode { get; init; }
    /// <summary>Format.</summary>
    public string Format { get; init; } = "";
    /// <summary>Quality.</summary>
    public string Quality { get; init; } = "";
    /// <summary>Credits reserved.</summary>
    public int CreditsReserved { get; init; }
    /// <summary>State.</summary>
    public JobState Status { get; init; }
    /// <summary>Progress, 0-100.</summary>
    public double Progress { get; init; }
    /// <summary>Credits charged.</summary>
    public int CreditsCharged { get; init; }
    /// <summary>Provider that ran the job.</summary>
    public string? Provider { get; init; }
    /// <summary>Media title.</summary>
    public string? Title { get; init; }
    /// <summary>Output path.</summary>
    public string? OutputPath { get; init; }
    /// <summary>Storage key.</summary>
    public string? OutputStorageKey { get; init; }
    /// <summary>Output size in bytes.</summary>
    public long? OutputSize { get; init; }
    /// <summary>Thumbnail URL.</summary>
    public string? ThumbnailUrl { get; init; }
    /// <summary>Error code on failure.</summary>
    public string? ErrorCode { get; init; }
    /// <summary>Error message on failure.</summary>
    public string? ErrorMessage { get; init; }
    /// <summary>Completion time.</summary>
    public DateTimeOffset? CompletedAt { get; init; }
    /// <summary>Creation time.</summary>
    public DateTimeOffset CreatedAt { get; init; }
    /// <summary>Last update time.</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>Credit balance and job counts.</summary>
public sealed record Usage(long CreditBalance, long JobsTotal, long JobsCompleted, long JobsFailed);

/// <summary>One platform health check.</summary>
public sealed record StatusHistoryItem
{
    /// <summary>Check id.</summary>
    public string Id { get; init; } = "";
    /// <summary>Platform slug.</summary>
    public string Platform { get; init; } = "";
    /// <summary>Provider.</summary>
    public string Provider { get; init; } = "";
    /// <summary>Result.</summary>
    public string Status { get; init; } = "";
    /// <summary>Duration.</summary>
    public long? DurationMs { get; init; }
    /// <summary>Error code.</summary>
    public string? ErrorCode { get; init; }
    /// <summary>Error message.</summary>
    public string? ErrorMessage { get; init; }
    /// <summary>Time of the check.</summary>
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Failure rate of one provider.</summary>
public sealed record ProviderFailureRate(string Provider, long Total, long Failed, double FailureRate);

/// <summary>An operational alert. Level is info, warn or critical.</summary>
public sealed record OperationalAlert(string Level, string Code, string Message);

/// <summary>Queue and job metrics.</summary>
public sealed record OperationalMetrics
{
    /// <summary>Generation time.</summary>
    public DateTimeOffset GeneratedAt { get; init; }
    /// <summary>Queue depth.</summary>
    public long QueueDepth { get; init; }
    /// <summary>Queued jobs.</summary>
    public long QueuedJobs { get; init; }
    /// <summary>Running jobs.</summary>
    public long RunningJobs { get; init; }
    /// <summary>Completed in 24 h.</summary>
    public long CompletedJobs24h { get; init; }
    /// <summary>Failed in 24 h.</summary>
    public long FailedJobs24h { get; init; }
    /// <summary>p95 job duration.</summary>
    public long? P95JobDurationMs { get; init; }
    /// <summary>Per-provider failure rates.</summary>
    public IReadOnlyList<ProviderFailureRate> ProviderFailureRates { get; init; } = [];
    /// <summary>Active alerts.</summary>
    public IReadOnlyList<OperationalAlert> Alerts { get; init; } = [];
}

/// <summary>Service liveness.</summary>
public sealed record ServiceHealth(bool Ok, string Service, DateTimeOffset Time);

/// <summary>A work challenge for the anonymous flow.</summary>
public sealed record PowChallenge(string Nonce, string ExpiresAt, int Difficulty, string Signature);

/// <summary>Body of an API key creation.</summary>
public sealed record ApiKeyCreateRequest(string Name)
{
    /// <summary>Defaults to jobs:write, jobs:read, usage:read. Others: keys:read, keys:write, billing:write.</summary>
    public IReadOnlyList<string>? Scopes { get; init; }
    /// <summary>1-1000, defaults to 60.</summary>
    public int? RateLimitPerMinute { get; init; }
}

/// <summary>A new API key. <see cref="Key"/> is only ever returned once.</summary>
public sealed record CreatedApiKey(string Id, string Name, string Key, string Prefix, IReadOnlyList<string> Scopes);

/// <summary>An existing API key.</summary>
public sealed record ApiKeySummary
{
    /// <summary>Key id.</summary>
    public string Id { get; init; } = "";
    /// <summary>Name.</summary>
    public string Name { get; init; } = "";
    /// <summary>Visible prefix.</summary>
    public string Prefix { get; init; } = "";
    /// <summary>Scopes.</summary>
    public IReadOnlyList<string> Scopes { get; init; } = [];
    /// <summary>Rate limit.</summary>
    public int RateLimitPerMinute { get; init; }
    /// <summary>Revocation time.</summary>
    public DateTimeOffset? RevokedAt { get; init; }
    /// <summary>Creation time.</summary>
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Body of a credit purchase: set <see cref="Credits"/> (1,000-250,000) or a legacy <see cref="Pack"/>.</summary>
public sealed record CheckoutRequest
{
    /// <summary>1,000-250,000 credits.</summary>
    public int? Credits { get; init; }
    /// <summary>Legacy fixed pack: starter, builder or scale.</summary>
    public string? Pack { get; init; }
}

/// <summary>A checkout session. Open <see cref="CheckoutUrl"/> to pay.</summary>
public sealed record CheckoutSession
{
    /// <summary>Payment page.</summary>
    public string? CheckoutUrl { get; init; }
    /// <summary>stripe or placeholder.</summary>
    public string Mode { get; init; } = "";
    /// <summary>Stripe session id.</summary>
    public string? SessionId { get; init; }
    /// <summary>Credits bought.</summary>
    public long Credits { get; init; }
    /// <summary>Price in cents.</summary>
    public long AmountCents { get; init; }
    /// <summary>Note from the API.</summary>
    public string? Message { get; init; }
}

/// <summary>Reads enums case-insensitively, mapping unknown values to the zero member; writes them lowercase.</summary>
internal sealed class LenientEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String && Enum.TryParse<T>(reader.GetString(), true, out var value) ? value : default;

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString().ToLowerInvariant());
}
