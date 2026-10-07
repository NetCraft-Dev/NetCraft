using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Suggestion;
using NetCraft.Game.Commands;
using NetCraft.Game.Server;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//EntityArgument entity argument, maps to vanilla net.minecraft.commands.arguments.EntityArgument
//Four factories entity/entities/player/players cover the single/multiple and entity/player dimensions
//At execution time the parsed selector is resolved against the command source into a target set
public sealed class EntityArgument(bool single, bool playersOnly) : ArgumentType<EntitySelector>
{
    private static readonly IReadOnlyList<string> ExamplesList = new[]
        { "Player", "0123", "@e", "@e[type=foo]", "dd12be42-52a9-4a91-a8a1-11c01849e498" };

    //The six selector prefixes; suggestions filter by the already-typed prefix
    private static readonly string[] SelectorPrefixes = { "@a", "@e", "@n", "@p", "@r", "@s" };

    public static readonly SimpleCommandExceptionType ErrorNotSingleEntity =
        new(new TranslatableMessage("argument.entity.toomany"));
    public static readonly SimpleCommandExceptionType ErrorNotSinglePlayer =
        new(new TranslatableMessage("argument.player.toomany"));
    public static readonly SimpleCommandExceptionType ErrorOnlyPlayersAllowed =
        new(new TranslatableMessage("argument.player.entities"));
    public static readonly SimpleCommandExceptionType NoEntitiesFound =
        new(new TranslatableMessage("argument.entity.notfound.entity"));
    public static readonly SimpleCommandExceptionType NoPlayersFound =
        new(new TranslatableMessage("argument.entity.notfound.player"));

    //Entity single entity argument
    public static EntityArgument Entity() => new(true, false);

    //Entities multiple entities argument
    public static EntityArgument Entities() => new(false, false);

    //Player single player argument
    public static EntityArgument Player() => new(true, true);

    //Players multiple players argument
    public static EntityArgument Players() => new(false, true);

    //Single whether it is a single target, used for network serialization
    public bool Single { get; } = single;

    //PlayersOnly whether only players are targeted, used for network serialization
    public bool PlayersOnly { get; } = playersOnly;

    //Parse parses the selector and validates single/multiple and the player restriction by the argument dimension; rolls back to the argument start on failure
    public EntitySelector Parse(StringReader reader)
    {
        var start = reader.Cursor;
        //Player command sources always pass the selector permission
        var parser = new EntitySelectorParser(reader, allowSelectors: true);
        var selector = parser.Parse();
        if (selector.MaxResults > 1 && single)
        {
            reader.SetCursor(start);
            throw (playersOnly ? ErrorNotSinglePlayer : ErrorNotSingleEntity).CreateWithContext(reader);
        }
        if (selector.IncludesEntities && playersOnly && !selector.IsSelfSelector)
        {
            reader.SetCursor(start);
            throw ErrorOnlyPlayersAllowed.CreateWithContext(reader);
        }
        return selector;
    }

    //GetEntity gets a single entity target; tp supports players only here, so a non-player hit is reported with vanilla's players-only error
    public static ServerPlayer GetEntity(CommandContext<CommandSourceStack> context, string name)
    {
        var target = context.GetArgument<EntitySelector>(name).FindSingleEntity(GetSource(context));
        return target.Player ?? throw ErrorOnlyPlayersAllowed.Create();
    }

    //GetSingleTarget gets one entity target; may return either a player or a level entity, maps to vanilla EntityArgument.getEntity
    //The existing GetEntity only gives players; commands such as /data that operate on arbitrary entities go through this
    public static CommandTarget GetSingleTarget(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<EntitySelector>(name).FindSingleEntity(GetSource(context));

    //GetEntities gets multiple entity targets, including players and level entities; an empty result throws NO_ENTITIES_FOUND
    public static IReadOnlyList<CommandTarget> GetEntities(CommandContext<CommandSourceStack> context, string name)
    {
        var result = GetOptionalEntities(context, name);
        if (result.Count == 0)
            throw NoEntitiesFound.Create();
        return result;
    }

    //GetOptionalEntities gets multiple entity targets, allowing an empty result
    public static IReadOnlyList<CommandTarget> GetOptionalEntities(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<EntitySelector>(name).FindEntities(GetSource(context));

    //GetPlayer gets a single player target
    public static ServerPlayer GetPlayer(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<EntitySelector>(name).FindSinglePlayer(GetSource(context));

    //GetPlayers gets multiple player targets; an empty result throws NO_PLAYERS_FOUND
    public static IReadOnlyList<ServerPlayer> GetPlayers(CommandContext<CommandSourceStack> context, string name)
    {
        var players = context.GetArgument<EntitySelector>(name).FindPlayers(GetSource(context));
        if (players.Count == 0)
            throw NoPlayersFound.Create();
        return players;
    }

    //GetOptionalPlayers gets multiple player targets, allowing an empty result
    public static IReadOnlyList<ServerPlayer> GetOptionalPlayers(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<EntitySelector>(name).FindPlayers(GetSource(context));

    private static ServerCommandSource GetSource(CommandContext<CommandSourceStack> context)
        => (ServerCommandSource)context.GetSource();

    //ListSuggestions suggests selector type prefixes and online player names, maps to vanilla EntitySelectorParser suggestions
    //Empty input and input starting with @ both suggest selector types; the former additionally suggests online player names
    public Task<Suggestions> ListSuggestions<S>(CommandContext<S> context, SuggestionsBuilder builder)
    {
        var remaining = builder.Remaining;
        if (remaining.Length == 0 || remaining.StartsWith('@'))
        {
            foreach (var prefix in SelectorPrefixes)
                if (prefix.StartsWith(remaining, StringComparison.Ordinal))
                    builder.Add(prefix);
            if (remaining.Length > 0) return builder.BuildFuture();
        }
        if (context.GetSource() is ServerCommandSource source)
        {
            foreach (var player in source.Server.PlayerList.Players)
                if (player.Profile.Name.StartsWith(remaining, StringComparison.OrdinalIgnoreCase))
                    builder.Add(player.Profile.Name);
        }
        return builder.BuildFuture();
    }

    public IReadOnlyList<string> Examples => ExamplesList;
}
