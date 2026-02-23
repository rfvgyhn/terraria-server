using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Terraria;
using Terraria.Chat;
using Terraria.ID;
using Terraria.Localization;
using Terraria.Net;

namespace TerrariaServerModded;

public sealed class ServerMonitor : IDisposable
{
    private readonly byte _playerDifficulty;
    private readonly PlayerStore _playerStore;
    private readonly PlayerDataService _playerDataService;
    private readonly ILogger<ServerMonitor> _log;
    private readonly HashSet<string> _invalidPlayers = new();
    private readonly Dictionary<string, Playtime> _additionalPlayerData = new();

    public ServerMonitor(byte playerDifficulty, PlayerStore playerStore, PlayerDataService playerDataService,
        ILogger<ServerMonitor> log)
    {
        _playerDifficulty = playerDifficulty;
        _playerStore = playerStore;
        _playerDataService = playerDataService;
        _log = log;
        RegisterHooks();
    }

    private void RegisterHooks()
    {
        On.Terraria.IO.WorldFile.LoadWorld += OnWorldLoad;
        On.Terraria.IO.WorldFile.SaveWorld_bool_bool += OnWorldSave;
        On.Terraria.Initializers.ChatInitializer.Load += OnChatInitLoad;
        On.Terraria.Main.Initialize += OnInitialize;
        On.Terraria.MessageBuffer.GetData += OnGetData;
        On.Terraria.Player.Spawn += OnPlayerSpawn;
        On.Terraria.RemoteClient.Reset += OnClientDisconnect;
    }

    private void UnregisterHooks()
    {
        On.Terraria.IO.WorldFile.LoadWorld -= OnWorldLoad;
        On.Terraria.IO.WorldFile.SaveWorld_bool_bool -= OnWorldSave;
        On.Terraria.Initializers.ChatInitializer.Load -= OnChatInitLoad;
        On.Terraria.Main.Initialize -= OnInitialize;
        On.Terraria.MessageBuffer.GetData -= OnGetData;
        On.Terraria.Player.Spawn -= OnPlayerSpawn;
        On.Terraria.RemoteClient.Reset -= OnClientDisconnect;
    }

    private static void OnChatInitLoad(On.Terraria.Initializers.ChatInitializer.orig_Load orig)
    {
        orig();
        LanguageManager.Instance._localizedTexts.Add("ChatCommandDescription.Playtime", new LocalizedText("ChatCommandDescription.Playtime", "/playtime: Display your character's time in this server"));
    }

    private void OnGetData(On.Terraria.MessageBuffer.orig_GetData orig, MessageBuffer msgBuffer, int start, int length,
        out int msgType)
    {
        msgType = msgBuffer.readBuffer[start];

        if (msgType == MessageID.NetModules && HandleNetModules(msgBuffer, start, length)) 
            return;

        orig(msgBuffer, start, length, out msgType);
    }

    /// <returns>True if the packet was handled</returns>
    private bool HandleNetModules(MessageBuffer msgBuffer, int start, int length)
    {
        var reader = msgBuffer.reader;
        var originalPos = reader.BaseStream.Position;

        try
        {
            // Skip the Packet ID
            reader.BaseStream.Position = start + 1;

            var moduleType = reader.ReadUInt16();
            if (moduleType == NetManager.Instance.GetId<Terraria.GameContent.NetModules.NetTextModule>())
            {
                var msg = ChatMessage.Deserialize(reader);
                return HandleChat(msg, msgBuffer.whoAmI);
            }
        }
        finally
        {
            msgBuffer.reader.BaseStream.Position = originalPos;
        }
        
        return false;
    }

    /// <returns>True if the message was handled</returns>
    private bool HandleChat(ChatMessage msg, int playerIndex)
    {
        if (msg.Text.AsSpan().Trim().Equals("/playtime", StringComparison.OrdinalIgnoreCase))
        {
            var player = Main.player[playerIndex];
            if (_additionalPlayerData.TryGetValue(GetPlayerId(player), out var playtime))
            {
                if (playtime.Total.TotalDays >= 1)
                    player.SendInfoMessage($"{playtime.Total.TotalDays:F0}d {playtime.Total.Hours:D2}h {playtime.Total.Minutes:D2}m");
                else if (playtime.Total.TotalHours >= 1)
                    player.SendInfoMessage($"{playtime.Total.TotalHours:F0}h {playtime.Total.Minutes:D2}m");
                else if (playtime.Total.TotalMinutes >= 1)
                    player.SendInfoMessage($"{playtime.Total.TotalMinutes:F0} minutes");
                else
                    player.SendInfoMessage($"{playtime.Total.TotalSeconds:F0} seconds");
            }

            return true;
        }

        return false;
    }
    
    private static void OnInitialize(On.Terraria.Main.orig_Initialize orig, Main main)
    {
        Main.ServerSideCharacter = true;
        orig(main);
    }
    
    private void OnWorldLoad(On.Terraria.IO.WorldFile.orig_LoadWorld orig)
    {
        orig();
        _log.LogDebug("Setting world name for save directory: '{WorldName}'", Main.worldName);
        _playerStore.WorldDirectory = Main.worldName;
    }

    private void OnWorldSave(On.Terraria.IO.WorldFile.orig_SaveWorld_bool_bool orig, bool cloud, bool reset)
    {
        _log.LogInformation("World save detected. Queuing save of all online players");
        SavePlayers(_playerDataService, Main.player);
        orig(cloud, reset);
    }

    private void OnClientDisconnect(On.Terraria.RemoteClient.orig_Reset orig, RemoteClient remoteClient)
    {
        if (remoteClient.IsActive)
        {
            var player = Main.player[remoteClient.Id];
            var playerId = GetPlayerId(remoteClient, player.name);
            if (!_invalidPlayers.Contains(playerId))
            {
                if (player.difficulty == PlayerDifficultyID.Hardcore && (player.dead || player.ghost))
                    DeletePlayer(_playerDataService, player);
                else
                    SavePlayer(_playerDataService, player);
            }
            _invalidPlayers.Remove(playerId);
            _additionalPlayerData.Remove(playerId);
        }

        orig(remoteClient);
    }

    private void OnPlayerSpawn(On.Terraria.Player.orig_Spawn orig, Player player, PlayerSpawnContext context)
    {
        if (context == PlayerSpawnContext.SpawningIntoWorld)
            LoadPlayer(player, _playerStore);
        
        orig(player, context);
    }
    
    private void LoadPlayer(Player p, PlayerStore store)
    {
        var playerId = GetPlayerId(p);
        _invalidPlayers.Remove(playerId);
        _additionalPlayerData.Remove(playerId);

        if (!store.TryLoad(playerId, out var data))
        {
            _log.LogWarning("Player {Name} - {Id} will be forced to a new character until issue is resolved", p.name, playerId);
            _invalidPlayers.Add(playerId);
            p.SendErrorMessage("Your player data could not be loaded. Contact a server administrator for assistance.");
        }

        var playtime = TimeSpan.Zero;
        if (data is not null)
        {
            _log.LogInformation("Restoring player data: {Name}", p.name);
            data.ApplyTo(p);
            playtime = data.Stats.PlayTime;
        }
        else
        {
            _log.LogInformation("Initializing new player: {Name}", p.name);
            p.Reset();
        }
        
        _additionalPlayerData[playerId] = new Playtime(Stopwatch.GetTimestamp(), playtime);
        Array.Clear(p.buffType);
        Array.Clear(p.buffTime);
        p.difficulty = _playerDifficulty;
        _log.LogDebug("Syncing player: {Name}", p.name);
        SyncPlayer(p);

        if (data is null)
            p.SendInfoMessage("[SSC] Your player data has been reset to the server's default new character template");
    }
    
    private void SavePlayers(PlayerDataService playerDataService, Player[] players)
    {
        foreach (var p in players)
            SavePlayer(playerDataService, p);
    }

    private void SavePlayer(PlayerDataService playerDataService, Player player)
    {
        if (!player.active) 
            return;
        
        var id = GetPlayerId(player);
        if (!_invalidPlayers.Contains(id))
        {
            var playtime = TimeSpan.Zero;
            if (_additionalPlayerData.TryGetValue(id, out var data))
                playtime = data.Total;
                
            playerDataService.Save(id, player, playtime);
        }
    }
    
    private static void DeletePlayer(PlayerDataService playerDataService, Player player)
    {
        var id = GetPlayerId(player);
        playerDataService.Delete(id);
    }

    private static string GetPlayerId(RemoteClient client, string playerName) => client.Socket.GetRemoteAddress() switch
    {
        SteamAddress a => $"steam_{a.SteamId}_{playerName}",
        //WeGameAddress a => $"wegame_{a.rail_id}_{playerName}",
        _ => string.IsNullOrEmpty(client.ClientUUID) ? PlayerStore.UnknownPlayerId : $"client_{client.ClientUUID}_{playerName}"
    };

    private static string GetPlayerId(Player p)
    {
        var client = Netplay.Clients[p.whoAmI];

        return GetPlayerId(client, p.name);
    }

    private static void SyncPlayer(Player p)
    {
        var idx = p.whoAmI;
        
        SendData(MessageID.SyncPlayer, idx, number: idx);
        SendData(MessageID.PlayerLifeMana, idx, number: idx);
        SendData(MessageID.Unknown42, idx, number: idx); // Mana
        SendData(MessageID.PlayerBuffs, idx, number: idx);
        SendData(MessageID.SyncLoadout, idx, number2: p.CurrentLoadoutIndex);
        SendData(MessageID.QuestsCountSync, idx, number: idx);
        SendData(MessageID.TeamChange, idx, number: idx);
        SendAllItems(p);
        
        return;

        static void SendAllItems(Player p)
        {
            SendItems(p.inventory, PlayerItemSlotID.Inventory0);
            SendItems(p.armor, PlayerItemSlotID.Armor0);
            SendItems(p.dye, PlayerItemSlotID.Dye0);
            SendItems(p.miscEquips, PlayerItemSlotID.Misc0);
            SendItems(p.miscDyes, PlayerItemSlotID.MiscDye0);
            SendItems(p.bank.item, PlayerItemSlotID.Bank1_0);
            SendItems(p.bank2.item, PlayerItemSlotID.Bank2_0);
            SendItem(PlayerItemSlotID.TrashItem);
            SendItems(p.bank3.item, PlayerItemSlotID.Bank3_0);
            SendItems(p.bank4.item, PlayerItemSlotID.Bank4_0);
            SendLoadouts(p);
            return;

            void SendLoadouts(Player p)
            {
                for (var i = 0; i < p.Loadouts.Length; i++)
                {
                    var (armorSlot, dyeSlot) = i switch
                    {
                        0 => (PlayerItemSlotID.Loadout1_Armor_0, PlayerItemSlotID.Loadout1_Dye_0),
                        1 => (PlayerItemSlotID.Loadout2_Armor_0, PlayerItemSlotID.Loadout2_Dye_0),
                        2 => (PlayerItemSlotID.Loadout3_Armor_0, PlayerItemSlotID.Loadout3_Dye_0),
                        _ => throw new ArgumentOutOfRangeException(nameof(i), i, "Invalid loadout index")
                    };
                    var loadout = p.Loadouts[i];
                    SendItems(loadout.Armor, armorSlot);
                    SendItems(loadout.Dye, dyeSlot);
                }
            }

            void SendItems(Item[] items, int startIndex)
            {
                for (var slot = startIndex; slot < items.Length + startIndex; slot++)
                    SendItem(slot);
            }

            void SendItem(int slotId) => SendData(MessageID.SyncEquipment, p.whoAmI, number: p.whoAmI, number2: slotId);
        }

        static void SendData(int messageId, int playerIndex, int number = 0, int number2 = 0) => 
            NetMessage.SendData(messageId, remoteClient: playerIndex, number: number, number2: number2);
    }

    public void Dispose() => UnregisterHooks();
}