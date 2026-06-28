namespace RcloneCrypt.Core;

/// <summary>
/// PKCS#7 padding, matching rclone's backend/crypt/pkcs7 implementation.
/// </summary>
internal static class Pkcs7
{
    public static byte[] Pad(int blockSize, byte[] buf)
    {
        if (blockSize <= 1 || blockSize >= 256)
            throw new ArgumentOutOfRangeException(nameof(blockSize), "blockSize must be in (1, 256)");

        int padding = blockSize - (buf.Length % blockSize);
        var result = new byte[buf.Length + padding];
        Array.Copy(buf, result, buf.Length);
        for (int i = buf.Length; i < result.Length; i++)
            result[i] = (byte)padding;
        return result;
    }

    public static byte[] Unpad(int blockSize, byte[] buf)
    {
        if (blockSize <= 1 || blockSize >= 256)
            throw new ArgumentOutOfRangeException(nameof(blockSize), "blockSize must be in (1, 256)");
        int length = buf.Length;
        if (length == 0)
            throw new CryptException("PKCS#7: data is empty");
        if (length % blockSize != 0)
            throw new CryptException("PKCS#7: data is not a multiple of the block size");

        int padding = buf[length - 1];
        if (padding == 0 || padding > blockSize)
            throw new CryptException("PKCS#7: invalid padding");
        for (int i = 0; i < padding; i++)
        {
            if (buf[length - 1 - i] != (byte)padding)
                throw new CryptException("PKCS#7: invalid padding");
        }
        var result = new byte[length - padding];
        Array.Copy(buf, result, result.Length);
        return result;
    }
}
