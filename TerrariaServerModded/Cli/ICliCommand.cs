namespace TerrariaServerModded.Cli;

public interface ICliCommand
{
    static abstract string Display { get; }
    static abstract string Description { get; }
    static abstract string Command { get; }
    static abstract bool TryExecute(ReadOnlySpan<char> input, out ReadOnlyMemory<byte> response);
}