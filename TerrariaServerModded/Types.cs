using System.Diagnostics;
using Microsoft.Xna.Framework;
using Terraria;
using TerrariaServerModded.Extensions;

namespace TerrariaServerModded;

[Flags]
public enum PlayerFlags : uint
{
    None = 0,
    AteArtisanBread = 1u << 0,
    DownedDd2EventAnyDifficulty = 1u << 1,
    EnabledSuperCart = 1u << 2,
    ExtraAccessory = 1u << 3,
    HappyFunTorchTime = 1u << 4,
    UnlockedBiomeTorches = 1u << 5,
    UnlockedSuperCart = 1u << 6,
    UsedAegisCrystal = 1u << 7,
    UsedAegisFruit = 1u << 8,
    UsedAmbrosia = 1u << 9,
    UsedArcaneCrystal = 1u << 10,
    UsedGalaxyPearl = 1u << 11,
    UsedGummyWorm = 1u << 12,
    UsingBiomeTorches = 1u << 13
}

public record struct ItemData(int Id, int Stack, int Prefix, bool Favorite);

public record VisualData(
    Color EyeColor,
    int Hair,
    Color HairColor,
    byte HairDye,
    uint HiddenAccessories,
    byte HiddenMisc,
    Color PantsColor,
    Color ShirtColor,
    Color ShoeColor,
    Color SkinColor,
    int SkinVariant,
    Color UnderShirtColor,
    int VoiceVariant,
    float VoicePitchOffset
);

public readonly record struct PlayerStats(
    int AnglerQuestsFinished,
    int DeathsPve,
    int DeathsPvp,
    int GolfScore,
    int Hp,
    int Mana,
    int MaxHp,
    int MaxMana,
    int RespawnTimer,
    int TaxMoney,
    int Team,
    TimeSpan PlayTime
);

public record Loadout(ItemData[] Armor, ItemData[] Dye, uint HiddenAccessories);

public readonly record struct Playtime
{
    private readonly long _sessionStart;
    private readonly TimeSpan _previousTotal;

    public Playtime(long sessionStart, TimeSpan previousTotal)
    {
        _sessionStart = sessionStart;
        _previousTotal = previousTotal;
    }

    public TimeSpan Total => _previousTotal + Stopwatch.GetElapsedTime(_sessionStart);

    public void Deconstruct(out long sessionStart, out TimeSpan previousTotal)
    {
        sessionStart = _sessionStart;
        previousTotal = _previousTotal;
    }
};

public record ServerPlayerData(
    string Name,
    int SpawnX,
    int SpawnY,
    int CurrentLoadoutIndex,
    PlayerStats Stats,
    VisualData Visuals,
    PlayerFlags Flags,
    ItemData[] Inventory,
    ItemData[] Armor,
    ItemData[] Dye,
    ItemData[] MiscEquip,
    ItemData[] MiscDye,
    ItemData[] Bank1,
    ItemData[] Bank2,
    ItemData[] Bank3,
    ItemData[] Bank4,
    byte VoidVaultInfo,
    Loadout[] Loadouts,
    ItemData TrashItem
)
{
    public void ApplyTo(Player p)
    {
        p.SpawnX = SpawnX;
        p.SpawnY = SpawnY;
        p.CurrentLoadoutIndex = CurrentLoadoutIndex;
        p.voidVaultInfo = VoidVaultInfo;

        MapStats(p, Stats);
        MapFlags(p, Flags);
        MapAllItems();
        MapVisuals(p, Visuals);

        return;

        void MapAllItems()
        {
            MapItem(TrashItem, p.trashItem);
            MapItems(Inventory, p.inventory);
            MapItems(Armor, p.armor);
            MapItems(Dye, p.dye);
            MapItems(MiscEquip, p.miscEquips);
            MapItems(MiscDye, p.miscDyes);
            MapItems(Bank1, p.bank.item);
            MapItems(Bank2, p.bank2.item);
            MapItems(Bank3, p.bank3.item);
            MapItems(Bank4, p.bank4.item);
            MapLoadouts(Loadouts, p.Loadouts);
        }

        static void MapItems(ReadOnlyMemory<ItemData> source, Item[] dest)
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual(source.Length, dest.Length);

            var span = source.Span;
            for (var i = 0; i < span.Length; i++)
                MapItem(span[i], dest[i]);
        }

        static void MapItem(ItemData source, Item dest)
        {
            dest.SetDefaults(source.Id);
            dest.Prefix(source.Prefix);
            dest.stack = source.Stack;
            dest.favorited = source.Favorite;
        }

        static void MapStats(Player p, PlayerStats data)
        {
            p.anglerQuestsFinished = data.AnglerQuestsFinished;
            p.dead = data.RespawnTimer > 0;
            p.golferScoreAccumulated = data.GolfScore;
            p.numberOfDeathsPVE = data.DeathsPve;
            p.numberOfDeathsPVP = data.DeathsPvp;
            p.respawnTimer = data.RespawnTimer;
            p.statLife = data.Hp;
            p.statLifeMax = data.MaxHp;
            p.statMana = data.Mana;
            p.statManaMax = data.MaxMana;
            p.taxMoney = data.TaxMoney;
            p.team = data.Team;
        }

        static void MapFlags(Player p, PlayerFlags data)
        {
            p.ateArtisanBread = data.HasFlag(PlayerFlags.AteArtisanBread);
            p.downedDD2EventAnyDifficulty = data.HasFlag(PlayerFlags.DownedDd2EventAnyDifficulty);
            p.enabledSuperCart = data.HasFlag(PlayerFlags.EnabledSuperCart);
            p.extraAccessory = data.HasFlag(PlayerFlags.ExtraAccessory);
            p.happyFunTorchTime = data.HasFlag(PlayerFlags.HappyFunTorchTime);
            p.unlockedBiomeTorches = data.HasFlag(PlayerFlags.UnlockedBiomeTorches);
            p.unlockedSuperCart = data.HasFlag(PlayerFlags.UnlockedSuperCart);
            p.usedAegisCrystal = data.HasFlag(PlayerFlags.UsedAegisCrystal);
            p.usedAegisFruit = data.HasFlag(PlayerFlags.UsedAegisFruit);
            p.usedAmbrosia = data.HasFlag(PlayerFlags.UsedAmbrosia);
            p.usedArcaneCrystal = data.HasFlag(PlayerFlags.UsedArcaneCrystal);
            p.usedGalaxyPearl = data.HasFlag(PlayerFlags.UsedGalaxyPearl);
            p.usedGummyWorm = data.HasFlag(PlayerFlags.UsedGummyWorm);
            p.UsingBiomeTorches = data.HasFlag(PlayerFlags.UsingBiomeTorches);
        }

        static void MapVisuals(Player p, VisualData data)
        {
            p.eyeColor = data.EyeColor;
            p.hair = data.Hair;
            p.hairColor = data.HairColor;
            p.hairDye = data.HairDye;
            p.hideMisc = data.HiddenMisc;
            p.hideVisibleAccessory = data.HiddenAccessories.UnpackBools(p.hideVisibleAccessory.Length);
            p.pantsColor = data.PantsColor;
            p.shirtColor = data.ShirtColor;
            p.shoeColor = data.ShoeColor;
            p.skinColor = data.SkinColor;
            p.skinVariant = data.SkinVariant;
            p.underShirtColor = data.UnderShirtColor;
            p.voiceVariant = data.VoiceVariant;
            p.voicePitchOffset = data.VoicePitchOffset;
        }

        static void MapLoadouts(Loadout[] source, EquipmentLoadout[] dest)
        {
            for (var i = 0; i < dest.Length; i++)
            {
                if (i >= source.Length)
                    break;

                var sourceLoadout = source[i];
                var destLoadout = dest[i];
                MapItems(sourceLoadout.Armor, destLoadout.Armor);
                MapItems(sourceLoadout.Dye, destLoadout.Dye);
                destLoadout.Hide = sourceLoadout.HiddenAccessories.UnpackBools(destLoadout.Hide.Length);
            }
        }
    }

    public static ServerPlayerData FromPlayer(Player p, bool includeTeam, TimeSpan playTime)
    {
        const int defaultTeam = 0;
        var flags = PlayerFlags.None;
        if (p.ateArtisanBread) flags |= PlayerFlags.AteArtisanBread;
        if (p.downedDD2EventAnyDifficulty) flags |= PlayerFlags.DownedDd2EventAnyDifficulty;
        if (p.enabledSuperCart) flags |= PlayerFlags.EnabledSuperCart;
        if (p.extraAccessory) flags |= PlayerFlags.ExtraAccessory;
        if (p.happyFunTorchTime) flags |= PlayerFlags.HappyFunTorchTime;
        if (p.unlockedBiomeTorches) flags |= PlayerFlags.UnlockedBiomeTorches;
        if (p.unlockedSuperCart) flags |= PlayerFlags.UnlockedSuperCart;
        if (p.usedAegisCrystal) flags |= PlayerFlags.UsedAegisCrystal;
        if (p.usedAegisFruit) flags |= PlayerFlags.UsedAegisFruit;
        if (p.usedAmbrosia) flags |= PlayerFlags.UsedAmbrosia;
        if (p.usedArcaneCrystal) flags |= PlayerFlags.UsedArcaneCrystal;
        if (p.usedGalaxyPearl) flags |= PlayerFlags.UsedGalaxyPearl;
        if (p.usedGummyWorm) flags |= PlayerFlags.UsedGummyWorm;
        if (p.UsingBiomeTorches) flags |= PlayerFlags.UsingBiomeTorches;

        return new ServerPlayerData(
            Name: p.name,
            SpawnX: p.SpawnX,
            SpawnY: p.SpawnY,
            CurrentLoadoutIndex: p.CurrentLoadoutIndex,
            Stats: new(
                AnglerQuestsFinished: p.anglerQuestsFinished,
                DeathsPve: p.numberOfDeathsPVE,
                DeathsPvp: p.numberOfDeathsPVP,
                GolfScore: p.golferScoreAccumulated,
                Hp: p.statLife,
                Mana: p.statMana,
                MaxHp: p.statLifeMax,
                MaxMana: p.statManaMax,
                RespawnTimer: p.dead ? p.respawnTimer : 0,
                TaxMoney: p.taxMoney,
                Team: includeTeam ? p.team : defaultTeam,
                PlayTime: playTime
            ),
            Visuals: new(
                EyeColor: p.eyeColor,
                Hair: p.hair,
                HairColor: p.hairColor,
                HairDye: p.hairDye,
                HiddenAccessories: p.hideVisibleAccessory.PackBools(),
                HiddenMisc: p.hideMisc,
                PantsColor: p.pantsColor,
                ShirtColor: p.shirtColor,
                ShoeColor: p.shoeColor,
                SkinColor: p.skinColor,
                SkinVariant: p.skinVariant,
                UnderShirtColor: p.underShirtColor,
                VoiceVariant: p.voiceVariant,
                VoicePitchOffset: p.voicePitchOffset
            ),
            Flags: flags,
            Inventory: MapItems(p.inventory),
            Armor: MapItems(p.armor),
            Dye: MapItems(p.dye),
            MiscEquip: MapItems(p.miscEquips),
            MiscDye: MapItems(p.miscDyes),
            Bank1: MapItems(p.bank.item),
            Bank2: MapItems(p.bank2.item),
            Bank3: MapItems(p.bank3.item),
            Bank4: MapItems(p.bank4.item),
            VoidVaultInfo: p.voidVaultInfo,
            Loadouts: MapLoadouts(p.Loadouts),
            TrashItem: MapItem(p.trashItem)
        );

        static Loadout[] MapLoadouts(EquipmentLoadout[] source)
        {
            var result = new Loadout[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                result[i] = new(
                    Armor: MapItems(source[i].Armor),
                    Dye: MapItems(source[i].Dye),
                    HiddenAccessories: source[i].Hide.PackBools()
                );
            }

            return result;
        }

        static ItemData[] MapItems(Item[] source)
        {
            var buffer = new ItemData[source.Length];
            for (var i = 0; i < source.Length; i++)
                buffer[i] = MapItem(source[i]);

            return buffer;
        }

        static ItemData MapItem(Item source) => new(source.type, source.stack, source.prefix, source.favorited);
    }
}