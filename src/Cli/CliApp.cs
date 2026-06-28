using RcloneCrypt.Core;

namespace RcloneCrypt.Cli;

/// <summary>
/// Parses arguments and runs the encrypt/decrypt commands. All terminal IO goes
/// through <see cref="IConsole"/> so the whole flow is unit-testable.
/// </summary>
public static class CliApp
{
    public const string ProgramName = "cli-claude48-opusmedium-csharp";
    public const string PasswordEnvVar = "RCRYPT_PASSWORD";
    public const string SaltEnvVar = "RCRYPT_SALT";

    public static string Version =>
        typeof(CliApp).Assembly.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "0.0.0";

    public static int Run(string[] args, IConsole console)
    {
        try
        {
            return RunInner(args, console);
        }
        catch (CryptException ex)
        {
            console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
        catch (CliException ex)
        {
            console.Error.WriteLine($"error: {ex.Message}");
            return 2;
        }
        catch (FileNotFoundException ex)
        {
            console.Error.WriteLine($"error: file not found: {ex.FileName ?? ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static int RunInner(string[] args, IConsole console)
    {
        if (args.Length == 0)
        {
            PrintHelp(console.Out);
            return 2;
        }

        string command = args[0].ToLowerInvariant();

        // Top-level flags that work without a command.
        if (command is "--version" or "-v" or "version")
        {
            console.Out.WriteLine($"{ProgramName} {Version}");
            return 0;
        }
        if (command is "--help" or "-h" or "help")
        {
            PrintHelp(console.Out);
            return 0;
        }

        bool encrypt = command switch
        {
            "encrypt" or "enc" => true,
            "decrypt" or "dec" => false,
            _ => throw new CliException($"unknown command '{args[0]}' (expected 'encrypt' or 'decrypt')")
        };

        var options = ParseOptions(args[1..]);

        if (options.ShowHelp)
        {
            PrintHelp(console.Out);
            return 0;
        }

        if (string.IsNullOrEmpty(options.InputFile))
            throw new CliException("an input file is required (-i / --input-file)");
        if (!File.Exists(options.InputFile))
            throw new CliException($"input file does not exist: {options.InputFile}");

        FilenameEncoding encoding = NameEncoding.Parse(options.Encoding);
        string password = ResolvePassword(options, console);
        string? salt = ResolveSalt(options, console);

        using var cipher = new RcloneCipher(password, salt, encoding);

        byte[] inputBytes = File.ReadAllBytes(options.InputFile);
        string inputName = Path.GetFileName(options.InputFile);
        string inputDir = Path.GetDirectoryName(Path.GetFullPath(options.InputFile)) ?? ".";

        string outputPath;
        if (encrypt)
        {
            byte[] outBytes = cipher.EncryptData(inputBytes);
            outputPath = options.OutputFile ?? Path.Combine(inputDir, cipher.EncryptName(inputName));
            File.WriteAllBytes(outputPath, outBytes);
            console.Out.WriteLine($"Encrypted '{options.InputFile}' -> '{outputPath}'");
        }
        else
        {
            byte[] outBytes = cipher.DecryptData(inputBytes);
            if (options.OutputFile is not null)
            {
                outputPath = options.OutputFile;
            }
            else
            {
                string decryptedName = cipher.DecryptName(inputName);
                outputPath = Path.Combine(inputDir, decryptedName);
            }
            File.WriteAllBytes(outputPath, outBytes);
            console.Out.WriteLine($"Decrypted '{options.InputFile}' -> '{outputPath}'");
        }

        return 0;
    }

    private static string ResolvePassword(Options options, IConsole console)
    {
        if (options.Password is not null)
        {
            console.Error.WriteLine(
                "WARNING: passing --password on the command line is insecure. The value may be " +
                "stored in your shell history and visible to other processes.");
            console.Error.WriteLine(
                $"         Prefer the {PasswordEnvVar} environment variable or the interactive prompt, " +
                "and clear the offending shell-history entry afterwards.");
            if (options.Password.Length == 0)
                throw new CliException("password must not be empty");
            return options.Password;
        }

        string? fromEnv = Environment.GetEnvironmentVariable(PasswordEnvVar);
        if (!string.IsNullOrEmpty(fromEnv))
            return fromEnv;

        string? entered = console.ReadSecret("Password: ");
        if (string.IsNullOrEmpty(entered))
            throw new CliException("password must not be empty");
        return entered;
    }

    private static string? ResolveSalt(Options options, IConsole console)
    {
        if (options.Salt is not null)
            return options.Salt;

        string? fromEnv = Environment.GetEnvironmentVariable(SaltEnvVar);
        if (!string.IsNullOrEmpty(fromEnv))
            return fromEnv;

        // Salt is optional: an empty answer means "use rclone's built-in salt".
        string? entered = console.ReadSecret("Salt (optional, press Enter to skip): ");
        return string.IsNullOrEmpty(entered) ? null : entered;
    }

    private static Options ParseOptions(string[] args)
    {
        var options = new Options();
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "-i":
                case "--input-file":
                    options.InputFile = RequireValue(args, ref i, arg);
                    break;
                case "-o":
                case "--output-file":
                    options.OutputFile = RequireValue(args, ref i, arg);
                    break;
                case "--password":
                    options.Password = RequireValue(args, ref i, arg);
                    break;
                case "--salt":
                case "--password2":
                    options.Salt = RequireValue(args, ref i, arg);
                    break;
                case "--filename-encoding":
                case "--name-encoding":
                    options.Encoding = RequireValue(args, ref i, arg);
                    break;
                case "-h":
                case "--help":
                    options.ShowHelp = true;
                    break;
                default:
                    if (arg.StartsWith('-'))
                        throw new CliException($"unknown option '{arg}'");
                    throw new CliException($"unexpected argument '{arg}'");
            }
        }
        return options;
    }

    private static string RequireValue(string[] args, ref int i, string flag)
    {
        if (i + 1 >= args.Length)
            throw new CliException($"option '{flag}' requires a value");
        return args[++i];
    }

    private static void PrintHelp(TextWriter w)
    {
        w.WriteLine($"{ProgramName} - rclone-compatible file & filename encryption");
        w.WriteLine();
        w.WriteLine($"USAGE:\n  {ProgramName} <encrypt|decrypt> -i <input> [options]");
        w.WriteLine();
        w.WriteLine("COMMANDS:");
        w.WriteLine("  encrypt, enc    Encrypt a file (and derive its encrypted name)");
        w.WriteLine("  decrypt, dec    Decrypt a file (and recover its original name)");
        w.WriteLine();
        w.WriteLine("OPTIONS:");
        w.WriteLine("  -i, --input-file <path>        Input file (required)");
        w.WriteLine("  -o, --output-file <path>       Output file (optional; derived from the");
        w.WriteLine("                                 encrypted/decrypted filename when omitted)");
        w.WriteLine("      --password <pw>            Password (INSECURE - see warning below)");
        w.WriteLine("      --salt, --password2 <s>    Optional salt / password2");
        w.WriteLine("      --filename-encoding <enc>  base32 (default) or base64");
        w.WriteLine("  -h, --help                     Show this help");
        w.WriteLine("  -v, --version                  Show version");
        w.WriteLine();
        w.WriteLine("PASSWORD INPUT (most secure first):");
        w.WriteLine($"  1. Interactive prompt (default when --password is omitted)");
        w.WriteLine($"  2. {PasswordEnvVar} / {SaltEnvVar} environment variables");
        w.WriteLine("  3. --password flag (DISCOURAGED: leaks into shell history / process list;");
        w.WriteLine("     clear the relevant history entry if you use it)");
    }

    private sealed class Options
    {
        public string? InputFile;
        public string? OutputFile;
        public string? Password;
        public string? Salt;
        public string Encoding = "base32";
        public bool ShowHelp;
    }
}

internal sealed class CliException(string message) : Exception(message);
