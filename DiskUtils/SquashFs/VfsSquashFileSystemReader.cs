﻿//
// Copyright (c) 2008-2011, Kenneth Bell
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
//

using DiscUtils.Streams;
using DiscUtils.Vfs;

#pragma warning disable CS8600
#pragma warning disable CS8602
#pragma warning disable CS8604
namespace DiscUtils.SquashFs
{
    internal class VfsSquashFileSystemReader : VfsReadOnlyFileSystem<DirectoryEntry, File, Directory, Context>,
                                               IUnixFileSystem
    {
        public const int MetadataBufferSize = 8 * 1024;
        private readonly BlockCache<Block> _blockCache;

        private readonly Context _context;
        private byte[]? _ioBuffer;
        private readonly BlockCache<Metablock> _metablockCache;

        public VfsSquashFileSystemReader(Stream stream)
            : base(new DiscFileSystemOptions())
        {
            _context = new Context();
            _context.SuperBlock = new SuperBlock();
            _context.RawStream = stream;

            // Read superblock
            stream.Position = 0;
            byte[] buffer = StreamUtilities.ReadExact(stream, _context.SuperBlock.Size);
            _context.SuperBlock.ReadFrom(buffer, 0);

            if (_context.SuperBlock.Magic != SuperBlock.SquashFsMagic)
            {
                throw new IOException("Invalid SquashFS filesystem - magic mismatch");
            }

            // 1 = gzip
            // 2 = lzma
            // 3 = lzo
            // 4 = xz
            // 5 = lz4                
            // 6 = zstd               
            if (_context.SuperBlock.Compression < 1 || _context.SuperBlock.Compression > 6)
            {
                throw new IOException("Unsupported compression used");
            }

            if (_context.SuperBlock.ExtendedAttrsTableStart != -1)
            {
                throw new IOException("Unsupported extended attributes present");
            }

            if (_context.SuperBlock.MajorVersion != 4)
            {
                throw new IOException("Unsupported file system version: " + _context.SuperBlock.MajorVersion + "." +
                                      _context.SuperBlock.MinorVersion);
            }

            // Create block caches, used to reduce the amount of I/O and decompression activity.
            _blockCache = new BlockCache<Block>((int)_context.SuperBlock.BlockSize, 20);
            _metablockCache = new BlockCache<Metablock>(MetadataBufferSize, 20);
            _context.ReadBlock = ReadBlock;
            _context.ReadMetaBlock = ReadMetaBlock;

            _context.InodeReader = new MetablockReader(_context, _context.SuperBlock.InodeTableStart);
            _context.DirectoryReader = new MetablockReader(_context, _context.SuperBlock.DirectoryTableStart);

            if (_context.SuperBlock.FragmentTableStart != -1)
            {
                _context.FragmentTableReaders = LoadIndirectReaders(
                    _context.SuperBlock.FragmentTableStart,
                    (int)_context.SuperBlock.FragmentsCount,
                    FragmentRecord.RecordSize);
            }

            if (_context.SuperBlock.UidGidTableStart != -1)
            {
                _context.UidGidTableReaders = LoadIndirectReaders(
                    _context.SuperBlock.UidGidTableStart,
                    _context.SuperBlock.UidGidCount,
                    4);
            }

            // Bootstrap the root directory
            _context.InodeReader.SetPosition(_context.SuperBlock.RootInode);
            DirectoryInode dirInode = (DirectoryInode)Inode.Read(_context.InodeReader);
            RootDirectory = new Directory(_context, dirInode, _context.SuperBlock.RootInode);
        }

        public override string FriendlyName
        {
            get { return "SquashFs"; }
        }

        public override string VolumeLabel
        {
            get { return string.Empty; }
        }

        public UnixFileSystemInfo GetUnixFileInfo(string path)
        {
            File file = GetFile(path);
            Inode inode = file.Inode;
            DeviceInode devInod = inode as DeviceInode;

            UnixFileSystemInfo info = new UnixFileSystemInfo
            {
                FileType = FileTypeFromInodeType(inode.Type),
                UserId = GetId(inode.UidKey),
                GroupId = GetId(inode.GidKey),
                Permissions = (UnixFilePermissions)inode.Mode,
                Inode = inode.InodeNumber,
                LinkCount = inode.NumLinks,
                DeviceId = devInod == null ? 0 : devInod.DeviceId
            };

            return info;
        }

        /// <summary>
        /// Size of the Filesystem in bytes
        /// </summary>
        public override long Size
        {
            get { throw new NotSupportedException("Filesystem size is not (yet) supported"); }
        }

        /// <summary>
        /// Used space of the Filesystem in bytes
        /// </summary>
        public override long UsedSpace
        {
            get { throw new NotSupportedException("Filesystem size is not (yet) supported"); }
        }

        /// <summary>
        /// Available space of the Filesystem in bytes
        /// </summary>
        public override long AvailableSpace
        {
            get { throw new NotSupportedException("Filesystem size is not (yet) supported"); }
        }

        internal static UnixFileType FileTypeFromInodeType(InodeType inodeType)
        {
            switch (inodeType)
            {
                case InodeType.BlockDevice:
                case InodeType.ExtendedBlockDevice:
                    return UnixFileType.Block;
                case InodeType.CharacterDevice:
                case InodeType.ExtendedCharacterDevice:
                    return UnixFileType.Character;
                case InodeType.Directory:
                case InodeType.ExtendedDirectory:
                    return UnixFileType.Directory;
                case InodeType.Fifo:
                case InodeType.ExtendedFifo:
                    return UnixFileType.Fifo;
                case InodeType.File:
                case InodeType.ExtendedFile:
                    return UnixFileType.Regular;
                case InodeType.Socket:
                case InodeType.ExtendedSocket:
                    return UnixFileType.Socket;
                case InodeType.Symlink:
                case InodeType.ExtendedSymlink:
                    return UnixFileType.Link;
                default:
                    throw new NotSupportedException("Unrecognized inode type: " + inodeType);
            }
        }

        protected override File ConvertDirEntryToFile(DirectoryEntry dirEntry)
        {
            MetadataRef inodeRef = dirEntry.InodeReference;
            _context.InodeReader.SetPosition(inodeRef);
            Inode inode = Inode.Read(_context.InodeReader);

            if (dirEntry.IsSymlink)
            {
                return new Symlink(_context, inode, inodeRef);
            }
            if (dirEntry.IsDirectory)
            {
                return new Directory(_context, inode, inodeRef);
            }
            return new File(_context, inode, inodeRef);
        }

        private MetablockReader[] LoadIndirectReaders(long pos, int count, int recordSize)
        {
            _context.RawStream.Position = pos;
            int numBlocks = MathUtilities.Ceil(count * recordSize, MetadataBufferSize);

            byte[] tableBytes = StreamUtilities.ReadExact(_context.RawStream, numBlocks * 8);
            MetablockReader[] result = new MetablockReader[numBlocks];
            for (int i = 0; i < numBlocks; ++i)
            {
                long block = EndianUtilities.ToInt64LittleEndian(tableBytes, i * 8);
                result[i] = new MetablockReader(_context, block);
            }

            return result;
        }

        private int GetId(ushort idKey)
        {
            int recordsPerBlock = MetadataBufferSize / 4;
            int block = idKey / recordsPerBlock;
            int offset = idKey % recordsPerBlock;

            MetablockReader reader = _context.UidGidTableReaders[block];
            reader.SetPosition(0, offset * 4);
            return reader.ReadInt();
        }

        private Block ReadBlock(long pos, int diskLen)
        {
            Block block = _blockCache.GetBlock(pos);
            if (block.Available >= 0)
            {
                return block;
            }

            Stream? stream = _context.RawStream;
            stream.Position = pos;

            int readLen = diskLen & 0x00FFFFFF;
            bool isCompressed = (diskLen & 0x01000000) == 0;

            if (isCompressed)
            {
                if (_ioBuffer == null || readLen > _ioBuffer.Length)
                {
                    _ioBuffer = new byte[readLen];
                }

                StreamUtilities.ReadExact(stream, _ioBuffer, 0, readLen);

                //╔═══╦════════════════════╦════════════════════╗
                //║ # ║ Compression Method ║ Compatible Version ║
                //╠═══╬════════════════════╬════════════════════╣
                //║ 1 ║ gzip               ║ 1.0 and newer      ║
                //║ 2 ║ lzma               ║ 4.1 and newer      ║
                //║ 3 ║ lzo                ║ 4.1 and newer      ║
                //║ 4 ║ xz                 ║ 4.2 and newer      ║
                //║ 5 ║ lz4                ║ 4.3 and newer      ║
                //║ 6 ║ zstd               ║ 4.4 and newer      ║
                //╚═══╩════════════════════╩════════════════════╝
                switch (_context.SuperBlock.Compression)
                {
                    case 1: 
                        using (
                            var zlibStream = new SharpCompress.Compressors.Deflate.ZlibStream(new MemoryStream(_ioBuffer, 0, readLen, false),
                                SharpCompress.Compressors.CompressionMode.Decompress))
                        {
                            block.Available = StreamUtilities.ReadMaximum(zlibStream, block.Data, 0, (int)_context.SuperBlock.BlockSize);
                        }
                        break;
                    case 2: //Lzma
                        var ba = new byte[13];
                        Array.Copy(_ioBuffer, ba, ba.Length);

                        using (var s = new SharpCompress.Compressors.LZMA.LzmaStream(ba, new MemoryStream(_ioBuffer, ba.Length, readLen - ba.Length, false)))
                        {
                            block.Available = StreamUtilities.ReadMaximum(s, block.Data, 0, (int)_context.SuperBlock.BlockSize);
                        }
                        break;

                    case 3:
                        //https://github.com/zivillian/lzo.net (Mit lisenced)
                        throw new NotImplementedException("LZO AppImage compression"); ;

                    case 4: //xz
                        using (var s = new SharpCompress.Compressors.Xz.XZStream(new MemoryStream(_ioBuffer, 0, readLen, false)))
                        {
                            block.Available = StreamUtilities.ReadMaximum(s, block.Data, 0, (int)_context.SuperBlock.BlockSize);
                        }
                        break;
                    case 5:
                        //https://github.com/MiloszKrajewski/lz4net
                        throw new NotImplementedException("LZ4 AppImage compression");
                    case 6:
                        //https://github.com/oleg-st/ZstdSharp/tree/master
                        using (var zstd = new ZstdSharp.DecompressionStream(new MemoryStream(_ioBuffer, 0, readLen, false)))
                        {
                            block.Available = StreamUtilities.ReadMaximum(zstd, block.Data, 0, (int)_context.SuperBlock.BlockSize);
                        }
                        break;
                    default:
                        throw new NotImplementedException("Compression method: " + _context.SuperBlock.Compression);
                }
            }
            else
            {
                StreamUtilities.ReadExact(stream, block.Data, 0, readLen);
                block.Available = readLen;
            }

            return block;
        }

        private Metablock ReadMetaBlock(long pos)
        {
            Metablock block = _metablockCache.GetBlock(pos);
            if (block.Available >= 0)
            {
                return block;
            }

            Stream stream = _context.RawStream;
            stream.Position = pos;

            byte[] buffer = StreamUtilities.ReadExact(stream, 2);

            int readLen = EndianUtilities.ToUInt16LittleEndian(buffer, 0);
            bool isCompressed = (readLen & 0x8000) == 0;
            readLen &= 0x7FFF;
            if (readLen == 0)
            {
                readLen = 0x8000;
            }

            block.NextBlockStart = pos + readLen + 2;

            if (isCompressed)
            {
                if (_ioBuffer == null || readLen > _ioBuffer.Length)
                {
                    _ioBuffer = new byte[readLen];
                }

                StreamUtilities.ReadExact(stream, _ioBuffer, 0, readLen);

                switch(_context.SuperBlock.Compression)
                {
                    case 1: //deflate
                        using (
                            var zlibStream = new SharpCompress.Compressors.Deflate.ZlibStream(new MemoryStream(_ioBuffer, 0, readLen, false),
                                SharpCompress.Compressors.CompressionMode.Decompress))
                        {
                            block.Available = StreamUtilities.ReadMaximum(zlibStream, block.Data, 0, MetadataBufferSize);
                        }
                        break;
                    case 2: //Lzma
                        var ba = new byte[13];
                        Array.Copy(_ioBuffer, ba, ba.Length);

                        using (var s = new SharpCompress.Compressors.LZMA.LzmaStream(ba, new MemoryStream(_ioBuffer, ba.Length, readLen - ba.Length, false)))
                        {
                            block.Available = StreamUtilities.ReadMaximum(s, block.Data, 0, MetadataBufferSize);
                        }
                        break;

                    case 4: //xz
                        using (var s = new SharpCompress.Compressors.Xz.XZStream(new MemoryStream(_ioBuffer, 0, readLen, false)))
                        {
                            block.Available = StreamUtilities.ReadMaximum(s, block.Data, 0, MetadataBufferSize);
                        }
                        break;
                    case 6:
                        using (var zstd = new ZstdSharp.DecompressionStream(new MemoryStream(_ioBuffer, 0, readLen, false)))
                        {
                            block.Available = StreamUtilities.ReadMaximum(zstd, block.Data, 0, MetadataBufferSize);
                        }
                        break;
                    default:
                        throw new NotImplementedException("Compression method: " + _context.SuperBlock.Compression);
                }
            }
            else
            {
                block.Available = StreamUtilities.ReadMaximum(stream, block.Data, 0, readLen);
            }

            return block;
        }
    }
}
