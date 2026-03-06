using Terraria;

namespace TerrariaServerModded.Cli.Commands;

public class IsIdleCliCommand : ICliCommand
{
    private static readonly byte[] TrueResponse = "true"u8.ToArray();
    private static readonly byte[] FalseResponse = "false"u8.ToArray();

    public static string Display => Command;
    public static string Description => "Check if the server has zero active players";
    public static string Command => "isidle";

    public static ReadOnlySpan<char> TryExecute(ReadOnlySpan<char> input, out ReadOnlyMemory<byte> response)
    {
        foreach (var p in Main.player)
        {
            if (p.active)
            {
                response = FalseResponse;
                return [];
            }
        }

        response = TrueResponse;
        return [];
    }
}