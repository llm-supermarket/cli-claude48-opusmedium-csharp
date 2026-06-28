namespace RcloneCrypt.Cli;

/// <summary>
/// Abstraction over the terminal so the CLI can be driven head-less from tests
/// (including the interactive password / salt prompts).
/// </summary>
public interface IConsole
{
    TextWriter Out { get; }
    TextWriter Error { get; }

    /// <summary>Prompts for and reads a secret value with echo suppressed where possible.</summary>
    string? ReadSecret(string prompt);

    /// <summary>Prompts for and reads a normal (echoed) line.</summary>
    string? ReadLine(string prompt);
}
