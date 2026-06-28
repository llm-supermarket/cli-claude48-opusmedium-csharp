using System.Security.Cryptography;

namespace RcloneCrypt.Core;

/// <summary>
/// EME (ECB-Mix-ECB) wide-block encryption mode by Halevi and Rogaway, ported
/// from github.com/rfjakob/eme which rclone uses for filename encryption.
/// </summary>
internal static class Eme
{
    private const int BlockSize = 16;

    public enum Direction
    {
        Encrypt,
        Decrypt
    }

    // GF(2^128) multiply by two, per the EME-32 draft "multByTwo" procedure.
    private static void MultByTwo(byte[] outBuf, byte[] inBuf)
    {
        var tmp = new byte[BlockSize];
        tmp[0] = (byte)(2 * inBuf[0]);
        tmp[0] ^= (byte)(135 & (byte)-(inBuf[15] >> 7));
        for (int j = 1; j < BlockSize; j++)
        {
            tmp[j] = (byte)(2 * inBuf[j]);
            tmp[j] += (byte)(inBuf[j - 1] >> 7);
        }
        Array.Copy(tmp, outBuf, BlockSize);
    }

    private static void XorBlocks(byte[] outBuf, ReadOnlySpan<byte> in1, ReadOnlySpan<byte> in2)
    {
        for (int i = 0; i < in1.Length; i++)
            outBuf[i] = (byte)(in1[i] ^ in2[i]);
    }

    private static void AesTransform(Aes aes, Span<byte> dst, ReadOnlySpan<byte> src, Direction direction)
    {
        if (direction == Direction.Encrypt)
            aes.EncryptEcb(src, dst, PaddingMode.None);
        else
            aes.DecryptEcb(src, dst, PaddingMode.None);
    }

    private static byte[][] TabulateL(Aes aes, int m)
    {
        // L0 = 2 * AESenc(K; 0)
        var li = new byte[BlockSize];
        aes.EncryptEcb(new byte[BlockSize], li, PaddingMode.None);

        var table = new byte[m][];
        for (int i = 0; i < m; i++)
        {
            MultByTwo(li, li);
            table[i] = (byte[])li.Clone();
        }
        return table;
    }

    public static byte[] Transform(Aes aes, byte[] tweak, byte[] inputData, Direction direction)
    {
        if (tweak.Length != BlockSize)
            throw new ArgumentException("Tweak must be 16 bytes long", nameof(tweak));
        if (inputData.Length % BlockSize != 0)
            throw new ArgumentException("Data must be a multiple of 16 bytes long", nameof(inputData));

        int m = inputData.Length / BlockSize;
        if (m == 0 || m > 16 * 8)
            throw new ArgumentException($"EME operates on 1 to {16 * 8} blocks, got {m}", nameof(inputData));

        byte[] t = tweak;
        byte[] p = inputData;
        var c = new byte[p.Length];

        byte[][] lTable = TabulateL(aes, m);

        var ppj = new byte[BlockSize];
        for (int j = 0; j < m; j++)
        {
            XorBlocks(ppj, p.AsSpan(j * BlockSize, BlockSize), lTable[j]);
            AesTransform(aes, c.AsSpan(j * BlockSize, BlockSize), ppj, direction);
        }

        var mp = new byte[BlockSize];
        XorBlocks(mp, c.AsSpan(0, BlockSize), t);
        for (int j = 1; j < m; j++)
            XorBlocks(mp, mp, c.AsSpan(j * BlockSize, BlockSize));

        var mc = new byte[BlockSize];
        AesTransform(aes, mc, mp, direction);

        var mBlock = new byte[BlockSize];
        XorBlocks(mBlock, mp, mc);
        var cccj = new byte[BlockSize];
        for (int j = 1; j < m; j++)
        {
            MultByTwo(mBlock, mBlock);
            XorBlocks(cccj, c.AsSpan(j * BlockSize, BlockSize), mBlock);
            Array.Copy(cccj, 0, c, j * BlockSize, BlockSize);
        }

        var ccc1 = new byte[BlockSize];
        XorBlocks(ccc1, mc, t);
        for (int j = 1; j < m; j++)
            XorBlocks(ccc1, ccc1, c.AsSpan(j * BlockSize, BlockSize));
        Array.Copy(ccc1, 0, c, 0, BlockSize);

        for (int j = 0; j < m; j++)
        {
            AesTransform(aes, c.AsSpan(j * BlockSize, BlockSize), c.AsSpan(j * BlockSize, BlockSize), direction);
            XorBlocks2(c, j * BlockSize, lTable[j]);
        }

        return c;
    }

    private static void XorBlocks2(byte[] buf, int offset, byte[] other)
    {
        for (int i = 0; i < BlockSize; i++)
            buf[offset + i] ^= other[i];
    }
}
