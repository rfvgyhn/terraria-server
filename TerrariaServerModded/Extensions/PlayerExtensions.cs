using System.Diagnostics;
using Terraria;
using Terraria.Chat;
using Terraria.ID;
using Terraria.Localization;
using Terraria.Net;

namespace TerrariaServerModded.Extensions;

public static class PlayerExtensions
{
    public static void SendErrorMessage(this Player p, string message) =>
        ChatHelper.SendChatMessageToClient(NetworkText.FromLiteral(message), new(255, 0, 0), p.whoAmI);

    public static void SendInfoMessage(this Player p, string message) =>
        ChatHelper.SendChatMessageToClient(NetworkText.FromLiteral(message), new(0, 255, 255), p.whoAmI);

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
    
    public static int MaxPlayerIdLength
    {
        get
        {
            const int maxPrefixLength = 6;
            const int separatorCount = 2;
            const int clientUuidMaxLength = 36; // GUID string length with default formatting 
            const int steamIdMaxLength = 20; // max length of SteamID64 which is a ulong
            var idMaxLength = maxPrefixLength + Math.Max(clientUuidMaxLength, steamIdMaxLength) + Player.nameLen +
                              separatorCount;
            Debug.Assert(idMaxLength <= 1024, "Player ID length exceeds maximum allowed length");
            return idMaxLength;
        }
    }

    public static string GetPlayerId(this RemoteClient client, string playerName)
    {
        Span<char> idBuffer = stackalloc char[MaxPlayerIdLength];
        if (!client.TryGetPlayerId(playerName, idBuffer, out var charsWritten))
            throw new InvalidOperationException("Player ID buffer too small");
        
        return new string(idBuffer[..charsWritten]);
    }

    public static bool TryGetPlayerId(this RemoteClient client, string playerName, Span<char> destination,
        out int charsWritten)
    {
        charsWritten = 0;
        var address = client.Socket?.GetRemoteAddress();
        
        if (address is null)
            return false;

        if (address is SteamAddress steamAddress)
        {
            // Calculate for: "steam_" (6) + SteamId + "_" (1) + name
            if (!"steam_".AsSpan().TryCopyTo(destination))
                return false;

            if (!steamAddress.SteamId.m_SteamID.TryFormat(destination[6..], out var idLen))
                return false;

            int separatorIndex = 6 + idLen;
            if (destination.Length <= separatorIndex)
                return false;
            destination[separatorIndex] = '_';

            if (!playerName.AsSpan().TryCopyTo(destination[(separatorIndex + 1)..]))
                return false;

            charsWritten = separatorIndex + 1 + playerName.Length;
            return true;
        }

        if (string.IsNullOrEmpty(client.ClientUUID))
        {
            if (!PlayerStore.UnknownPlayerId.AsSpan().TryCopyTo(destination))
                return false;
            charsWritten = PlayerStore.UnknownPlayerId.Length;
            return true;
        }

        // Calculate for: "client_" (7) + UUID + "_" (1) + name
        if (!"client_".AsSpan().TryCopyTo(destination))
            return false;

        if (!client.ClientUUID.AsSpan().TryCopyTo(destination[7..]))
            return false;

        int clientSepIndex = 7 + client.ClientUUID.Length;
        if (destination.Length <= clientSepIndex)
            return false;
        destination[clientSepIndex] = '_';

        if (!playerName.AsSpan().TryCopyTo(destination[(clientSepIndex + 1)..]))
            return false;

        charsWritten = clientSepIndex + 1 + playerName.Length;
        return true;
    }

    public static string GetPlayerId(this Player p)
    {
        var client = Netplay.Clients[p.whoAmI];

        return client.GetPlayerId(p.name);
    }

    public static bool TryGetPlayerId(this Player p, Span<char> destination, out int charsWritten)
    {
        var client = Netplay.Clients[p.whoAmI];

        return client.TryGetPlayerId(p.name, destination, out charsWritten);
    }
}