using DiscUtils.SquashFs;
using System;
using System.IO;

namespace _86BoxManager.Linux;

public class AppImageChecker
{
    private static readonly byte[] Type2MagicNumber = { 0x41, 0x49, 0x02 };
    private static readonly byte[] Type2SqshNumber = { 0x68, 0x73, 0x71, 0x73 }; // "sqsh" in little-endian

    public static bool TryGetAppInfo(string path, out AppImageInfo info)
    {
        using (var fs = File.OpenRead(path))
            return TryGetAppInfo(fs, out info);
    }

    public static bool TryGetAppInfo(Stream s, out AppImageInfo info)
    {
        string exe_name = null, version = null, arch = null;
        SquashFileSystemReader squash;

        TryOpenAppImage(s, out squash);

        if (squash != null) 
        {
            foreach (var name in squash.GetFiles(""))
            {
                if (name.EndsWith("86Box.desktop", StringComparison.InvariantCultureIgnoreCase))
                {
                    var file = squash.GetFileInfo(name);

                    using (var reader = file.OpenRead())
                    {
                        using (var sr = new StreamReader(reader, System.Text.Encoding.UTF8))
                        {
                            while (true)
                            {
                                var line = sr.ReadLine();

                                if (line != null)
                                {
                                    if (line.StartsWith("X-AppImage-Arch"))
                                    {
                                        var split = line.Split('=');
                                        if (split.Length == 2)
                                            arch = split[1];
                                    }
                                    if (line.StartsWith("X-AppImage-Version"))
                                    {
                                        var split = line.Split('=');
                                        if (split.Length == 2)
                                            version = split[1];
                                    }
                                    if (line.StartsWith("X-AppImage-Name"))
                                    {
                                        var split = line.Split('=');
                                        if (split.Length == 2)
                                            exe_name = split[1];
                                    }
                                }
                                else
                                {
                                    break;
                                }
                                
                            }
                        }
                    }

                    if (version != null)
                        break;
                }
            }
        }

        info = new AppImageInfo(arch, exe_name, version);

        return squash != null;
    }

    public static bool TryOpenAppImage(Stream stream, out SquashFileSystemReader squash)
    {
        var (buffer, position) = IsAppImageType2(stream);
        squash = null;

        if (position == -1)            
            return false;

        (buffer, position) = FindSquashFsOffset(buffer, position, stream);

        if (position == -1)
            return false;

        //Checks if it's the actual file system
        var bs = new BufferStream(buffer, stream);
        var os = new OffsetStream(bs, position);

        int tries = 3;

        while (tries-- > 0)
        {
            try
            {
                squash = new SquashFileSystemReader(os);

                return true;
            }
            catch (IOException)
            { }

            (buffer, position) = FindSquashFsOffset(buffer, position + 4, stream);
            if (position == -1)
                return false;

            bs = new BufferStream(buffer, stream);
            os = new OffsetStream(bs, position);
        }

        return squash != null;
    }

    public static (byte[] buffer, int position) IsAppImageType2(Stream appImageStream)
    {
        return FindMagicNumber(appImageStream, Type2MagicNumber);
    }

    public static (byte[] buffer, int position) FindSquashFsOffset(byte[] data, int pos, Stream source)
    {
        return FindMagicNumber(source, Type2SqshNumber, data, pos);
    }

    public static (byte[] buffer, int position) FindMagicNumber(Stream stream, byte[] magicNumber, byte[] buffer = null, int pos = 0)
    {
        const int NUM_TRIES = 32;
        const int bufferSize = 32 * 1024; // 32 KB
        int totalBytesRead, bytesRead;

        if (buffer == null)
        {
            buffer = new byte[bufferSize];
            totalBytesRead = 0;
            bytesRead = 0;
        }
        else
        {
            bytesRead = buffer.Length;
            totalBytesRead = bytesRead;

            // Expand the buffer if needed
            if (pos >= buffer.Length - magicNumber.Length)
            {
                Array.Resize(ref buffer, pos + bufferSize);
            }
        }

        while (totalBytesRead < bufferSize * NUM_TRIES)
        {
            if (totalBytesRead < buffer.Length)
            {
                bytesRead = stream.Read(buffer, totalBytesRead, bufferSize);
                if (bytesRead == 0)
                {
                    break; // End of stream
                }

                totalBytesRead += bytesRead;
            }

            // Search for the magic number in the current buffer
            int position = SearchMagicNumber(buffer, totalBytesRead, magicNumber, pos);
            if (position != -1)
            {
                // Magic number found, truncate the buffer to the actual data read
                Array.Resize(ref buffer, totalBytesRead);
                return (buffer, position);
            }

            pos = buffer.Length - magicNumber.Length;
            Array.Resize(ref buffer, totalBytesRead + bufferSize);            
        }

        // Magic number not found, truncate the buffer to the actual data read
        Array.Resize(ref buffer, totalBytesRead);
        return (buffer, -1);
    }

    private static int SearchMagicNumber(byte[] buffer, int length, byte[] magicNumber, int start_pos)
    {
        for (int i = start_pos; i <= length - magicNumber.Length; i++)
        {
            bool found = true;
            for (int j = 0; j < magicNumber.Length; j++)
            {
                if (buffer[i + j] != magicNumber[j])
                {
                    found = false;
                    break;
                }
            }
            if (found)
            {
                return i;
            }
        }
        return -1;
    }
}
