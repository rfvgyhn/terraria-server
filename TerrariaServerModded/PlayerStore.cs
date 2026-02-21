using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace TerrariaServerModded;

public partial class PlayerStore(string baseDir, bool compress, int maxBackups, ILogger<PlayerStore> logger)
{
    public const string UnknownPlayerId = "unknown";

    private string GetPath(string playerId) =>
        Path.Combine(baseDir, WorldDirectory,
            compress ? $"{playerId.ToSafeFileName()}.json.gz" : $"{playerId.ToSafeFileName()}.json");

    public string WorldDirectory { private get; set; } = "";

    public bool TryLoad(string playerId, out ServerPlayerData? data)
    {
        data = null;
        var path = GetPath(playerId);
        if (!File.Exists(path))
            return true;

        try
        {
            using var fileStream = File.OpenRead(path);
            using Stream stream = compress ? new GZipStream(fileStream, CompressionMode.Decompress) : fileStream;
            data = JsonSerializer.Deserialize(stream, PlayerJsonContext.Default.ServerPlayerData);
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to load player data for {PlayerId}", playerId);
            return false;
        }
    }

    public void Delete(string playerId)
    {
        var path = GetPath(playerId);
        if (Path.GetDirectoryName(path) is not { } dir)
            return;

        foreach (var file in Directory.EnumerateFiles(dir, $"{playerId}.*", SearchOption.TopDirectoryOnly))
            File.Delete(file);
    }

    public async Task Save(string playerId, ServerPlayerData data, CancellationToken ct = default)
    {
        if (playerId == UnknownPlayerId)
        {
            logger.LogDebug("Skipping save for player with unknown ID: {Name}", data.Name);
            return;
        }

        var path = GetPath(playerId);
        if (Path.GetDirectoryName(path) is { } dir)
            Directory.CreateDirectory(dir);

        if (maxBackups > 0 && File.Exists(path))
        {
            for (var i = maxBackups - 1; i >= 1; i--)
            {
                var source = $"{path}.bak{i}";
                var destination = $"{path}.bak{i + 1}";
                if (File.Exists(source))
                    File.Move(source, destination, true);
            }

            File.Move(path, $"{path}.bak1", true);
        }

        await using var fileStream = new FileStream(path, new FileStreamOptions
        {
            Access = FileAccess.Write,
            Mode = FileMode.Create,
            Options = FileOptions.Asynchronous
        });
        await using Stream stream = compress ? new GZipStream(fileStream, CompressionLevel.Optimal) : fileStream;
        await JsonSerializer.SerializeAsync(stream, data, PlayerJsonContext.Default.ServerPlayerData, ct);
    }

    [JsonSerializable(typeof(ServerPlayerData))]
    [JsonSourceGenerationOptions(Converters = [typeof(ColorJsonConverter)])]
    private partial class PlayerJsonContext : JsonSerializerContext;
}