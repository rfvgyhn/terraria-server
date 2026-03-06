using Terraria;
using Terraria.Localization;

namespace TerrariaServerModded.Cli.Commands;

public class ExitCliCommand : ICliCommand
{
    public static string Display => $"{Command} [reason]";
    public static string Description => "Shutdown the server and save with an optional reason";
    public static string Command => "exit";

    public static ReadOnlySpan<char> TryExecute(ReadOnlySpan<char> input, out ReadOnlyMemory<byte> response)
    {
        if (CliCommandProcessor.GetArgString(input) is { IsEmpty: false } arg)
        {
            for (var i = 0; i < Main.player.Length; i++)
            {
                if (Main.player[i].active)
                    NetMessage.BootPlayer(i, NetworkText.FromLiteral($"Server is shutting down - {arg}"));
            }
        }

        response = default;
        return Command;
    }
}