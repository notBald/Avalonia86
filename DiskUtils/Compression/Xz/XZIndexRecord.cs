namespace SharpCompress.Compressors.Xz;

public class XZIndexRecord
{
    public ulong UnpaddedSize { get; private set; }
    public ulong UncompressedSize { get; private set; }

    protected XZIndexRecord() { }

    public static XZIndexRecord FromBinaryReader(BinaryReader br)
    {
        var record = new XZIndexRecord();
        record.UnpaddedSize = br.ReadXZInteger();
        record.UncompressedSize = br.ReadXZInteger();
        return record;
    }
}
