using System.Buffers;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Unicode;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TerrariaServerModded.Cli;
using TerrariaServerModded.Extensions;

namespace TerrariaServerModded;

[SupportedOSPlatform("linux")]
public class CommandListener(
    ConsoleInterceptor console,
    string socketDir,
    Encoding encoding,
    ILogger<CommandListener> log) : BackgroundService
{
    private readonly string _socketPath = Path.Combine(socketDir, "terraria.sock");

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
                var message = buffer.AsSpan(0, totalReceived).TrimWhitespace();

                if (TryHandleMessage(message, out var response) && response.Length > 0)
                {
                    await client.SendAsync(response, SocketFlags.None, cts.Token);
                    await client.SendAsync(encoding.GetBytes(Environment.NewLine), SocketFlags.None, cts.Token);
                }
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
        var buffer = message.Length <= 1024 ? stackalloc char[message.Length] : new char[message.Length];
        if (Utf8.ToUtf16(message, buffer, out _, out var charsWritten) != OperationStatus.Done)
        {
            response = ReadOnlyMemory<byte>.Empty;
            return false;
        }
        
        var cmdToRun = CliCommandProcessor.TryHandle(buffer[..charsWritten], out response);
        if (!cmdToRun.IsEmpty)
            console.QueueInput(cmdToRun.ToString(), true);
        
        return true;
    }
}