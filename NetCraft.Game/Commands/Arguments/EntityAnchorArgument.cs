using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Suggestion;
using NetCraft.Game.Commands;
using NetCraft.Game.Server;
using NetCraft.Primitives;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//EntityAnchorArgument entity anchor argument, maps to vanilla EntityAnchorArgument
//feet / eyes decide which reference point is used when tp facing an entity
public sealed class EntityAnchorArgument : ArgumentType<EntityAnchorArgument.Anchor>
{
    private static readonly IReadOnlyList<string> ExamplesList = new[] { "eyes", "feet" };

    public static readonly DynamicCommandExceptionType ErrorInvalid =
        new(name => new TranslatableMessage("argument.anchor.invalid", name));

    public static EntityAnchorArgument EntityAnchor() => new();

    //Anchor anchor; feet is the foot position, eyes raises the eye height above it
    public enum Anchor
    {
        Feet,
        Eyes,
    }

    private static readonly IReadOnlyDictionary<string, Anchor> ByName = new Dictionary<string, Anchor>
    {
        ["feet"] = Anchor.Feet,
        ["eyes"] = Anchor.Eyes,
    };

    //Player standing eye height; treated as the player constant before the entity system is wired up
    private const float PlayerEyeHeight = 1.62f;

    public Anchor Parse(StringReader reader)
    {
        var start = reader.Cursor;
        var name = reader.ReadUnquotedString();
        if (!ByName.TryGetValue(name, out var anchor))
        {
            reader.SetCursor(start);
            throw ErrorInvalid.CreateWithContext(reader, name);
        }
        return anchor;
    }

    //GetAnchor gets the parse result
    public static Anchor GetAnchor(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<Anchor>(name);

    //Apply resolves the executor's world coordinate for the anchor
    public static Vec3 Apply(Anchor anchor, ServerCommandSource source)
        => Apply(anchor, source.PlayerOrThrow);

    //Apply resolves the player's world coordinate for the anchor
    public static Vec3 Apply(Anchor anchor, ServerPlayer player)
    {
        var pos = player.Position;
        return anchor == Anchor.Eyes
            ? new Vec3(pos.X, pos.Y + PlayerEyeHeight, pos.Z)
            : pos;
    }

    public Task<Suggestions> ListSuggestions<S>(CommandContext<S> context, SuggestionsBuilder builder)
    {
        foreach (var name in ByName.Keys)
            builder.Add(name);
        return builder.BuildFuture();
    }

    public IReadOnlyList<string> Examples => ExamplesList;
}
