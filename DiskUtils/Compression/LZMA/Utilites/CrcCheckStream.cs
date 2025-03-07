using System.Diagnostics;

namespace SharpCompress.Compressors.LZMA.Utilites;

public class CrcCheckStream : Stream
{
    private readonly uint _mExpectedCrc;
    private uint _mCurrentCrc;
    private bool _mClosed;

    private readonly long[] _mBytes = new long[256];
    private long _mLength;

    public CrcCheckStream(uint crc)
    {
        _mExpectedCrc = crc;
        _mCurrentCrc = Crc.INIT_CRC;
    }

    protected override void Dispose(bool disposing)
    {
        //Nanook - is not equal here - _mCurrentCrc is yet to be negated
        //if (_mCurrentCrc != _mExpectedCrc)
        //{
        //    throw new InvalidOperationException();
        //}
        try
        {
            if (disposing && !_mClosed)
            {
                _mClosed = true;
                _mCurrentCrc = Crc.Finish(_mCurrentCrc); //now becomes equal
#if DEBUG
                if (_mCurrentCrc == _mExpectedCrc)
                {
                    Debug.WriteLine("CRC ok: " + _mExpectedCrc.ToString("x8"));
                }
                else
                {
                    Debugger.Break();
                    Debug.WriteLine("bad CRC");
                }

                var lengthInv = 1.0 / _mLength;
                double entropy = 0;
                for (var i = 0; i < 256; i++)
                {
                    if (_mBytes[i] != 0)
                    {
                        var p = lengthInv * _mBytes[i];
                        entropy -= p * Math.Log(p, 256);
                    }
                }
                Debug.WriteLine("entropy: " + (int)(entropy * 100) + "%");
#endif
                if (_mCurrentCrc != _mExpectedCrc) //moved test to here
                {
                    throw new InvalidOperationException();
                }
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override void Flush() { }

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new InvalidOperationException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        _mLength += count;
        for (var i = 0; i < count; i++)
        {
            _mBytes[buffer[offset + i]]++;
        }

        _mCurrentCrc = Crc.Update(_mCurrentCrc, buffer, offset, count);
    }
}
