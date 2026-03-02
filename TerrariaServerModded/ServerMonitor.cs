using System.Buffers.Binary;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using On.Terraria.Chat.Commands;
using Terraria;
using Terraria.Chat;
using Terraria.ID;
using Terraria.Net;
using TerrariaServerModded.Extensions;
using ChatCommandProcessor = TerrariaServerModded.Chat.ChatCommandProcessor;

namespace TerrariaServerModded;

public sealed class ServerMonitor : IDisposable
{
    private readonly byte _playerDifficulty;
    private readonly PlayerStore _playerStore;
    private readonly PlayerDataService _playerDataService;
    private readonly ILogger<ServerMonitor> _log;
    private readonly Dictionary<string, AdditionalPlayerData> _additionalPlayerData = new(StringComparer.OrdinalIgnoreCase);

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
        On.Terraria.Chat.Commands.HelpCommand.ProcessIncomingMessage += OnHelpCommand;
        On.Terraria.IO.WorldFile.LoadWorld += OnWorldLoad;
        On.Terraria.IO.WorldFile.SaveWorld_bool_bool += OnWorldSave;
        On.Terraria.Initializers.ChatInitializer.Load += OnChatInitLoad;
        On.Terraria.Main.Initialize += OnInitialize;
        On.Terraria.MessageBuffer.GetData += OnGetData;
        On.Terraria.RemoteClient.Reset += OnClientDisconnect;
    }

    private void UnregisterHooks()
    {
        On.Terraria.Chat.Commands.HelpCommand.ProcessIncomingMessage -= OnHelpCommand;
        On.Terraria.IO.WorldFile.LoadWorld -= OnWorldLoad;
        On.Terraria.IO.WorldFile.SaveWorld_bool_bool -= OnWorldSave;
        On.Terraria.Initializers.ChatInitializer.Load -= OnChatInitLoad;
        On.Terraria.Main.Initialize -= OnInitialize;
        On.Terraria.MessageBuffer.GetData -= OnGetData;
        On.Terraria.RemoteClient.Reset -= OnClientDisconnect;
    }
    
    private static void OnHelpCommand(HelpCommand.orig_ProcessIncomingMessage orig, Terraria.Chat.Commands.HelpCommand self, string text, byte clientId)
    {
        orig(self, text, clientId);
        ChatCommandProcessor.ListToClient(clientId);
    }

    private static void OnChatInitLoad(On.Terraria.Initializers.ChatInitializer.orig_Load orig)
    {
        orig();
        ChatCommandProcessor.Init();
    }

    private void OnGetData(On.Terraria.MessageBuffer.orig_GetData orig, MessageBuffer msgBuffer, int start, int length,
        out int msgType)
    {
        var message = msgBuffer.readBuffer.AsSpan(start, length);
        msgType = message[0];
        message = message[1..];

        if (msgType == MessageID.PlayerSpawn)
        {
            // player Index + spawnX + spawnY + respawnTimer + deathsPve + deathsPvp + team
            const int contextOffset = 1 + 2 + 2 + 4 + 2 + 2 + 1;
            var player = Main.player[msgBuffer.whoAmI];
            var context = (PlayerSpawnContext)message[contextOffset];
            if (HandleSpawn(player, context, message[1..]))
                return;
            
            orig(msgBuffer, start, length, out msgType);
            if (context == PlayerSpawnContext.SpawningIntoWorld 
                && player.SpawnX > -1 
                && player.SpawnY > -1 
                && _additionalPlayerData.TryGet(player, out var data))
            {
                data.PendingSpawn = true;
            }
            return;
        }

        if (msgType == MessageID.PlayerControls)
        {
            var player = Main.player[msgBuffer.whoAmI];
            if (HandlePlayerControls(player, message[1..]))
                return;
        }

        if (msgType == MessageID.AnglerQuestFinished)
        {
            // Packet QuestsCountSync (76) isn't sent by client
            // TODO: probably need to manually handle golf score as well. Look into possible hooks to do so. Maybe packet 128
            Main.player[msgBuffer.whoAmI].anglerQuestsFinished++;
        }
        
        if (msgType == MessageID.NetModules && HandleNetModules(msgBuffer, start, length)) 
            return;

        orig(msgBuffer, start, length, out msgType);
    }

    /// <returns>True if the packet was handled</returns>
    private bool HandleSpawn(Player player, PlayerSpawnContext context, Span<byte> message)
    {
        if (context == PlayerSpawnContext.SpawningIntoWorld)
        {
            LoadPlayer(player, _playerStore);
            BinaryPrimitives.WriteInt16LittleEndian(message, (short)player.SpawnX);
            BinaryPrimitives.WriteInt16LittleEndian(message[2..], (short)player.SpawnY);
            BinaryPrimitives.WriteInt32LittleEndian(message[4..], player.respawnTimer);
            BinaryPrimitives.WriteInt16LittleEndian(message[8..], (short)player.numberOfDeathsPVE);
            BinaryPrimitives.WriteInt16LittleEndian(message[10..], (short)player.numberOfDeathsPVP);
            message[12] = (byte)player.team;
        }
        else if (context is PlayerSpawnContext.RecallFromItem or PlayerSpawnContext.ReviveFromDeath && player.SpawnX > -1 && player.SpawnY > -1)
        {
            if (Player.CheckSpawn(player.SpawnX, player.SpawnY))
            {
                var pos = Vector2.Subtract(new Point(player.SpawnX, player.SpawnY).ToWorldCoordinates(autoAddY: 0.0f), new Vector2(player.width / 2.0f, player.height));
                player.Teleport(pos);
                NetMessage.SendData(MessageID.TeleportEntity, number2: player.whoAmI, number3: pos.X, number4: pos.Y, number5: -1);
                NetMessage.SendData(MessageID.PlayerSpawn, ignoreClient: player.whoAmI, number: player.whoAmI, number2: (byte) context);
            }

            return true;
        }

        return false;
    }

    /// <returns>True if the packet was handled</returns>
    private bool HandlePlayerControls(Player player, Span<byte> message)
    {
        // message layout
        // 0 = byte  - Bits1
        // 1 = byte  - Bits2
        // 2 = byte  - Bits3
        //             0 = tryKeepingHoveringUp
        //             1 = IsVoidVaultEnabled;
        //             2 = sitting.isSitting;
        //             3 = downedDD2EventAnyDifficulty;
        // 3 = byte  - Bits4
        // 4 = byte  - Selected Item
        // 5 = float - position.x
        // 9 = float - position.y
        ref var bits3 = ref message[2];
        if (SetBit(ref bits3, 1, player.IsVoidVaultEnabled) || SetBit(ref bits3, 3, player.downedDD2EventAnyDifficulty))
            NetMessage.SendData(MessageID.PlayerControls, number: player.whoAmI, number2: bits3);

        if (_additionalPlayerData.TryGet(player, out var data) && data.PendingSpawn && Player.CheckSpawn(player.SpawnX, player.SpawnY))
        {
            data.PendingSpawn = false;
            var pos = Vector2.Subtract(new Point(player.SpawnX, player.SpawnY).ToWorldCoordinates(autoAddY: 0.0f), new Vector2(player.width / 2.0f, player.height));
            player.Teleport(pos);
            NetMessage.SendData(MessageID.TeleportEntity, number2: player.whoAmI, number3: pos.X, number4: pos.Y, number5: -1);
            BinaryPrimitives.WriteSingleLittleEndian(message[5..], player.position.X);
            BinaryPrimitives.WriteSingleLittleEndian(message[9..], player.position.Y);
        }
        
        return false;

        bool SetBit(ref byte b, byte offset, bool value)
        {
            var prev = b;
            var mask = 1 << offset;
                
            if (value)
                b = (byte)(b | mask);
            else
                b = (byte)(b & ~mask);

            return prev != b;
        }
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
        var p = Main.player[playerIndex];
        var text = msg.Text.AsSpan().Trim();
        _additionalPlayerData.TryGet(p, out var data);
        
        return ChatCommandProcessor.ExecuteIfMatches(p, data, text);
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
            if (_additionalPlayerData.TryRemove(player, out var data))
            {
                if (player.difficulty == PlayerDifficultyID.Hardcore && (player.dead || player.ghost))
                    DeletePlayer(_playerDataService, player);
                else
                    SavePlayer(_playerDataService, player, data);
            }
        }

        orig(remoteClient);
    }
    
    private void LoadPlayer(Player p, PlayerStore store)
    {
        var playerId = p.GetPlayerId();
        var isValid = true;
        _additionalPlayerData.Remove(playerId);

        if (!store.TryLoad(playerId, out var data))
        {
            _log.LogWarning("Player {Name} - {Id} will be forced to a new character until issue is resolved", p.name, playerId);
            isValid = false;
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
        
        if (isValid)
            _additionalPlayerData[playerId] = new(new Playtime(Stopwatch.GetTimestamp(), playtime), false);
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
            SavePlayer(playerDataService, p, null);
    }

    private void SavePlayer(PlayerDataService playerDataService, Player player, AdditionalPlayerData? data)
    {
        if (!player.active) 
            return;
        
        var id = player.GetPlayerId();
        if (data is null)
            _additionalPlayerData.TryGetValue(id, out data);
        
        var playtime = data?.PlayTime.Total ?? TimeSpan.Zero;
        playerDataService.Save(id, player, playtime);
    }
    
    private static void DeletePlayer(PlayerDataService playerDataService, Player player)
    {
        var id = player.GetPlayerId();
        playerDataService.Delete(id);
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