using System;
using System.IO;
using SharpCompress.Common.SevenZip;
using SharpCompress.Compressors.LZMA.Utilites;
using SharpCompress.IO;

namespace SharpCompress.Compressors.LZMA;

internal abstract class DecoderStream2 : Stream
{
    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override void Flush() { }

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}

internal static class DecoderStreamHelper
{
    private static int FindCoderIndexForOutStreamIndex(CFolder folderInfo, int outStreamIndex)
    {
        for (var coderIndex = 0; coderIndex < folderInfo._coders.Count; coderIndex++)
        {
            var coderInfo = folderInfo._coders[coderIndex];
            outStreamIndex -= coderInfo._numOutStreams;
            if (outStreamIndex < 0)
            {
                return coderIndex;
            }
        }

        throw new InvalidOperationException("Could not link output stream to coder.");
    }

    private static Stream CreateDecoderStream(
        Stream[] packStreams,
        long[] packSizes,
        Stream[] outStreams,
        CFolder folderInfo,
        int coderIndex,
        IPasswordProvider pass
    )
    {
        var coderInfo = folderInfo._coders[coderIndex];
        if (coderInfo._numOutStreams != 1)
        {
            throw new NotSupportedException("Multiple output streams are not supported.");
        }

        var inStreamId = 0;
        for (var i = 0; i < coderIndex; i++)
        {
            inStreamId += folderInfo._coders[i]._numInStreams;
        }

        var outStreamId = 0;
        for (var i = 0; i < coderIndex; i++)
        {
            outStreamId += folderInfo._coders[i]._numOutStreams;
        }

        var inStreams = new Stream[coderInfo._numInStreams];

        for (var i = 0; i < inStreams.Length; i++, inStreamId++)
        {
            var bindPairIndex = folderInfo.FindBindPairForInStream(inStreamId);
            if (bindPairIndex >= 0)
            {
                var pairedOutIndex = folderInfo._bindPairs[bindPairIndex]._outIndex;

                if (outStreams[pairedOutIndex] != null)
                {
                    throw new NotSupportedException(
                        "Overlapping stream bindings are not supported."
                    );
                }

                var otherCoderIndex = FindCoderIndexForOutStreamIndex(folderInfo, pairedOutIndex);
                inStreams[i] = CreateDecoderStream(
                    packStreams,
                    packSizes,
                    outStreams,
                    folderInfo,
                    otherCoderIndex,
                    pass
                );

                //inStreamSizes[i] = folderInfo.UnpackSizes[pairedOutIndex];

                if (outStreams[pairedOutIndex] != null)
                {
                    throw new NotSupportedException(
                        "Overlapping stream bindings are not supported."
                    );
                }

                outStreams[pairedOutIndex] = inStreams[i];
            }
            else
            {
                var index = folderInfo.FindPackStreamArrayIndex(inStreamId);
                if (index < 0)
                {
                    throw new NotSupportedException("Could not find input stream binding.");
                }

                inStreams[i] = packStreams[index];

                //inStreamSizes[i] = packSizes[index];
            }
        }

        var unpackSize = folderInfo._unpackSizes[outStreamId];
        return DecoderRegistry.CreateDecoderStream(
            coderInfo._methodId,
            inStreams,
            coderInfo._props,
            pass,
            unpackSize
        );
    }
}
