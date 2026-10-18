using System.Text.Json.Serialization;

namespace FakeHttpClient;

/// <summary>
/// How the raw TCP client sends its request. Flags can be combined, e.g. "NoHalfClose, SplitRequest".
/// </summary>
[Flags]
[JsonConverter(typeof(JsonStringEnumConverter<RequestMode>))]
public enum RequestMode
{
    /// <summary>
    /// Send the whole request at once, then shut down the sending side (half-close).
    /// </summary>
    Normal = 0,

    /// <summary>
    /// Keep the sending side open while waiting for the response, like curl or a browser.
    /// A proxy that waits for recv() to return 0 before reading the request will hang.
    /// </summary>
    NoHalfClose = 1,

    /// <summary>
    /// Send the request in several pieces with pauses between them, splitting the request line,
    /// the headers, and the final "\r\n\r\n" terminator. A proxy that reads the request with a
    /// single recv() will see an incomplete request.
    /// </summary>
    SplitRequest = 2,

    /// <summary>
    /// Add padding headers so the request is about 10 KB: larger than common 4 KB and 8 KB buffers,
    /// and more than one recv() for most proxies. A proxy with a small fixed buffer, or one that
    /// reads the request with a single recv(), will see a truncated request.
    /// </summary>
    LargeRequest = 4,
}

public static class RequestModeParser
{
    public static RequestMode Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return RequestMode.Normal;
        }
        if (!Enum.TryParse<RequestMode>(value, ignoreCase: true, out var mode) || int.TryParse(value, out _))
        {
            throw new ArgumentException($"Unknown mode '{value}'. Valid modes: {string.Join(", ", Enum.GetNames<RequestMode>())} (combine with commas).");
        }
        return mode;
    }
}
