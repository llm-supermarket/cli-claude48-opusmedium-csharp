using System.Text;
using RcloneCrypt.Core;
using Xunit;

namespace RcloneCrypt.Tests;

public class CipherTests
{
    private const string Password = "Testpassword1";

    [Fact]
    public void ContentRoundTrip_NoSalt()
    {
        using var cipher = new RcloneCipher(Password, null, FilenameEncoding.Base32);
        byte[] plain = Encoding.UTF8.GetBytes("hello rclone crypt\nsecond line");
        byte[] encrypted = cipher.EncryptData(plain);
        byte[] decrypted = cipher.DecryptData(encrypted);
        Assert.Equal(plain, decrypted);
    }

    [Fact]
    public void ContentRoundTrip_WithSalt()
    {
        using var cipher = new RcloneCipher(Password, "my-custom-salt", FilenameEncoding.Base32);
        byte[] plain = Encoding.UTF8.GetBytes("salted payload");
        byte[] encrypted = cipher.EncryptData(plain);
        byte[] decrypted = cipher.DecryptData(encrypted);
        Assert.Equal(plain, decrypted);
    }

    [Fact]
    public void Salt_ChangesCiphertextKey()
    {
        using var noSalt = new RcloneCipher(Password, null, FilenameEncoding.Base32);
        using var withSalt = new RcloneCipher(Password, "different", FilenameEncoding.Base32);

        byte[] plain = Encoding.UTF8.GetBytes("data");
        byte[] encrypted = noSalt.EncryptData(plain);

        // A cipher created with a different salt must not be able to authenticate it.
        Assert.Throws<CryptException>(() => withSalt.DecryptData(encrypted));
    }

    [Fact]
    public void EmptyFile_RoundTrips()
    {
        using var cipher = new RcloneCipher(Password, null, FilenameEncoding.Base32);
        byte[] encrypted = cipher.EncryptData([]);
        byte[] decrypted = cipher.DecryptData(encrypted);
        Assert.Empty(decrypted);
    }

    [Fact]
    public void LargeMultiBlockFile_RoundTrips()
    {
        using var cipher = new RcloneCipher(Password, null, FilenameEncoding.Base32);
        var plain = new byte[64 * 1024 * 2 + 123]; // spans three secretbox blocks
        new Random(42).NextBytes(plain);
        byte[] encrypted = cipher.EncryptData(plain);
        byte[] decrypted = cipher.DecryptData(encrypted);
        Assert.Equal(plain, decrypted);
    }

    [Theory]
    [InlineData(FilenameEncoding.Base32)]
    [InlineData(FilenameEncoding.Base64)]
    public void NameRoundTrip(FilenameEncoding encoding)
    {
        using var cipher = new RcloneCipher(Password, null, encoding);
        const string name = "TEST_FILE.txt";
        string encrypted = cipher.EncryptName(name);
        Assert.NotEqual(name, encrypted);
        Assert.Equal(name, cipher.DecryptName(encrypted));
    }

    [Fact]
    public void NameEncryption_IsDeterministic()
    {
        using var cipher = new RcloneCipher(Password, null, FilenameEncoding.Base32);
        Assert.Equal(cipher.EncryptName("a-name.dat"), cipher.EncryptName("a-name.dat"));
    }

    [Fact]
    public void WrongPassword_FailsAuthentication()
    {
        using var encCipher = new RcloneCipher(Password, null, FilenameEncoding.Base32);
        byte[] encrypted = encCipher.EncryptData(Encoding.UTF8.GetBytes("secret"));

        using var wrong = new RcloneCipher("not the password", null, FilenameEncoding.Base32);
        Assert.Throws<CryptException>(() => wrong.DecryptData(encrypted));
    }

    [Fact]
    public void TamperedCiphertext_FailsAuthentication()
    {
        using var cipher = new RcloneCipher(Password, null, FilenameEncoding.Base32);
        byte[] encrypted = cipher.EncryptData(Encoding.UTF8.GetBytes("authentic"));
        encrypted[^1] ^= 0x01; // flip a bit in the ciphertext/tag
        Assert.Throws<CryptException>(() => cipher.DecryptData(encrypted));
    }

    [Fact]
    public void BadMagic_IsRejected()
    {
        using var cipher = new RcloneCipher(Password, null, FilenameEncoding.Base32);
        var bogus = new byte[64];
        Assert.Throws<CryptException>(() => cipher.DecryptData(bogus));
    }

    [Fact]
    public void TooShortFile_IsRejected()
    {
        using var cipher = new RcloneCipher(Password, null, FilenameEncoding.Base32);
        Assert.Throws<CryptException>(() => cipher.DecryptData(new byte[8]));
    }

    [Fact]
    public void DecryptName_RejectsNonBlockMultiple()
    {
        using var cipher = new RcloneCipher(Password, null, FilenameEncoding.Base32);
        // 5 base32hex chars decode to ~3 bytes, not a multiple of the 16-byte block.
        Assert.Throws<CryptException>(() => cipher.DecryptName("abcde"));
    }

    [Fact]
    public void DecryptName_RejectsInvalidBase32Char()
    {
        using var cipher = new RcloneCipher(Password, null, FilenameEncoding.Base32);
        Assert.Throws<CryptException>(() => cipher.DecryptName("zzz!!!"));
    }

    [Fact]
    public void LongFilename_RoundTrips_AcrossManyEmeBlocks()
    {
        using var cipher = new RcloneCipher(Password, null, FilenameEncoding.Base64);
        string name = new string('x', 200); // > 12 EME blocks after padding
        Assert.Equal(name, cipher.DecryptName(cipher.EncryptName(name)));
    }

    // ----- Compatibility with files produced by rclone itself -----

    [Theory]
    [InlineData("sample-base32.bin", FilenameEncoding.Base32, "kr9tu4e1da4u3nifdd99g9tf5o", "TEST_FILE.txt")]
    [InlineData("sample-base64.bin", FilenameEncoding.Base64, "Iyxcijgc9bp3o5Y0npW6xqUvwWNcc3MA4SadB0sR6cY", "TEST_FILE BASE64.txt")]
    public void DecryptsRealRcloneFiles(string fixture, FilenameEncoding encoding, string encryptedName, string plainName)
    {
        using var cipher = new RcloneCipher(Password, null, encoding);
        byte[] encrypted = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TestData", fixture));

        // Filename decrypts to the expected original name...
        Assert.Equal(plainName, cipher.DecryptName(encryptedName));
        // ...and re-encrypting that name is deterministic and matches rclone's output.
        Assert.Equal(encryptedName, cipher.EncryptName(plainName));

        // Content authenticates (proves the data key matches rclone) and is BIP39-ish text.
        byte[] content = cipher.DecryptData(encrypted);
        string text = Encoding.UTF8.GetString(content);
        Assert.Contains("umbrella", text);
    }
}
