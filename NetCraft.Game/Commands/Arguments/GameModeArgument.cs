using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Suggestion;
using NetCraft.Game.Commands;
using NetCraft.Game.World.Level;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//GameModeArgument game mode argument, maps to vanilla net.minecraft.commands.arguments.GameModeArgument
//Parses the full or short game mode name, aligned with the /gamemode argument type (gameMode) network id
public sealed class GameModeArgument : ArgumentType<GameType>
{
    private static readonly IReadOnlyList<string> ExamplesList = new[] { "survival", "creative" };

    //The four built-in modes, iterated for parsing and suggestions
    private static readonly GameType[] Values =
        { GameType.Survival, GameType.Creative, GameType.Adventure, GameType.Spectator };

    public static readonly DynamicCommandExceptionType ErrorInvalidGameMode =
        new(name => new LiteralMessage($"unknown game mode {name}"));

    public static GameModeArgument GameMode() => new();

    public GameType Parse(StringReader reader)
    {
        var start = reader.Cursor;
        var name = reader.ReadString();
        var mode = GameType.ByName(name);
        if (mode is null)
        {
            reader.SetCursor(start);
            throw ErrorInvalidGameMode.CreateWithContext(reader, name);
        }
        return mode;
    }

    //GetGameMode gets the parsed game mode
    public static GameType GetGameMode(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<GameType>(name);

    //ListSuggestions suggests full and short mode names, maps to vanilla GameModeArgument suggestions
    public Task<Suggestions> ListSuggestions<S>(CommandContext<S> context, SuggestionsBuilder builder)
    {
        foreach (var mode in Values)
        {
            if (mode.Name.StartsWith(builder.RemainingLowerCase, StringComparison.Ordinal))
                builder.Add(mode.Name);
            else if (mode.ShortName.StartsWith(builder.RemainingLowerCase, StringComparison.Ordinal))
                builder.Add(mode.ShortName);
        }
        return builder.BuildFuture();
    }

    public IReadOnlyList<string> Examples => ExamplesList;
}
