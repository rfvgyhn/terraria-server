namespace TerrariaServerModded;

public class AdditionalPlayerData(Playtime playTime, bool pendingSpawn)
{
    public Playtime PlayTime { get; } = playTime;
    public bool PendingSpawn { get; set; } = pendingSpawn;
}