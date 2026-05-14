namespace Twtapi;

/// <summary>
/// Connectivity failure (DNS, TCP, TLS, request timeout). No HTTP status —
/// the request never completed.
/// </summary>
public sealed class NetworkException : TwtApiException
{
    internal NetworkException(string message, Exception? innerException)
        : base(message, status: null, errorCode: null, body: null, bodyText: null, innerException) { }
}
