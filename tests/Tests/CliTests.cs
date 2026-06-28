using System.Text;
using RcloneCrypt.Cli;
using Xunit;

namespace RcloneCrypt.Tests;

public sealed class CliTests : IDisposable
{
    private readonly string _dir;

    public CliTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "rcrypt-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        // Make sure no ambient env vars leak into the prompt-based tests.
        Environment.SetEnvironmentVariable(CliApp.PasswordEnvVar, null);
        Environment.SetEnvironmentVariable(CliApp.SaltEnvVar, null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(CliApp.PasswordEnvVar, null);
        Environment.SetEnvironmentVariable(CliApp.SaltEnvVar, null);
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private string WriteInput(string name, string content)
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void EncryptThenDecrypt_WithPasswordFlag_RoundTrips_AndWarns()
    {
        string input = WriteInput("plain.txt", "the quick brown fox");
        string encPath = Path.Combine(_dir, "out.enc");

        var enc = new FakeConsole();
        int rc1 = CliApp.Run(
            ["encrypt", "-i", input, "-o", encPath, "--password", "hunter2"], enc);
        Assert.Equal(0, rc1);
        Assert.Contains("WARNING", enc.ErrorOutput); // insecure --password warning

        string decPath = Path.Combine(_dir, "back.txt");
        var dec = new FakeConsole();
        int rc2 = CliApp.Run(
            ["decrypt", "-i", encPath, "-o", decPath, "--password", "hunter2"], dec);
        Assert.Equal(0, rc2);
        Assert.Equal("the quick brown fox", File.ReadAllText(decPath));
    }

    [Fact]
    public void RoundTrip_WithSaltFlag()
    {
        string input = WriteInput("plain.txt", "salted content");
        string encPath = Path.Combine(_dir, "out.enc");
        string decPath = Path.Combine(_dir, "back.txt");

        Assert.Equal(0, CliApp.Run(
            ["encrypt", "-i", input, "-o", encPath, "--password", "pw", "--salt", "s4lt"], new FakeConsole()));
        Assert.Equal(0, CliApp.Run(
            ["decrypt", "-i", encPath, "-o", decPath, "--password", "pw", "--salt", "s4lt"], new FakeConsole()));

        Assert.Equal("salted content", File.ReadAllText(decPath));
    }

    [Fact]
    public void WrongSalt_FailsDecryption()
    {
        string input = WriteInput("plain.txt", "salted content");
        string encPath = Path.Combine(_dir, "out.enc");
        string decPath = Path.Combine(_dir, "back.txt");

        CliApp.Run(["encrypt", "-i", input, "-o", encPath, "--password", "pw", "--salt", "right"], new FakeConsole());
        int rc = CliApp.Run(["decrypt", "-i", encPath, "-o", decPath, "--password", "pw", "--salt", "wrong"], new FakeConsole());
        Assert.Equal(1, rc);
        Assert.False(File.Exists(decPath));
    }

    [Fact]
    public void RoundTrip_WithEnvVarPassword()
    {
        Environment.SetEnvironmentVariable(CliApp.PasswordEnvVar, "env-secret");
        string input = WriteInput("plain.txt", "from env");
        string encPath = Path.Combine(_dir, "out.enc");
        string decPath = Path.Combine(_dir, "back.txt");

        Assert.Equal(0, CliApp.Run(["encrypt", "-i", input, "-o", encPath], new FakeConsole()));
        Assert.Equal(0, CliApp.Run(["decrypt", "-i", encPath, "-o", decPath], new FakeConsole()));
        Assert.Equal("from env", File.ReadAllText(decPath));
    }

    [Fact]
    public void RoundTrip_WithPrompt_ForPasswordAndSalt()
    {
        string input = WriteInput("plain.txt", "prompted secret");
        string encPath = Path.Combine(_dir, "out.enc");
        string decPath = Path.Combine(_dir, "back.txt");

        // Prompts: first ReadSecret -> password, second ReadSecret -> salt.
        var enc = new FakeConsole(secrets: ["promptpw", "promptsalt"]);
        Assert.Equal(0, CliApp.Run(["encrypt", "-i", input, "-o", encPath], enc));

        var dec = new FakeConsole(secrets: ["promptpw", "promptsalt"]);
        Assert.Equal(0, CliApp.Run(["decrypt", "-i", encPath, "-o", decPath], dec));

        Assert.Equal("prompted secret", File.ReadAllText(decPath));
    }

    [Fact]
    public void CustomEncoding_Base64_RoundTrips_AndDerivesName()
    {
        string input = WriteInput("report.txt", "encoded with base64 names");

        // No -o: the encrypted output name is derived from the encrypted filename.
        var enc = new FakeConsole();
        Assert.Equal(0, CliApp.Run(
            ["encrypt", "-i", input, "--password", "pw", "--filename-encoding", "base64"], enc));

        // Find the produced (base64-named) ciphertext file.
        string encryptedFile = Directory.GetFiles(_dir)
            .Single(f => Path.GetFileName(f) != "report.txt");

        var dec = new FakeConsole();
        Assert.Equal(0, CliApp.Run(
            ["decrypt", "-i", encryptedFile, "--password", "pw", "--filename-encoding", "base64"], dec));

        // Decrypting restores the original filename next to the ciphertext.
        Assert.Equal("encoded with base64 names", File.ReadAllText(Path.Combine(_dir, "report.txt")));
    }

    [Fact]
    public void PasswordFlag_OverridesEnvVar()
    {
        Environment.SetEnvironmentVariable(CliApp.PasswordEnvVar, "env-pw");
        string input = WriteInput("plain.txt", "precedence");
        string encPath = Path.Combine(_dir, "out.enc");
        string decPath = Path.Combine(_dir, "back.txt");

        // Encrypt with the flag password; the env var must be ignored.
        Assert.Equal(0, CliApp.Run(["encrypt", "-i", input, "-o", encPath, "--password", "flag-pw"], new FakeConsole()));
        // Decrypting with the env-var password must fail (wrong key)...
        Assert.Equal(1, CliApp.Run(["decrypt", "-i", encPath, "-o", decPath], new FakeConsole()));
        // ...while decrypting with the flag password succeeds.
        Assert.Equal(0, CliApp.Run(["decrypt", "-i", encPath, "-o", decPath, "--password", "flag-pw"], new FakeConsole()));
        Assert.Equal("precedence", File.ReadAllText(decPath));
    }

    [Fact]
    public void MissingFlagValue_IsRejected()
    {
        string input = WriteInput("plain.txt", "x");
        int rc = CliApp.Run(["encrypt", "-i", input, "--password"], new FakeConsole());
        Assert.Equal(2, rc);
    }

    [Fact]
    public void UnknownOption_IsRejected()
    {
        string input = WriteInput("plain.txt", "x");
        int rc = CliApp.Run(["encrypt", "-i", input, "--password", "pw", "--bogus"], new FakeConsole());
        Assert.Equal(2, rc);
    }

    [Fact]
    public void EmptyPassword_IsRejected()
    {
        string input = WriteInput("plain.txt", "x");
        var c = new FakeConsole(secrets: [""]); // prompt returns empty
        int rc = CliApp.Run(["encrypt", "-i", input, "-o", Path.Combine(_dir, "o"), ], c);
        Assert.Equal(2, rc);
    }

    [Fact]
    public void MissingInput_IsRejected()
    {
        int rc = CliApp.Run(["encrypt", "--password", "pw"], new FakeConsole());
        Assert.Equal(2, rc);
    }

    [Fact]
    public void Version_PrintsNameAndVersion()
    {
        var c = new FakeConsole();
        int rc = CliApp.Run(["--version"], c);
        Assert.Equal(0, rc);
        Assert.Contains(CliApp.ProgramName, c.Output);
    }

    [Fact]
    public void UnknownEncoding_IsRejected()
    {
        string input = WriteInput("plain.txt", "x");
        int rc = CliApp.Run(
            ["encrypt", "-i", input, "--password", "pw", "--filename-encoding", "rot13"], new FakeConsole());
        Assert.Equal(1, rc);
    }
}
