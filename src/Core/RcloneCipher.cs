using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.Generators;

namespace RcloneCrypt.Core;

/// <summary>
/// A faithful port of rclone's <c>backend/crypt</c> cipher: scrypt key
/// derivation, NaCl secretbox for file contents and AES-EME for file names.
/// Output is byte-for-byte compatible with rclone using name encryption mode
/// "standard".
/// </summary>
public sealed class RcloneCipher : IDisposable
{
    // rclone constants (backend/crypt/cipher.go)
    private static readonly byte[] FileMagic = "RCLONE\x00\x00"u8.ToArray();
    private const int FileNonceSize = 24;
    private const int FileHeaderSize = 8 + FileNonceSize; // magic + nonce
    private const int BlockDataSize = 64 * 1024;
    private const int BlockSize = SecretBox.Overhead + BlockDataSize;
    private const int NameCipherBlockSize = 16;

    private static readonly byte[] DefaultSalt =
    [
        0xA8, 0x0D, 0xF4, 0x3A, 0x8F, 0xBD, 0x03, 0x08,
        0xA7, 0xCA, 0xB8, 0x3E, 0x58, 0x1F, 0x86, 0xB1
    ];

    private readonly byte[] _dataKey = new byte[32];
    private readonly byte[] _nameKey = new byte[32];
    private readonly byte[] _nameTweak = new byte[16];
    private readonly Aes _nameCipher;
    private readonly FilenameEncoding _encoding;

    public RcloneCipher(string password, string? salt, FilenameEncoding encoding)
    {
        _encoding = encoding;

        const int keySize = 32 + 32 + 16;
        byte[] key;
        if (password.Length == 0)
        {
            // rclone: empty password yields all-zero keys (used by its tests).
            key = new byte[keySize];
        }
        else
        {
            byte[] saltBytes = string.IsNullOrEmpty(salt)
                ? DefaultSalt
                : System.Text.Encoding.UTF8.GetBytes(salt);
            key = SCrypt.Generate(
                System.Text.Encoding.UTF8.GetBytes(password),
                saltBytes,
                16384, 8, 1, keySize);
        }

        Array.Copy(key, 0, _dataKey, 0, 32);
        Array.Copy(key, 32, _nameKey, 0, 32);
        Array.Copy(key, 64, _nameTweak, 0, 16);

        _nameCipher = Aes.Create();
        _nameCipher.Key = _nameKey;
        _nameCipher.Mode = CipherMode.ECB;
        _nameCipher.Padding = PaddingMode.None;
    }

    public FilenameEncoding Encoding => _encoding;

    // ----- File name encryption -----

    /// <summary>Encrypts a single path segment (no '/').</summary>
    public string EncryptName(string plaintext)
    {
        if (plaintext.Length == 0)
            return string.Empty;
        byte[] padded = Pkcs7.Pad(NameCipherBlockSize, System.Text.Encoding.UTF8.GetBytes(plaintext));
        if (padded.Length > 2048)
            throw new CryptException("filename is too long to encrypt (max 2048 bytes after padding)");
        byte[] ciphertext = Eme.Transform(_nameCipher, _nameTweak, padded, Eme.Direction.Encrypt);
        return NameEncoding.Encode(_encoding, ciphertext);
    }

    /// <summary>Decrypts a single path segment produced by <see cref="EncryptName"/>.</summary>
    public string DecryptName(string ciphertext)
    {
        if (ciphertext.Length == 0)
            return string.Empty;
        byte[] raw = NameEncoding.Decode(_encoding, ciphertext);
        if (raw.Length == 0)
            throw new CryptException("filename too short after decode");
        if (raw.Length % NameCipherBlockSize != 0)
            throw new CryptException("filename is not a multiple of the block size");
        if (raw.Length > 2048)
            throw new CryptException("filename too long after decode");
        byte[] padded = Eme.Transform(_nameCipher, _nameTweak, raw, Eme.Direction.Decrypt);
        byte[] plaintext = Pkcs7.Unpad(NameCipherBlockSize, padded);
        return System.Text.Encoding.UTF8.GetString(plaintext);
    }

    // ----- File content encryption -----

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> into the rclone crypt container format.
    /// A random nonce is generated unless <paramref name="nonce"/> is supplied
    /// (the override exists for deterministic tests).
    /// </summary>
    public byte[] EncryptData(ReadOnlySpan<byte> plaintext, byte[]? nonce = null)
    {
        var nonceBytes = new byte[FileNonceSize];
        if (nonce is null)
            RandomNumberGenerator.Fill(nonceBytes);
        else if (nonce.Length != FileNonceSize)
            throw new ArgumentException($"nonce must be {FileNonceSize} bytes", nameof(nonce));
        else
            Array.Copy(nonce, nonceBytes, FileNonceSize);

        using var output = new MemoryStream();
        output.Write(FileMagic);
        output.Write(nonceBytes);

        var current = (byte[])nonceBytes.Clone();
        int offset = 0;
        // Always run at least once so empty files still get a (zero-length) block,
        // matching rclone which writes only the header for empty input.
        while (offset < plaintext.Length)
        {
            int len = Math.Min(BlockDataSize, plaintext.Length - offset);
            byte[] block = SecretBox.Seal(plaintext.Slice(offset, len), current, _dataKey);
            output.Write(block);
            Increment(current);
            offset += len;
        }

        return output.ToArray();
    }

    /// <summary>Decrypts an rclone crypt container produced by <see cref="EncryptData"/> or rclone itself.</summary>
    public byte[] DecryptData(ReadOnlySpan<byte> ciphertext)
    {
        if (ciphertext.Length < FileHeaderSize)
            throw new CryptException("file is too short to be encrypted");
        if (!ciphertext[..8].SequenceEqual(FileMagic))
            throw new CryptException("not an encrypted file - bad magic string");

        var nonce = ciphertext.Slice(8, FileNonceSize).ToArray();
        using var output = new MemoryStream();
        int offset = FileHeaderSize;
        while (offset < ciphertext.Length)
        {
            int len = Math.Min(BlockSize, ciphertext.Length - offset);
            if (len <= SecretBox.Overhead)
                throw new CryptException("file has truncated block");
            byte[] plain = SecretBox.Open(ciphertext.Slice(offset, len), nonce, _dataKey);
            output.Write(plain);
            Increment(nonce);
            offset += len;
        }
        return output.ToArray();
    }

    // Little-endian +1 across the 24-byte nonce, matching rclone's nonce.increment.
    private static void Increment(byte[] nonce)
    {
        for (int i = 0; i < nonce.Length; i++)
        {
            byte digit = nonce[i];
            byte newDigit = (byte)(digit + 1);
            nonce[i] = newDigit;
            if (newDigit >= digit)
                break;
        }
    }

    public void Dispose() => _nameCipher.Dispose();
}
