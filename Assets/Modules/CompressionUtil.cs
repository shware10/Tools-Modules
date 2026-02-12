using K4os.Compression.LZ4;

public static class CompressionUtil
{
    public static byte[] Compress(byte[] data)
    {
        return LZ4Pickler.Pickle(data);
    }

    public static byte[] Decompress(byte[] data)
    {
        return LZ4Pickler.Unpickle(data);
    }
}
