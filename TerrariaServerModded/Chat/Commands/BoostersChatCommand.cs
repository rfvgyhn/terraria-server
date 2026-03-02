using System.Text;
using Terraria;
using Terraria.Localization;
using TerrariaServerModded.Extensions;

namespace TerrariaServerModded.Chat.Commands;

public class BoostersChatCommand : IChatCommand
{
    public static string Description => "/boosters: Display your character's permanent boosters";
    public static string DescriptionKey => "ChatCommandDescription.PlayerBoosters";

    public static bool Matches(ReadOnlySpan<char> input) =>
        input.Equals("/boosters", StringComparison.OrdinalIgnoreCase);

    public static void Execute(Player player, AdditionalPlayerData? data, ReadOnlySpan<char> input)
    {
        var sb = new StringBuilder();
        Append(player.usedAegisCrystal, "ItemName.AegisCrystal");
        Append(player.usedAegisFruit, "ItemName.AegisFruit");
        Append(player.usedAmbrosia, "ItemName.Ambrosia");
        Append(player.usedArcaneCrystal, "ItemName.ArcaneCrystal");
        Append(player.ateArtisanBread, "ItemName.ArtisanLoaf");
        Append(player.extraAccessory, "ItemName.DemonHeart");
        Append(player.usedGalaxyPearl, "ItemName.GalaxyPearl");
        Append(player.usedGummyWorm, "ItemName.GummyWorm");
        Append(player.unlockedSuperCart, "ItemName.MinecartPowerup");
        Append(player.unlockedBiomeTorches, "ItemName.TorchGodsFavor");

        if (sb.Length > 0)
            player.SendInfoMessage(sb.ToString());

        return;

        void Append(bool flag, string key)
        {
            if (flag)
            {
                if (sb.Length > 0)
                    sb.Append(", ");
                sb.Append(LanguageManager.Instance.GetTextValue(key));
            }
        }
    }
}