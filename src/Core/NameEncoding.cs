using System.Buffers.Text;
using System.Text;

namespace RcloneCrypt.Core;

/// <summary>
/// The set of filename encodings rclone's crypt backend supports for turning the
/// raw encrypted name bytes into a filesystem-safe string.
/// </summary>
public enum FilenameEncoding
{
    /// <summary>Lower-case, unpadded base32hex (rclone's "base32", the default).</summary>
    Base32,

    /// <summary>URL-safe, unpadded base64 (rclone's "base64").</summary>
    Base64
}

public static class NameEncoding
{
    public static FilenameEncoding Parse(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "base32" => FilenameEncoding.Base32,
            "base64" => FilenameEncoding.Base64,
            _ => throw new CryptException(
                $"unknown filename encoding '{value.Trim()}' (supported: base32, base64)")
        };
    }

    public static string Encode(FilenameEncoding encoding, byte[] data) => encoding switch
    {
        FilenameEncoding.Base32 => Base32Hex.EncodeLowerNoPad(data),
        FilenameEncoding.Base64 => Base64UrlEncodeNoPad(data),
        _ => throw new ArgumentOutOfRangeException(nameof(encoding))
    };

    public static byte[] Decode(FilenameEncoding encoding, string text) => encoding switch
    {
        FilenameEncoding.Base32 => Base32Hex.DecodeLowerNoPad(text),
        FilenameEncoding.Base64 => Base64UrlDecodeNoPad(text),
        _ => throw new ArgumentOutOfRangeException(nameof(encoding))
    };

    private static string Base64UrlEncodeNoPad(byte[] data) => Base64Url.EncodeToString(data);

    private static byte[] Base64UrlDecodeNoPad(string text)
    {
        try
        {
            return Base64Url.DecodeFromChars(text);
        }
        catch (FormatException ex)
        {
            throw new CryptException("bad base64 filename encoding", ex);
        }
    }
}

/// <summary>
/// base32hex (RFC 4648 "Extended Hex" alphabet) with rclone's tweaks: output is
/// lower-cased and the '=' padding is stripped. Matches
/// crypt.caseInsensitiveBase32Encoding.
/// </summary>
internal static class Base32Hex
{
    private const string Alphabet = "0123456789abcdefghijklmnopqrstuv";

    public static string EncodeLowerNoPad(byte[] data)
    {
        if (data.Length == 0)
            return string.Empty;

        var sb = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0;
        int bitsLeft = 0;
        foreach (byte b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                int index = (buffer >> bitsLeft) & 0x1F;
                sb.Append(Alphabet[index]);
            }
        }
        if (bitsLeft > 0)
        {
            int index = (buffer << (5 - bitsLeft)) & 0x1F;
            sb.Append(Alphabet[index]);
        }
        return sb.ToString();
    }

    public static byte[] DecodeLowerNoPad(string text)
    {
        if (text.Length == 0)
            return [];
        if (text.EndsWith('='))
            throw new CryptException("bad base32 filename encoding");

        var output = new List<byte>(text.Length * 5 / 8);
        int buffer = 0;
        int bitsLeft = 0;
        foreach (char rawCh in text)
        {
            char ch = char.ToLowerInvariant(rawCh);
            int val = Alphabet.IndexOf(ch);
            if (val < 0)
                throw new CryptException("bad base32 filename encoding");
            buffer = (buffer << 5) | val;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                output.Add((byte)((buffer >> bitsLeft) & 0xFF));
            }
        }
        // Reject non-canonical encodings: any leftover bits must be zero, matching
        // Go's base32 decoder (which rclone uses).
        if (bitsLeft > 0 && (buffer & ((1 << bitsLeft) - 1)) != 0)
            throw new CryptException("bad base32 filename encoding");
        return output.ToArray();
    }
}
