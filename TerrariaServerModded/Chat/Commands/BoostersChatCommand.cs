using System.Text;
using Terraria;
using Terraria.Localization;
using TerrariaServerModded.Extensions;

namespace TerrariaServerModded.Chat.Commands;

public class BoostersChatCommand : IChatCommand
{
    public static string Description => "/boosters: Display your character's and the world's permanent boosters";
    public static string DescriptionKey => "ChatCommandDescription.PlayerBoosters";

    public static bool Matches(ReadOnlySpan<char> input) =>
        input.Equals("/boosters", StringComparison.OrdinalIgnoreCase);

    public static void Execute(Player player, AdditionalPlayerData? data, ReadOnlySpan<char> input)
    {
        var sb = new StringBuilder();
        
        AppendCharacterBased();
        if (sb.Length > 0)
            player.SendInfoMessage(sb.ToString());
        
        sb.Clear();
        AppendWorldBased();
        if (sb.Length > 0)
            player.SendInfoMessage(sb.ToString());
        
        return;

        void AppendCharacterBased()
        {
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
        }

        void AppendWorldBased()
        {
            Append(NPC.combatBookWasUsed, "ItemName.CombatBook");
            Append(NPC.combatBookVolumeTwoWasUsed, "ItemName.CombatBookVolumeTwo");
            Append(NPC.peddlersSatchelWasUsed, "ItemName.PeddlersSatchel");
        }

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