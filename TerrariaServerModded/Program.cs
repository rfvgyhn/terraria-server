using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using ConsoleAppFramework;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Xna.Framework;
using Terraria.Chat;
using Terraria.ID;
using Terraria.Localization;

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
    /// <param name="socketDir">Directory to use for Unix domain socket</param>
    private static async Task Run(
        ConsoleAppContext context,
        string dataPath = "~/.local/share/Terraria",
        byte difficulty = PlayerDifficultyID.SoftCore,
        bool noCompress = false,
        bool noTeamSave = false,
        bool verbose = false,
        int backupCount = 5,
        bool dryRun = false,
        string socketDir = "/tmp",
        CancellationToken ct = default)
    {
        var logFactory = CreateLogger(verbose);
        var log = logFactory.CreateLogger(typeof(Program));
        var version = typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        log.LogInformation("Modded Terraria Server - v{Version}", version);

        var (fullDataPath, terrariaArgs) = PrepareArgs(context, dataPath, log);
        var saveRoot = InitSaveRoot(fullDataPath);
        var playerStore = new PlayerStore(saveRoot, !noCompress, backupCount, logFactory.CreateLogger<PlayerStore>());
        var playerDataService = new PlayerDataService(!noTeamSave, playerStore, logFactory.CreateLogger<PlayerDataService>());
        using var serverMonitor = new ServerMonitor(difficulty, playerStore, playerDataService, logFactory.CreateLogger<ServerMonitor>());
        using var console = new ConsoleInterceptor(Console.In, ct);
        var commandListener = OperatingSystem.IsLinux() ? new CommandListener(console, socketDir, Encoding.UTF8, logFactory.CreateLogger<CommandListener>()) : null;
        
        await playerDataService.StartAsync(ct);
        await (commandListener?.StartAsync(ct) ?? Task.CompletedTask);
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
        
        await Task.WhenAll(
            commandListener?.StopAsync(ct) ?? Task.CompletedTask,
            playerDataService.StopAsync(ct)
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