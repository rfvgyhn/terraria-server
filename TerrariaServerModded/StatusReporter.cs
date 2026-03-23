using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Text.Unicode;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Terraria;
using Terraria.Localization;
using TerrariaServerModded.Extensions;

namespace TerrariaServerModded;

[SupportedOSPlatform("linux")]
public sealed class StatusReporter(string socketDir, ChannelReader<string> statusText, TimeSpan reportInterval, ILogger<StatusReporter> log)
    : BackgroundService
{
    private enum State
    {
        None,
        LoadingWorld,
        GeneratingWorld,
        Running,
        Stopped
    }

    private static readonly string WorldLoadStep1 = Lang.gen[47].Value;
    private static readonly string RunningStep1 = Language.GetTextValue("CLI.ServerStarted");
    private static readonly float[] InitializeStepWeights = [.33f, .33f, .33f, .1f];
    private static readonly float[] GenerateWorldStepWeights = [.94f, .3f, .3f];
    private readonly string _socketDir = Path.Combine(socketDir, "status");
    private State _currentState = State.None;
    private State _lastReportedState = State.None;
    private float _lastReportedProgress;
    private int _currentStep;
    private string _lastStatus = "";
    private Socket? _socket;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        if (reportInterval <= TimeSpan.Zero)
        {
            log.LogInformation("Report interval '{Interval}' is not greater than zero. Status reporting disabled", reportInterval);
            return;
        }
            
        try
        {
            Directory.CreateDirectory(_socketDir);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed to create socket directory '{Path}'. Status reporting disabled.", _socketDir);
            return;
        }

        _socket ??= new Socket(AddressFamily.Unix, SocketType.Dgram, ProtocolType.Unspecified);
        _socket.SendTimeout = 500;
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        log.LogInformation("Sending status updates to {SocketPath}/*.sock", _socketDir);
        var lastSend = Stopwatch.GetTimestamp();

        try
        {
            await foreach (var status in statusText.ReadAllAsync(stoppingToken))
            {
                var progress = 0f;
                var str = status.AsSpan();

                if (str.Contains("Finalizing World", StringComparison.OrdinalIgnoreCase) || status == _lastStatus)
                    continue;

                if (str.StartsWith(WorldLoadStep1) || (_currentState == State.GeneratingWorld &&
                                                       _currentStep == GenerateWorldStepWeights.Length))
                {
                    _currentState = State.LoadingWorld;
                    _currentStep = 0;
                    _lastReportedProgress = 0;
                }
                else if (StartsWithPercent(str, out progress) && EndsWithPercentChar(str))
                    _currentState = State.GeneratingWorld;
                else if (str == RunningStep1)
                    _currentState = State.Running;
                else if (_currentState == State.Running)
                    _currentState = State.None;

                var stepProgress = 0f;
                switch (_currentState)
                {
                    case State.LoadingWorld:
                        if (str.EndsWith('%'))
                        {
                            var lastSpace = str.LastIndexOf(' ');
                            if (lastSpace > 0)
                            {
                                if (int.TryParse(str[(lastSpace + 1)..^1], out var percent))
                                    stepProgress = percent / 100f;

                                if (_lastReportedProgress > 0 && stepProgress == 0)
                                    continue;

                                if (_lastStatus != "" && !_lastStatus.StartsWith(str[..lastSpace]))
                                    _currentStep++;
                            }
                        }
                        else
                            _currentStep++;

                        if (_currentStep < InitializeStepWeights.Length)
                        {
                            progress = InitializeStepWeights.SumUpTo(_currentStep) +
                                       stepProgress * InitializeStepWeights[_currentStep];
                        }
                        else
                        {
                            progress = 1;
                            log.LogWarning($$"""Encountered additional {{nameof(State.LoadingWorld)}} step: {step}""",
                                status);
                        }
                        break;
                    case State.GeneratingWorld:
                        if (progress < 1 || StartsWithPercent(str, out _))
                            break;

                        if (str.EndsWith('%'))
                        {
                            var lastSpace = str.LastIndexOf(' ');
                            if (lastSpace > 0)
                            {
                                if (int.TryParse(str[(lastSpace + 1)..^1], out var percent))
                                    stepProgress = percent / 100f;

                                if (_lastReportedProgress > 0 && stepProgress == 0)
                                    continue;

                                if (_lastStatus != "" && !_lastStatus.StartsWith(str[..lastSpace]))
                                    _currentStep++;
                            }
                        }

                        if (_currentStep < GenerateWorldStepWeights.Length)
                        {
                            progress = GenerateWorldStepWeights.SumUpTo(_currentStep) +
                                       stepProgress * GenerateWorldStepWeights[_currentStep];
                        }
                        else
                        {
                            progress = 1;
                            log.LogWarning($$"""Encountered additional {{nameof(State.GeneratingWorld)}} step: {step}""",
                                status);
                        }
                        break;
                    case State.Running:
                    case State.Stopped:
                    case State.None:
                        progress = 0;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                _lastStatus = status;

                if ((Stopwatch.GetElapsedTime(lastSend) < reportInterval && progress > _lastReportedProgress)
                    || ((int)(progress * 100) == (int)(_lastReportedProgress * 100) &&
                        _currentState == _lastReportedState)
                    || (progress == 0 && _currentState == State.GeneratingWorld))
                {
                    continue;
                }

                lastSend = Stopwatch.GetTimestamp();
                _lastReportedProgress = progress;
                _lastReportedState = _currentState;

                if (_currentState != State.None)
                    ReportProgress(_currentState, progress);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        ReportProgress(State.Stopped);
        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _socket?.Dispose();
        _socket = null;
        base.Dispose();
    }

    private static bool EndsWithPercentChar(ReadOnlySpan<char> str) => str[^1] == '%';

    private static bool StartsWithPercent(ReadOnlySpan<char> str, out float percent)
    {
        percent = 0;
        for (var windowSize = 4; windowSize < 7; windowSize++)
        {
            if (windowSize > str.Length)
                continue;

            if (TryParsePercent(str[..windowSize], out percent))
                return true;
        }

        return false;
    }

    private static bool TryParsePercent(ReadOnlySpan<char> str, out float percent)
    {
        percent = 0;

        if (!EndsWithPercentChar(str) || !float.TryParse(str[..^1], out var value))
            return false;

        percent = value / 100f;
        return true;
    }

    private void ReportProgress(State state, float progress = 0)
    {
        if (_socket is null)
            return;

        Span<byte> buffer = stackalloc byte[32];
        if (!Utf8.TryWrite(buffer, $"{state}:{progress * 100:F0}\n", out var bytesWritten))
            return;

        _socket.Blocking = progress >= 1;
        var msg = buffer[..bytesWritten];
        try
        {
            foreach (var path in Directory.EnumerateFiles(_socketDir, "*.sock", SearchOption.TopDirectoryOnly))
            {
                var endpoint = new UnixDomainSocketEndPoint(path);
                _socket.SendTo(msg, SocketFlags.None, endpoint);
            }
        }
        catch (SocketException)
        {
            // swallow
        }
        catch (Exception e)
        {
            log.LogError(e, "Failed to send status update");
        }
    }
}