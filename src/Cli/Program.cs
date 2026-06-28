using System.Text;
using RcloneCrypt.Cli;

var console = new SystemConsole();
return CliApp.Run(args, console);

/// <summary>
/// Real terminal implementation of <see cref="IConsole"/>. Masks secret input
/// when attached to an interactive console, and falls back to a plain read when
/// stdin is redirected (pipes, here-strings, tests).
/// </summary>
internal sealed class SystemConsole : IConsole
{
    public TextWriter Out => Console.Out;
    public TextWriter Error => Console.Error;

    public string? ReadLine(string prompt)
    {
        Console.Error.Write(prompt);
        return Console.In.ReadLine();
    }

    public string? ReadSecret(string prompt)
    {
        Console.Error.Write(prompt);

        if (Console.IsInputRedirected)
            return Console.In.ReadLine();

        var sb = new StringBuilder();
        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.Error.WriteLine();
                break;
            }
            if (key.Key == ConsoleKey.Backspace)
            {
                if (sb.Length > 0)
                    sb.Length--;
                continue;
            }
            if (!char.IsControl(key.KeyChar))
                sb.Append(key.KeyChar);
        }
        return sb.ToString();
    }
}
