using System.Text;
using RcloneCrypt.Cli;

namespace RcloneCrypt.Tests;

/// <summary>
/// Head-less <see cref="IConsole"/> used to drive the CLI in tests, including
/// scripted answers to the interactive password / salt prompts.
/// </summary>
internal sealed class FakeConsole : IConsole
{
    private readonly Queue<string> _secrets;
    private readonly Queue<string> _lines;
    private readonly StringWriter _out = new();
    private readonly StringWriter _err = new();

    public FakeConsole(IEnumerable<string>? secrets = null, IEnumerable<string>? lines = null)
    {
        _secrets = new Queue<string>(secrets ?? []);
        _lines = new Queue<string>(lines ?? []);
    }

    public TextWriter Out => _out;
    public TextWriter Error => _err;

    public string Output => _out.ToString();
    public string ErrorOutput => _err.ToString();

    public string? ReadSecret(string prompt)
    {
        _err.Write(prompt);
        return _secrets.Count > 0 ? _secrets.Dequeue() : string.Empty;
    }

    public string? ReadLine(string prompt)
    {
        _err.Write(prompt);
        return _lines.Count > 0 ? _lines.Dequeue() : string.Empty;
    }
}
