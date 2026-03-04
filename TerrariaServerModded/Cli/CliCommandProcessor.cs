using System.Text;

namespace TerrariaServerModded.Cli;

[CliCommandProcessor]
public static partial class CliCommandProcessor
{
    public static bool HandleConsoleInput(ReadOnlySpan<char> input)
    {
        if (TryHandle(input, out var response))
        {
            Console.WriteLine(Encoding.UTF8.GetString(response.Span));
            return true;
        }

        return false;
    }
}