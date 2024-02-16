using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AssettoServer.Utils;


/// <summary>
/// Author https://github.com/TheYakuzo
/// </summary>
public class BandwidthLimitedStream : Stream
{
    private readonly Stream _baseStream;
    private readonly long _bytesPerSecond;
    private long _bytesSent;
    private DateTime _startTime;
    
    public BandwidthLimitedStream(Stream baseStream, long bytesPerSecond)
    {
        _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
        _bytesPerSecond = bytesPerSecond;
        _startTime = DateTime.UtcNow;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (DateTime.UtcNow - _startTime > TimeSpan.FromSeconds(1))
        {
            _bytesSent = 0;
            _startTime = DateTime.UtcNow;
        }

        if (_bytesSent >= _bytesPerSecond)
        {
            int waitTime = (int)(1000 - (DateTime.UtcNow - _startTime).TotalMilliseconds);
            if (waitTime > 0)
            {
                await Task.Delay(waitTime, cancellationToken);
            }
            _bytesSent = 0;
            _startTime = DateTime.UtcNow;
        }

        int toRead = Math.Min(count, (int)(_bytesPerSecond - _bytesSent));
        int read = await _baseStream.ReadAsync(buffer, offset, toRead, cancellationToken);

        _bytesSent += read;
        return read;
    }

    public override void Flush()
    {
        _baseStream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return _baseStream.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return _baseStream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        _baseStream.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _baseStream.Write(buffer, offset, count);
    }

    public override bool CanRead => _baseStream.CanRead;

    public override bool CanSeek => _baseStream.CanSeek;

    public override bool CanWrite => _baseStream.CanWrite;

    public override long Length => _baseStream.Length;

    public override long Position
    {
        get => _baseStream.Position;
        set => _baseStream.Position = value;
    }
}
