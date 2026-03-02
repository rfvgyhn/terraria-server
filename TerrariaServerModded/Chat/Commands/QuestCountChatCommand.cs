using Terraria;
using TerrariaServerModded.Extensions;

namespace TerrariaServerModded.Chat.Commands;

public class QuestCountChatCommand : IChatCommand
{
    public static string Description => "/quests: Display the number of angler quests you have completed";
    public static string DescriptionKey => "ChatCommandDescription.QuestCount";

    public static bool Matches(ReadOnlySpan<char> input) =>
        input.Equals("/quests", StringComparison.OrdinalIgnoreCase);

    public static void Execute(Player player, AdditionalPlayerData? data, ReadOnlySpan<char> input) =>
        player.SendInfoMessage(player.anglerQuestsFinished.ToString());
}