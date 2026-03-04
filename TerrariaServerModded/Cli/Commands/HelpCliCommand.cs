using System.Text;
using Terraria.Localization;

namespace TerrariaServerModded.Cli.Commands;

public class HelpCliCommand : ICliCommand
{
    public static string Display => Command;
    public static string Description => "Display a list of available commands";
    public static string Command => "help";

    public static bool TryExecute(ReadOnlySpan<char> input, out ReadOnlyMemory<byte> response)
    {
        Console.WriteLine(Language.GetTextValue("CLI.AvailableCommands"));
        Console.WriteLine();
        var text = CliCommandProcessor.List();
        Console.WriteLine(text);
        Console.SetOut(new WriteLineSuppressor(Console.Out));
        response = Encoding.UTF8.GetBytes(text);
        
        return false;
    }

    private class WriteLineSuppressor(TextWriter original) : TextWriter
    {
        public override Encoding Encoding => original.Encoding;
        public override void WriteLine(string? value) => Console.SetOut(original);
        public override void Write(string? value) => original.Write(value);
    }
}