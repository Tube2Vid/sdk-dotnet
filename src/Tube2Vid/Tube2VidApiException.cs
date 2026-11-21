using System.Net;
using System.Text.Json;

namespace Tube2Vid;

/// <summary>Base type for every error this SDK throws (network failures, timeouts).</summary>
public class Tube2VidException : Exception
{
    /// <summary>Creates the exception.</summary>
    public Tube2VidException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>Thrown for any non-2xx API response. <see cref="Exception.Message"/> is the API's <c>error</c> field when it sent one.</summary>
public sealed class Tube2VidApiException : Tube2VidException
{
    /// <summary>HTTP status code.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>Raw response body, or null.</summary>
    public string? Body { get; }

    /// <summary>Machine-readable error code when the API sent one (e.g. "no_credits").</summary>
    public string? Code { get; }

    /// <summary>Parsed JSON body, or null when the body was not JSON.</summary>
    public JsonElement? JsonData { get; }

    /// <summary>Builds the exception from a response status and body.</summary>
    public Tube2VidApiException(HttpStatusCode statusCode, string? body)
        : this(statusCode, string.IsNullOrEmpty(body) ? null : body, Parse(body)) { }

    private Tube2VidApiException(HttpStatusCode statusCode, string? body, JsonElement? data)
        : base(Field(data, "error") ?? body ?? $"Tube2Vid API request failed with {(int)statusCode}")
    {
        StatusCode = statusCode;
        Body = body;
        Code = Field(data, "code");
        JsonData = data;
    }

    private static JsonElement? Parse(string? body)
    {
        if (string.IsNullOrEmpty(body)) return null;
        try { return JsonDocument.Parse(body).RootElement.Clone(); }
        catch (JsonException) { return null; }
    }

    private static string? Field(JsonElement? data, string name) =>
        data is { ValueKind: JsonValueKind.Object } d && d.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}
