using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Terraria;

namespace TerrariaServerModded;

public class PlayerDataService(bool persistTeam, PlayerStore store, ILogger<PlayerDataService> logger) : BackgroundService
{
    private readonly Channel<(string id, ServerPlayerData? data)> _queue =
        Channel.CreateUnbounded<(string id, ServerPlayerData? data)>();

    public void Delete(string playerId) => _queue.Writer.TryWrite((playerId, null));
    
    public void Save(string playerId, Player p, TimeSpan totalPlayTime) =>
        _queue.Writer.TryWrite((playerId, ServerPlayerData.FromPlayer(p, persistTeam, totalPlayTime)));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // ReSharper disable once MethodSupportsCancellation
#pragma warning disable CA2016
        await foreach (var (id, player) in _queue.Reader.ReadAllAsync())
#pragma warning restore CA2016
        {
            if (player == null)
                DeletePlayerData(id);
            else
                await SavePlayerData(id, player);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _queue.Writer.Complete();
        await base.StopAsync(cancellationToken);
    }

    private void DeletePlayerData(string id)
    {
        try
        {
            logger.LogInformation("Deleting player data: {PlayerId}", id);
            store.Delete(id);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to delete player data");
        }
    }

    private async Task SavePlayerData(string id, ServerPlayerData player)
    {
        try
        {
            logger.LogInformation("Saving player data: {PlayerId}", id);
            await store.Save(id, player);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to save player data");
        }
    }
}