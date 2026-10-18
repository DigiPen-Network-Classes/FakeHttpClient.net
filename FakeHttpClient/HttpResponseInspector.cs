using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace FakeHttpClient;

/// <summary>
/// Parses a raw HTTP/1.1 response as it streams in: the status line and headers, then the body
/// (de-chunked if Transfer-Encoding is chunked). The body is hashed incrementally, so large
/// responses never have to be held in memory.
/// </summary>
public sealed class HttpResponseInspector : IDisposable
{
    private const int MaxHeaderBytes = 64 * 1024;

    private enum State { Headers, Body, ChunkSize, ChunkData, ChunkDataEnd, Trailer, Done, Malformed }

    private readonly IncrementalHash _sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private readonly List<byte> _line = new();
    private State _state = State.Headers;
    private long _chunkRemaining;
    private int _chunkEndRemaining;
    private string? _sha256Hex;

    public int? StatusCode { get; private set; }
    public long? ContentLength { get; private set; }
    public bool Chunked { get; private set; }
    public long BodyLength { get; private set; }
    public bool HeadersComplete => StatusCode != null;
    public bool Malformed => _state == State.Malformed;

    /// <summary>
    /// Hex SHA-256 of the (de-chunked) body. Only valid after the response has ended.
    /// </summary>
    public string BodySha256 => _sha256Hex ??= Convert.ToHexString(_sha256.GetHashAndReset()).ToLowerInvariant();

    /// <summary>
    /// Call once the connection has closed. True if the whole body arrived: all Content-Length bytes,
    /// or the terminating zero-length chunk, or (with neither) the connection closing after the headers.
    /// </summary>
    public bool IsComplete
    {
        get
        {
            if (!HeadersComplete || Malformed)
            {
                return false;
            }
            if (Chunked)
            {
                return _state == State.Done;
            }
            return ContentLength == null || BodyLength == ContentLength;
        }
    }

    public void Feed(ReadOnlySpan<byte> data)
    {
        var i = 0;
        while (i < data.Length && _state is not (State.Done or State.Malformed))
        {
            switch (_state)
            {
                case State.Headers:
                    _line.Add(data[i++]);
                    if (EndsWithBlankLine(_line))
                    {
                        ParseHeaders(Encoding.ASCII.GetString(_line.ToArray()));
                        _line.Clear();
                    }
                    else if (_line.Count > MaxHeaderBytes)
                    {
                        _state = State.Malformed;
                    }
                    break;

                case State.Body:
                {
                    var take = data.Length - i;
                    if (ContentLength != null)
                    {
                        take = (int)Math.Min(take, ContentLength.Value - BodyLength);
                    }
                    AddBody(data.Slice(i, take));
                    i += take;
                    if (ContentLength != null && BodyLength == ContentLength)
                    {
                        _state = State.Done;
                    }
                    break;
                }

                case State.ChunkSize:
                    _line.Add(data[i++]);
                    if (EndsWithCrlf(_line))
                    {
                        var sizeText = Encoding.ASCII.GetString(_line.ToArray(), 0, _line.Count - 2).Split(';')[0].Trim();
                        _line.Clear();
                        if (!long.TryParse(sizeText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _chunkRemaining) || _chunkRemaining < 0)
                        {
                            _state = State.Malformed;
                        }
                        else
                        {
                            _state = _chunkRemaining == 0 ? State.Trailer : State.ChunkData;
                        }
                    }
                    break;

                case State.ChunkData:
                {
                    var take = (int)Math.Min(data.Length - i, _chunkRemaining);
                    AddBody(data.Slice(i, take));
                    i += take;
                    _chunkRemaining -= take;
                    if (_chunkRemaining == 0)
                    {
                        _chunkEndRemaining = 2; // the CRLF after the chunk data
                        _state = State.ChunkDataEnd;
                    }
                    break;
                }

                case State.ChunkDataEnd:
                    i++;
                    if (--_chunkEndRemaining == 0)
                    {
                        _state = State.ChunkSize;
                    }
                    break;

                case State.Trailer:
                    // trailer header lines (usually none), ending with an empty line
                    _line.Add(data[i++]);
                    if (EndsWithCrlf(_line))
                    {
                        _state = _line.Count == 2 ? State.Done : State.Trailer;
                        _line.Clear();
                    }
                    break;
            }
        }
    }

    private void AddBody(ReadOnlySpan<byte> body)
    {
        _sha256.AppendData(body);
        BodyLength += body.Length;
    }

    private void ParseHeaders(string headers)
    {
        var lines = headers.Split("\r\n");
        var status = lines[0].Split(' ');
        if (status.Length < 2 || !status[0].StartsWith("HTTP/", StringComparison.Ordinal) || !int.TryParse(status[1], out var code))
        {
            _state = State.Malformed;
            return;
        }
        StatusCode = code;

        foreach (var line in lines.Skip(1))
        {
            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }
            var name = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) && long.TryParse(value, out var length))
            {
                ContentLength = length;
            }
            else if (name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase) && value.Contains("chunked", StringComparison.OrdinalIgnoreCase))
            {
                Chunked = true;
            }
        }

        if (Chunked)
        {
            ContentLength = null; // chunked takes precedence (RFC 9112 6.3)
            _state = State.ChunkSize;
        }
        else if (ContentLength == 0 || StatusCode is 204 or 304 || StatusCode / 100 == 1)
        {
            _state = State.Done;
        }
        else
        {
            _state = State.Body;
        }
    }

    private static bool EndsWithCrlf(List<byte> line) =>
        line.Count >= 2 && line[^2] == '\r' && line[^1] == '\n';

    private static bool EndsWithBlankLine(List<byte> line) =>
        line.Count >= 4 && line[^4] == '\r' && line[^3] == '\n' && line[^2] == '\r' && line[^1] == '\n';

    public void Dispose() => _sha256.Dispose();
}
