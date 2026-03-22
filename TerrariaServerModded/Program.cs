using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using ConsoleAppFramework;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Xna.Framework;
using Terraria.Chat;
using Terraria.ID;
using Terraria.Localization;
using TerrariaServerModded.Cli;

namespace TerrariaServerModded;

public static partial class Program
{
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(30);
    public static async Task Main(string[] args)
    {
        ConsoleApp.Timeout = ShutdownTimeout;
        await ConsoleApp.RunAsync(args, Run);
    }

    /// <param name="dataPath">Directory where server data is stored</param>
    /// <param name="difficulty">Difficulty to use for new players</param>
    /// <param name="noCompress">Do not compress save files</param>
    /// <param name="noTeamSave">Do not save player's team</param>
    /// <param name="backupCount">Number of backups to keep per player</param>
    /// <param name="verbose">Enable verbose logging</param>
    /// <param name="dryRun">Do not start the server</param>
    /// <param name="socketDir">Directory to use for Unix domain sockets</param>
    /// <param name="statusReportInterval">Interval, in milliseconds, to report world loading status. 0 to disable reporting</param>
    private static async Task Run(
        ConsoleAppContext context,
        string dataPath = "~/.local/share/Terraria",
        byte difficulty = PlayerDifficultyID.SoftCore,
        bool noCompress = false,
        bool noTeamSave = false,
        bool verbose = false,
        int backupCount = 5,
        bool dryRun = false,
        string socketDir = "/run/terraria-server",
        int statusReportInterval = 0,
        CancellationToken ct = default)
    {
        var logFactory = CreateLogger(verbose);
        var log = logFactory.CreateLogger(typeof(Program));
        var version = typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        log.LogInformation("Modded Terraria Server - v{Version}", version);
        log.LogInformation("Terraria Server - v{Version}", Terraria.Main.assemblyVersionNumber);

        var (fullDataPath, terrariaArgs) = PrepareArgs(context, dataPath, log);
        var saveRoot = InitSaveRoot(fullDataPath);
        var playerStore = new PlayerStore(saveRoot, !noCompress, backupCount, logFactory.CreateLogger<PlayerStore>());
        var playerDataService = new PlayerDataService(!noTeamSave, playerStore, logFactory.CreateLogger<PlayerDataService>());
        using var console = new ConsoleInterceptor(Console.In, ct);
        console.InputReceived += (_, args) =>
        {
            if (CliCommandProcessor.HandleConsoleInput(args.Input) is { IsEmpty: false } response)
                args.Response = response.ToString();
        };
        var statusTextChannel = Channel.CreateBounded<string>(new BoundedChannelOptions(10)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = true,
            SingleReader = true,
        });
        var statusSocketDir = Path.Combine(socketDir, "status");
        using var serverMonitor = new ServerMonitor(difficulty, playerStore, playerDataService, statusTextChannel.Writer, logFactory.CreateLogger<ServerMonitor>());
        using var statusReporter = OperatingSystem.IsLinux() && statusReportInterval > 0 ? new StatusReporter(statusSocketDir, statusTextChannel.Reader, TimeSpan.FromMilliseconds(statusReportInterval), logFactory.CreateLogger<StatusReporter>()) : null;
        var commandListener = OperatingSystem.IsLinux() ? new CommandListener(console, socketDir, Encoding.UTF8, logFactory.CreateLogger<CommandListener>()) : null;

#pragma warning disable CA1416
        await playerDataService.StartAsync(ct);
        await (commandListener?.StartAsync(ct) ?? Task.CompletedTask);
        await (statusReporter?.StartAsync(ct) ?? Task.CompletedTask);
#pragma warning restore CA1416
        if (ct.IsCancellationRequested)
            return;
        
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = (Exception)e.ExceptionObject;
            log.LogError(ex, "Critical Error (Terminating: {IsTerminating})", e.IsTerminating);
            using var cts = new CancellationTokenSource(ShutdownTimeout);

            try
            {
                playerDataService.StopAsync(cts.Token).GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                log.LogError(exception, "Failed to stop player data service");
            }
        };

        await using var shutdownReg = ct.Register(RequestShutdown);
        try
        {
            if (!dryRun)
                Terraria.WindowsLaunch.Main(terrariaArgs);
            log.LogInformation("Server shutting down");
        }
        catch (Exception e)
        {
            log.LogError(e, "Server crashed");
        }
        
        statusTextChannel.Writer.TryComplete();
        
        await Task.WhenAll(
#pragma warning disable CA1416
            commandListener?.StopAsync(ct) ?? Task.CompletedTask,
            playerDataService.StopAsync(ct),
            statusReporter?.StopAsync(ct) ?? Task.CompletedTask
#pragma warning restore CA1416
        );
    }

    private static (string, string[]) PrepareArgs(ConsoleAppContext context, string dataPath, ILogger log)
    {
        dataPath = Path.GetFullPath(ExpandEnvVars(dataPath));
        var terrariaArgs = context.EscapedArguments.Contains<string>("-config") 
            ? context.EscapedArguments.ToArray()
            : [ "-config", Path.Combine(dataPath, "serverconfig.txt") ];
        log.LogDebug(
            """
            Args: {Args}
            DataPath: {DataPath}
            TerrariaArgs: {TerrariaArgs}
            """, string.Join(" ", context.Arguments), dataPath, string.Join(" ", terrariaArgs));
        return (dataPath, terrariaArgs);
    }

    private static string ExpandEnvVars(string path)
    {
        // Platform checks needed until corefx supports platform-specific vars
        // https://github.com/dotnet/corefx/issues/28890
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return Environment.ExpandEnvironmentVariables(path);
        
        var p = UnixVars().Replace(path, "%$1%").Replace("~", "%HOME%");
        return Environment.ExpandEnvironmentVariables(p);
    }

    private static string InitSaveRoot(string dataPath)
    {
        var saveRoot = Path.Combine(dataPath, "Players");
        Directory.CreateDirectory(saveRoot);
        return saveRoot;
    }

    private static ILoggerFactory CreateLogger(bool verbose) =>
        LoggerFactory.Create(builder => builder
            .AddFilter<ConsoleLoggerProvider>(null, verbose ? LogLevel.Trace : LogLevel.Information)
            .AddSimpleConsole());

    private static void RequestShutdown()
    {
        ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral("Server is shutting down"), new Color(255, 240, 20));
        Terraria.Netplay.Disconnect = true;
    }

    [GeneratedRegex(@"\$(\w+)")]
    private static partial Regex UnixVars();
}