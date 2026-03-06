using System.Collections.Concurrent;
using On.Terraria;

namespace TerrariaServerModded;

public class ConsoleInterceptor : TextReader
{
    private readonly BlockingCollection<(string text, bool handled)> _queue = new();
    private readonly TextReader _original;
    private readonly CancellationTokenSource _exitCts;
    private readonly CancellationToken _exitToken;
    private int _disposed;
    
    public event EventHandler<ConsoleInterceptorInputEventArgs>? InputReceived;

    public ConsoleInterceptor(TextReader original, CancellationToken stoppingToken = default)
    {
        _original = original;
        _exitCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        _exitToken = _exitCts.Token;
        On.Terraria.Main.ReadLineInput += OnReadLineInput;
        _ = Task.Run(() => InputPump(_exitToken), stoppingToken);
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
        catch (ObjectDisposedException) { }
    }

    public void QueueInput(string text, bool handled = false)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;

        try
        {
            _queue.Add((text, handled), _exitToken);
        }
        catch (InvalidOperationException) { } // Handles object disposed and queue already completed 
    }

    public override string? ReadLine()
    {
        try
        {
            var result = _queue.Take(_exitToken);
            var args = new ConsoleInterceptorInputEventArgs(result.text);
            if (!result.handled)
                InputReceived?.Invoke(this, args);

            return args.Response;

        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (InvalidOperationException) // Handles object disposed and empty queue already completed 
        {
            return null;
        }
    }
    
    protected override void Dispose(bool disposing)
    {
        if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            On.Terraria.Main.ReadLineInput -= OnReadLineInput;
            _queue.CompleteAdding();
            _exitCts.Cancel();
            _queue.Dispose();
            _exitCts.Dispose();
        }
        
        base.Dispose(disposing);
    }
}

public record ConsoleInterceptorInputEventArgs(string Input)
{
    public string? Response { get; set; } = Input;
}