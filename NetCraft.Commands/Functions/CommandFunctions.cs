namespace NetCraft.Commands.Functions;

//CommandFunctions non-generic static helpers of the function package, maps to the static methods of vanilla CommandFunction
//Static members of a C# generic interface carry type parameters that non-generic types like StringTemplate cannot reach, so they are split out here
public static class CommandFunctions
{
    //MaxCommandLineLength maximum length of a single command line
    public const int MaxCommandLineLength = 2_000_000;

    //CheckCommandLineLength line-length circuit breaker at 2 million characters; maps to vanilla checkCommandLineLength
    public static void CheckCommandLineLength(System.Text.StringBuilder line)
    {
        if (line.Length > MaxCommandLineLength)
        {
            var truncated = line.ToString(0, Math.Min(512, MaxCommandLineLength));
            throw new InvalidOperationException($"Command too long: {line.Length} characters, contents: {truncated}...");
        }
    }

    public static void CheckCommandLineLength(string line)
    {
        if (line.Length > MaxCommandLineLength)
        {
            var truncated = line[..Math.Min(512, MaxCommandLineLength)];
            throw new InvalidOperationException($"Command too long: {line.Length} characters, contents: {truncated}...");
        }
    }
}
