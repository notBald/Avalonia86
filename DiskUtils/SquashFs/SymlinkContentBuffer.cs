using DiscUtils.SquashFs;
using DiscUtils.Streams;
using System.Text;

namespace DiskUtils.SquashFs
{

    internal class SymlinkContentBuffer : IBuffer
    {
        private readonly SymlinkInode _inode;
        private readonly byte[] _contents;

        public SymlinkContentBuffer(Context context, SymlinkInode inode, MetadataRef inodeRef)
        {
            _inode = inode;

            context.InodeReader.SetPosition(inodeRef);
            context.InodeReader.Skip(inode.Size);
            _contents = new byte[inode.SymlinkSize];
            if (context.InodeReader.Read(_contents, 0, _contents.Length) != _contents.Length)
                throw new IOException("Unable to read Inode type");
        }

        public string TargetPath { get => Encoding.UTF8.GetString(_contents, 0, _contents.Length); }

        public bool CanRead
        {
            get { return true; }
        }

        public bool CanWrite
        {
            get { return false; }
        }

        public long Capacity
        {
            get { return _inode.SymlinkSize; }
        }

        public IEnumerable<StreamExtent> Extents
        {
            get { return [new StreamExtent(0, Capacity)]; }
        }
        public int Read(long pos, byte[] buffer, int offset, int count)
        {
            Buffer.BlockCopy(_contents, (int) pos, buffer, offset, count);

            return count;
        }

        public void Write(long pos, byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public void Clear(long pos, int count)
        {
            throw new NotSupportedException();
        }

        public void Flush() { }

        public void SetCapacity(long value)
        {
            throw new NotSupportedException();
        }

        public IEnumerable<StreamExtent> GetExtentsInRange(long start, long count)
        {
            return StreamExtent.Intersect(Extents, new StreamExtent(start, count));
        }
    }
}
