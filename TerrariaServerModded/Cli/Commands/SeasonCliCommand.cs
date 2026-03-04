using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.Localization;

namespace TerrariaServerModded.Cli.Commands;

public class SeasonCliCommand : ICliCommand
{
    public static string Display => "season [season]";
    public static string Description => "Gets or sets the world's season (xmas/christmas, halloween, none)";
    public static string Command => "season";
    public static bool TryExecute(ReadOnlySpan<char> input, out ReadOnlyMemory<byte> response)
    {
        var firstSpace = input.IndexOf(' ');
        var arg = firstSpace > 0
            ? input[(firstSpace + 1)..].Trim()
            : "";

        var wasXmas = Main.xMas;
        var wasHalloween = Main.halloween;
        switch (MapArg(arg))
        {
            case Season.None:
                Main.forceXMasForever = false;
                Main.forceHalloweenForever = false;
                break;
            case Season.Christmas:
                Main.forceXMasForever = true;
                Main.forceHalloweenForever = false;
                break;
            case Season.Halloween:
                Main.forceXMasForever = false;
                Main.forceHalloweenForever = true;
                break;
            case null:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        
        Main.checkXMas();
        Main.checkHalloween();
        
        var chatColor = new Color(0, 255, 255);
        if (Main.xMas)
        {
            response = Encoding.UTF8.GetBytes(Language.GetTextValue("Bestiary_Events.Christmas"));
            if (!wasXmas)
                ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(Language.GetTextValue("Misc.StartedVictoryXmas")), chatColor);
        }
        else if (Main.halloween)
        {
            response = Encoding.UTF8.GetBytes(Language.GetTextValue("Bestiary_Events.Halloween"));
            if (!wasHalloween)
                ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(Language.GetTextValue("Misc.StartedVictoryHalloween")), chatColor);
        }
        else
        {
            response = "None"u8.ToArray();
            if (wasXmas)
                ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(Language.GetTextValue("Misc.EndedVictoryXmas")), chatColor);
            if (wasHalloween)
                ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(Language.GetTextValue("Misc.EndedVictoryHalloween")), chatColor);
        }

        return true;
    }
    
    private static Season? MapArg(ReadOnlySpan<char> arg)
    {
        if (arg.Length > 9)
            return null;

        Span<char> loweredArg = stackalloc char[arg.Length];
        arg.ToLowerInvariant(loweredArg);
        return loweredArg switch
        {
            "xmas" or "christmas" => Season.Christmas,
            "halloween" => Season.Halloween,
            "none" => Season.None,
            _ => null
        };
    }

    private enum Season
    {
        None,
        Christmas,
        Halloween,
    }
}