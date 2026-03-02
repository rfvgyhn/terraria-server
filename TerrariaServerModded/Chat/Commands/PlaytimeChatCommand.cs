using Terraria;
using TerrariaServerModded.Extensions;

namespace TerrariaServerModded.Chat.Commands;

public class PlaytimeChatCommand : IChatCommand
{
    public static string Description => "/playtime: Display your character's time in this server";
    public static string DescriptionKey => "ChatCommandDescription.Playtime";

    public static bool Matches(ReadOnlySpan<char> input) =>
        input.Equals("/playtime", StringComparison.OrdinalIgnoreCase);

    public static void Execute(Player player, AdditionalPlayerData? data, ReadOnlySpan<char> input)
    {
        if (data is null)
            return;

        var time = data.PlayTime.Total;
        string message;
        if (time.TotalDays >= 1)
            message = $"{time.TotalDays:F0}d {time.Hours:D2}h {time.Minutes:D2}m";
        else if (time.TotalHours >= 1)
            message = $"{time.TotalHours:F0}h {time.Minutes:D2}m";
        else if (time.TotalMinutes >= 1)
            message = $"{time.TotalMinutes:F0} minutes";
        else
            message = $"{time.TotalSeconds:F0} seconds";
        
        player.SendInfoMessage(message);
    }
}