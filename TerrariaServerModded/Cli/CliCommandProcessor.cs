using System.Text;
using Microsoft.Xna.Framework;

namespace TerrariaServerModded.Cli;

[CliCommandProcessor]
public static partial class CliCommandProcessor
{
    public static readonly Color ChatColor = new(0, 255, 255);
    
    public static ReadOnlySpan<char> GetArgString(ReadOnlySpan<char> input)
    {
        var firstSpace = input.IndexOf(' ');
        return firstSpace > 0
            ? input[(firstSpace + 1)..].Trim()
            : [];
    }
    
    public static ReadOnlySpan<char> HandleConsoleInput(ReadOnlySpan<char> input)
    {
        var cmdToRun = TryHandle(input, out var response);
        if (cmdToRun.IsEmpty)
            Console.WriteLine(Encoding.UTF8.GetString(response.Span));

        return cmdToRun;
    }
}