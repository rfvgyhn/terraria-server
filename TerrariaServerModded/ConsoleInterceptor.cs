using System.Collections.Concurrent;
using On.Terraria;

namespace TerrariaServerModded;

public class ConsoleInterceptor : TextReader
{
    private readonly ConcurrentQueue<string> _queue = new();
    private readonly TextReader _original;
    private readonly CancellationToken _stoppingToken;
    private readonly SemaphoreSlim _signal = new(0);
    private readonly CancellationTokenSource _exitCts = new();
    private bool _disposed;

    public ConsoleInterceptor(TextReader original, CancellationToken stoppingToken = default)
    {
        _original = original;
        _stoppingToken = stoppingToken;
        On.Terraria.Main.ReadLineInput += OnReadLineInput;
        Task.Factory.StartNew(
            () => InputPump(stoppingToken), 
            stoppingToken, 
            TaskCreationOptions.LongRunning, 
            TaskScheduler.Default);
    }

    private string OnReadLineInput(Main.orig_ReadLineInput orig)
    {
        return ReadLine() ?? "";
    }

    private async Task InputPump(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var input = await _original.ReadLineAsync(ct);
                if (input is not null)
                    QueueInput(input);
            }
        }
        catch (OperationCanceledException) { }
    }

    public void QueueInput(string text)
    {
        _queue.Enqueue(text);
        _signal.Release();
    }

    public override string? ReadLine()
    {
        try
        {
            _signal.Wait(_stoppingToken);

            if (_disposed)
                return null;

            return _queue.TryDequeue(out var result) ? result : null;
        }
        catch (OperationCanceledException)
        {
            try
            {
                // Block here so Terraria Server Input Thread doesn't loop continuously on app cancellation
                _signal.Wait(_exitCts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            
            return null;
        }
        catch (ObjectDisposedException)
        {
            return null;
        }
    }
    
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _exitCts.Cancel();
            _exitCts.Dispose();
            On.Terraria.Main.ReadLineInput -= OnReadLineInput;
            _signal.Release(100); // Wake up any stuck ReadLine calls
            _signal.Dispose();
        }
        
        base.Dispose(disposing);
    }
}