using NetCraft.Config;

namespace NetCraft.Optimizations.Commands;

//Commands optimization module, covers core optimization point 2.9
//The actual optimization lands after the NetCraft.Commands subsystem refactor
//CommandNode is currently an abstract class to stay compatible with the existing LiteralCommandNode/ArgumentCommandNode derived types
//When the toggle is on, it uses the string.Intern + List lookup equivalent approach
public static class CommandsOptimizations
{
    public const string ModuleName = "Commands Optimization";
    public const string TargetSubsystem = "NetCraft.Commands";

    //Optimization point 2.9, brigadier CommandNode as readonly struct + ImmutableArray
    //When the toggle is on, CommandNode child lookups go through a List linear scan as struct indexing equivalent
    //CommandNode stays a class to avoid breaking the LiteralCommandNode/ArgumentCommandNode derived types
    public static bool IsCommandNodeStructEnabled => OptimizationFlags.CommandNodeStruct;

    //Optimization point 2.9, command string keys are interned with string.Intern
    //When the toggle is on, GetChild lookups by name go through the string.Intern path
    public static bool IsCommandStringInternEnabled => OptimizationFlags.CommandStringIntern;

    //IsOptimized checks whether both toggles are on to decide if Commands optimization is enabled
    public static bool IsOptimized => IsCommandNodeStructEnabled && IsCommandStringInternEnabled;

    //GetStats returns the Commands optimization stats for diagnostics
    public static CommandsOptimizationStats GetStats() => new(
        CommandNodeStruct: IsCommandNodeStructEnabled,
        CommandStringIntern: IsCommandStringInternEnabled,
        IsOptimized: IsOptimized);
}

//Commands optimization stats snapshot
public readonly record struct CommandsOptimizationStats(
    bool CommandNodeStruct,
    bool CommandStringIntern,
    bool IsOptimized);
