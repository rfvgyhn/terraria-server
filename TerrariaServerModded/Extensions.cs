using System.Buffers;
using Terraria;
using Terraria.Chat;
using Terraria.ID;
using Terraria.Localization;

namespace TerrariaServerModded;

public static class Extensions
{
    private static readonly SearchValues<char> InvalidFileNameChars =
        SearchValues.Create(Path.GetInvalidFileNameChars());

    public static string ToSafeFileName(this string? input, char replacement = '_')
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        return string.Create(input.Length, input, (span, source) =>
        {
            source.AsSpan().CopyTo(span);

            int index;
            var globalOffset = 0;
            ReadOnlySpan<char> remaining = span;

            while ((index = remaining.IndexOfAny(InvalidFileNameChars)) != -1)
            {
                var targetIndex = globalOffset + index;
                span[targetIndex] = replacement;

                globalOffset = targetIndex + 1;
                remaining = span[globalOffset..];
            }
        });
    }

    public static bool[] UnpackBools(this uint value, int bitCount)
    {
        if (bitCount > 32)
            throw new ArgumentException("Too many bits for uint");

        var bits = new bool[bitCount];
        for (var i = 0; i < bitCount; i++)
            bits[i] = (value & (1u << i)) != 0;

        return bits;
    }

    public static uint PackBools(this bool[] bits)
    {
        if (bits.Length > 32)
            throw new ArgumentException("Too many bits for uint");

        var value = 0u;
        for (var i = 0; i < bits.Length; i++)
        {
            if (bits[i])
                value |= 1u << i;
        }

        return value;
    }

    public static void SendErrorMessage(this Player p, string message) =>
        ChatHelper.DisplayMessageOnClient(NetworkText.FromLiteral(message), new(255, 0, 0), p.whoAmI);

    public static void SendInfoMessage(this Player p, string message) =>
        ChatHelper.DisplayMessageOnClient(NetworkText.FromLiteral(message), new(0, 255, 255), p.whoAmI);

    public static void Reset(this Player p)
    {
        p.CurrentLoadoutIndex = 0;
        p.voidVaultInfo = default;

        ResetStats(p);
        ResetFlags(p);
        ResetAllItems();
        ResetVisuals(p);

        return;

        void ResetAllItems()
        {
            p.trashItem = new();

            ResetItems(p.inventory);
            ResetItems(p.armor);
            ResetItems(p.dye);
            ResetItems(p.miscEquips);
            ResetItems(p.miscDyes);
            ResetItems(p.bank.item);
            ResetItems(p.bank2.item);
            ResetItems(p.bank3.item);
            ResetItems(p.bank4.item);
            ResetLoadouts(p.Loadouts);

            p.inventory[0].SetDefaults(ItemID.CopperPickaxe);
            p.inventory[1].SetDefaults(ItemID.CopperAxe);
            p.inventory[2].SetDefaults(ItemID.CopperShortsword);
        }

        static void ResetStats(Player p)
        {
            p.anglerQuestsFinished = 0;
            p.dead = false;
            p.golferScoreAccumulated = 0;
            p.numberOfDeathsPVE = 0;
            p.numberOfDeathsPVP = 0;
            p.respawnTimer = 0;
            p.statLife = 100;
            p.statLifeMax = 100;
            p.statMana = 20;
            p.statManaMax = 20;
            p.taxMoney = 0;
            p.team = 0;
        }

        static void ResetFlags(Player p)
        {
            p.ateArtisanBread = false;
            p.downedDD2EventAnyDifficulty = false;
            p.enabledSuperCart = true;
            p.extraAccessory = false;
            p.happyFunTorchTime = false;
            p.unlockedBiomeTorches = false;
            p.unlockedSuperCart = false;
            p.usedAegisCrystal = false;
            p.usedAegisFruit = false;
            p.usedAmbrosia = false;
            p.usedArcaneCrystal = false;
            p.usedGalaxyPearl = false;
            p.usedGummyWorm = false;
        }

        static void ResetVisuals(Player p)
        {
            p.hideMisc = default;
            Array.Clear(p.hideVisibleAccessory);
        }

        static void ResetItems(Item[] items)
        {
            foreach (var item in items)
                item.SetDefaults(0);
        }

        static void ResetLoadouts(EquipmentLoadout[] loadouts)
        {
            foreach (var loadout in loadouts)
            {
                ResetItems(loadout.Armor);
                ResetItems(loadout.Dye);
                Array.Clear(loadout.Hide);
            }
        }
    }
}