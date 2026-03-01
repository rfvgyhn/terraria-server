using System.Buffers;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Terraria;
using Terraria.Chat;
using Terraria.ID;
using Terraria.Localization;

namespace TerrariaServerModded;

[SupportedOSPlatform("linux")]
public class CommandListener(
    ConsoleInterceptor console,
    string socketDir,
    Encoding encoding,
    ILogger<CommandListener> log) : BackgroundService
{
    private readonly string _socketPath = Path.Combine(socketDir, "terraria.sock");
    private static readonly byte[] TrueResponse = "true\n"u8.ToArray();
    private static readonly byte[] FalseResponse = "false\n"u8.ToArray();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (Path.GetDirectoryName(_socketPath) is { } dir)
            Directory.CreateDirectory(dir);

        if (File.Exists(_socketPath))
            File.Delete(_socketPath);

        using var server = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        server.Bind(new UnixDomainSocketEndPoint(_socketPath));
        server.Listen(5);
        File.SetUnixFileMode(_socketPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite);
        log.LogInformation("Listening for commands on {SocketPath}", _socketPath);
        await using var reg = stoppingToken.Register(s => ((Socket?)s)?.Dispose(), server);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var client = await server.AcceptAsync(stoppingToken);
                _ = HandleClient(client, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected shutdown
        }
        catch (Exception e) when (e is SocketException)
        {
            log.LogError(e, "Socket error. Socket may have been closed externally.");
        }
        finally
        {
            if (File.Exists(_socketPath))
                File.Delete(_socketPath);
        }
    }

    private async Task HandleClient(Socket client, CancellationToken ct)
    {
        using var _ = client;
        var buffer = ArrayPool<byte>.Shared.Rent(512);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            int received;
            var totalReceived = 0;
            do
            {
                received = await client.ReceiveAsync(buffer.AsMemory(totalReceived), SocketFlags.None, cts.Token);
                totalReceived += received;
            } while (received > 0 && totalReceived < buffer.Length);

            if (totalReceived > 0)
            {
                var message = buffer.AsSpan(0, totalReceived).Trim(" \t\r\n\v\f"u8);

                if (TryHandleMessage(message, out var response) && response.Length > 0)
                    await client.SendAsync(response, SocketFlags.None, cts.Token);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private bool TryHandleMessage(ReadOnlySpan<byte> message, out ReadOnlyMemory<byte> response)
    {
        if (Ascii.EqualsIgnoreCase("isidle"u8, message))
        {
            response = ServerIsIdle() ? TrueResponse : FalseResponse;
            return true;
        }

        if (Ascii.EqualsIgnoreCase("gamemode"u8, message[..8]))
        {
            response = GameMode(message);
            return true;
        }

        console.QueueInput(encoding.GetString(message));
        response = default;
        return true;
    }

    private static bool ServerIsIdle()
    {
        foreach (var p in Main.player)
        {
            if (p.active)
                return false;
        }

        return true;
    }

    private byte[] GameMode(ReadOnlySpan<byte> message)
    {
        var firstSpace = message.Trim(" \t\r\n\v\f"u8).IndexOf((byte)' ');

        var arg = firstSpace > 0
            ? encoding.GetString(message[(firstSpace + 1)..].Trim(" \t\r\n\v\f"u8)).ToLowerInvariant()
            : "";
        var mode = arg switch
        {
            "classic" or "normal" or "0" => GameModeID.Normal,
            "expert" or "1" => GameModeID.Expert,
            "master" or "2" => GameModeID.Master,
            "journey" or "creative" or "3" => GameModeID.Creative,
            _ => Main.GameMode
        };
        var text = ModeStr(mode);
        if (mode != Main.GameMode)
        {
            Main.GameMode = mode;
            ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral($"World mode set to {text}"), new(0, 255, 255));
            NetMessage.SendData(MessageID.WorldData);
        }

        return encoding.GetBytes(text);
        
        static string ModeStr(int mode) => mode switch
        {
            GameModeID.Normal => "classic",
            GameModeID.Expert => "expert",
            GameModeID.Master => "master",
            GameModeID.Creative => "journey",
            _ => "unknown"
        };
    }
}