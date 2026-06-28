using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;

namespace RcloneCrypt.Core;

/// <summary>
/// NaCl crypto_secretbox (XSalsa20 + Poly1305), the construction rclone uses for
/// file <em>content</em>. Output layout matches golang.org/x/crypto/nacl/secretbox:
/// a 16-byte Poly1305 tag followed by the ciphertext.
/// </summary>
internal static class SecretBox
{
    public const int Overhead = 16;
    private const int KeySize = 32;
    private const int NonceSize = 24;

    public static byte[] Seal(ReadOnlySpan<byte> message, byte[] nonce, byte[] key)
    {
        var cipher = new XSalsa20Engine();
        cipher.Init(true, new ParametersWithIV(new KeyParameter(key), nonce));

        // First 32 bytes of the keystream form the one-time Poly1305 key.
        var subKey = new byte[KeySize];
        cipher.ProcessBytes(new byte[KeySize], 0, KeySize, subKey, 0);

        var output = new byte[Overhead + message.Length];
        var cipherText = output.AsSpan(Overhead);
        cipher.ProcessBytes(message.ToArray(), 0, message.Length, output, Overhead);

        var mac = new Poly1305();
        mac.Init(new KeyParameter(subKey));
        mac.BlockUpdate(cipherText.ToArray(), 0, cipherText.Length);
        mac.DoFinal(output, 0);
        return output;
    }

    public static byte[] Open(ReadOnlySpan<byte> box, byte[] nonce, byte[] key)
    {
        if (box.Length < Overhead)
            throw new CryptException("encrypted block is too short");

        var tag = box[..Overhead];
        var cipherText = box[Overhead..].ToArray();

        var cipher = new XSalsa20Engine();
        cipher.Init(false, new ParametersWithIV(new KeyParameter(key), nonce));

        var subKey = new byte[KeySize];
        cipher.ProcessBytes(new byte[KeySize], 0, KeySize, subKey, 0);

        var mac = new Poly1305();
        mac.Init(new KeyParameter(subKey));
        mac.BlockUpdate(cipherText, 0, cipherText.Length);
        var expected = new byte[Overhead];
        mac.DoFinal(expected, 0);

        if (!ConstantTimeEquals(tag, expected))
            throw new CryptException("failed to authenticate decrypted block - bad password?");

        var plain = new byte[cipherText.Length];
        cipher.ProcessBytes(cipherText, 0, cipherText.Length, plain, 0);
        return plain;
    }

    private static bool ConstantTimeEquals(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        if (a.Length != b.Length)
            return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++)
            diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
