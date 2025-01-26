using System;
using System.IO;

namespace Avalonia86.Linux;

public class BufferStream : Stream
{
    private readonly byte[] _buffer;
    private readonly Stream _stream;
    private long _position;
    private long _streamStartPosition;

    public BufferStream(byte[] buffer, Stream stream)
    {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _position = 0;
        _streamStartPosition = stream.Position;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _buffer.Length + _stream.Length - _streamStartPosition;

    public override long Position
    {
        get => _position;
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Position cannot be negative.");
            _position = value;
            if (_position >= _buffer.Length)
            {
                _stream.Position = _streamStartPosition + (_position - _buffer.Length);
            }
        }
    }

    public override void Flush() => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || count < 0)
            throw new ArgumentOutOfRangeException(offset < 0 ? nameof(offset) : nameof(count), "Offset and count cannot be negative.");
        if (buffer.Length - offset < count)
            throw new ArgumentException("Invalid offset and count.");

        int bytesRead = 0;

        // Read from the buffer
        if (_position < _buffer.Length)
        {
            int bufferBytesToRead = (int)Math.Min(count, _buffer.Length - _position);
            Array.Copy(_buffer, _position, buffer, offset, bufferBytesToRead);
            _position += bufferBytesToRead;
            offset += bufferBytesToRead;
            count -= bufferBytesToRead;
            bytesRead += bufferBytesToRead;
        }

        // Read from the stream
        if (count > 0)
        {
            if (_position >= _buffer.Length)
            {
                _stream.Position = _streamStartPosition + (_position - _buffer.Length);
            }
            int streamBytesRead = _stream.Read(buffer, offset, count);
            _position += streamBytesRead;
            bytesRead += streamBytesRead;
        }

        return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        long newPosition;
        switch (origin)
        {
            case SeekOrigin.Begin:
                newPosition = offset;
                break;
            case SeekOrigin.Current:
                newPosition = _position + offset;
                break;
            case SeekOrigin.End:
                newPosition = Length + offset;
                break;
            default:
                throw new ArgumentException("Invalid seek origin.", nameof(origin));
        }

        if (newPosition < 0)
            throw new IOException("Seek before beginning.");

        Position = newPosition;
        return _position;
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
