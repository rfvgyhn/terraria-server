namespace TerrariaServerModded.Cli;

public interface ICliCommand
{
    static abstract string Display { get; }
    static abstract string Description { get; }
    static abstract string Command { get; }

    /// <summary>
    /// Executes the command if it is known
    /// </summary>
    /// <param name="input">Command to execute</param>
    /// <param name="response">Command's response</param>
    /// <returns>A known Terraria command to run or empty when no other command should be run</returns>
    /// <remarks>
    /// Specifying a return value can be used to "fallback" to a Terraria command. Useful if you want to
    /// override the default command behavior.
    /// </remarks>
    static abstract ReadOnlySpan<char> TryExecute(ReadOnlySpan<char> input, out ReadOnlyMemory<byte> response);
}