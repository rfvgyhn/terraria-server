using System.Text;
using Terraria;
using Terraria.Chat;
using Terraria.ID;
using Terraria.Localization;

namespace TerrariaServerModded.Cli.Commands;

public class GameModeCliCommand : ICliCommand
{
    private static readonly Encoding Encoding = Encoding.UTF8;

    public static string Display => $"{Command} [mode]";
    public static string Description => "Gets or sets the world's game mode";
    public static string Command => "gamemode";

    public static bool TryExecute(ReadOnlySpan<char> input, out ReadOnlyMemory<byte> response)
    {
        var firstSpace = input.IndexOf(' ');
        var arg = firstSpace > 0
            ? input[(firstSpace + 1)..].Trim()
            : "";
        var mode = MapArg(arg);
        var text = MapMode(mode);
        
        if (mode != Main.GameMode)
        {
            Main.GameMode = mode;
            ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral($"World mode set to {text}"), new(0, 255, 255));
            NetMessage.SendData(MessageID.WorldData);
        }

        response = Encoding.GetBytes(text);
        return true;
    }

    private static int MapArg(ReadOnlySpan<char> arg)
    {
        if (arg.Length > 8)
            return Main.GameMode;

        Span<char> loweredArg = stackalloc char[arg.Length];
        arg.ToLowerInvariant(loweredArg);
        return loweredArg switch
        {
            "classic" or "normal" or "0" => GameModeID.Normal,
            "expert" or "1" => GameModeID.Expert,
            "master" or "2" => GameModeID.Master,
            "journey" or "creative" or "3" => GameModeID.Creative,
            _ => Main.GameMode
        };
    }

    public static string MapMode(int mode)
    {
        var key = mode switch
        {
            GameModeID.Normal => "Normal",
            GameModeID.Expert => "Expert",
            GameModeID.Master => "Master",
            GameModeID.Creative => "Creative",
            _ => "InvalidGameMode"
        };
        return LanguageManager.Instance.GetTextValue($"UI.{key}");
    }
}