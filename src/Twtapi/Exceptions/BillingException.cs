using System.Text.Json;

namespace Twtapi;

/// <summary>HTTP 402 — your plan does not cover this endpoint, or billing is past due.</summary>
public sealed class BillingException : TwtApiException
{
    internal BillingException(
        string message,
        int? status,
        string? errorCode,
        JsonElement? body,
        string? bodyText)
        : base(message, status, errorCode, body, bodyText) { }
}
