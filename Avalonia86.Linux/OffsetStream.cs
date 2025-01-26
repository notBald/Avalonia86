using System;
using System.IO;

namespace _86BoxManager.Linux
{
    internal class OffsetStream : Stream
    {
        private readonly Stream baseStream;
        private readonly long offset;

        public OffsetStream(Stream baseStream, long offset)
        {
            this.baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
            this.offset = offset;

            if (!baseStream.CanRead)
            {
                throw new ArgumentException("Base stream must be readable.", nameof(baseStream));
            }

            baseStream.Seek(offset, SeekOrigin.Begin);
        }

        public override bool CanRead => baseStream.CanRead;
        public override bool CanSeek => baseStream.CanSeek;
        public override bool CanWrite => false;
        public override long Length => baseStream.Length - offset;

        public override long Position
        {
            get => baseStream.Position - offset;
            set => baseStream.Position = value + offset;
        }

        public override void Flush() => baseStream.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            return baseStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (origin == SeekOrigin.Begin)
            {
                return baseStream.Seek(offset + this.offset, SeekOrigin.Begin) - this.offset;
            }
            else
            {
                return baseStream.Seek(offset, origin) - this.offset;
            }
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("OffsetStream does not support setting length.");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("OffsetStream is read-only.");
        }
    }
}
