using NetCraft.Codec;
using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Suggestion;
using NetCraft.Game.Commands;
using NetCraft.Nbt;
using NetCraft.Network.Component;
using NetCraft.Registry;
using NetCraft.Util;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//ItemInput item argument parse result, maps to vanilla ItemInput
//Carries the item itself, the identifier and the component patch; the identifier is used for the receipt display name
public sealed record ItemInput(Item Item, Identifier Id, DataComponentPatch Components);

//ItemArgument item argument, maps to vanilla net.minecraft.commands.arguments.item.ItemArgument
//Syntax: <item identifier>[<component>=<SNBT value>,!<component>,...]; component values go through SNBT first then the component's own persistent Codec
//The network id goes through item_stack, matching vanilla give's item argument type
public sealed class ItemArgument : ArgumentType<ItemInput>
{
    private const char SyntaxStartComponents = '[';
    private const char SyntaxEndComponents = ']';
    private const char SyntaxComponentSeparator = ',';
    private const char SyntaxComponentAssignment = '=';
    private const char SyntaxRemovedComponent = '!';

    private static readonly IReadOnlyList<string> ExamplesList =
        new[] { "stick", "minecraft:stick", "stick[damage=1]" };

    public static readonly DynamicCommandExceptionType ErrorUnknownItem =
        new(name => new LiteralMessage($"unknown item {name}"));

    public static readonly DynamicCommandExceptionType ErrorUnknownComponent =
        new(name => new LiteralMessage($"unknown component {name}"));

    public static readonly Dynamic2CommandExceptionType ErrorMalformedComponent =
        new((type, message) => new LiteralMessage($"component {type} malformed: {message}"));

    public static readonly SimpleCommandExceptionType ErrorExpectedComponent =
        new(new LiteralMessage("missing component"));

    public static readonly DynamicCommandExceptionType ErrorRepeatedComponent =
        new(name => new LiteralMessage($"duplicate component {name}"));

    private static RegistryOps<Tag>? _registryOps;
    private static TagParser<Tag>? _componentTagParser;

    //RegistryOpsForCommands lazily constructed; the registry is only needed when parsing component values
    private static RegistryOps<Tag> RegistryOpsForCommands
        => _registryOps ??= new RegistryOps<Tag>(NbtOps.Instance, BuiltInRegistries.CreateRegistryAccess());

    private static TagParser<Tag> ComponentTagParser
        => _componentTagParser ??= TagParser<Tag>.Create(RegistryOpsForCommands);

    public static ItemArgument Item() => new();

    //ParseState parse progress state; the suggestion path only cares about Suggest, the component map is filled as usual but unused
    private sealed class ParseState
    {
        public readonly Dictionary<object, Optional<object>> Components = new();
        public readonly HashSet<object> Seen = new(ReferenceEqualityComparer.Instance);

        //Suggest the suggestion at the current cursor, null means none
        public Action<SuggestionsBuilder>? Suggest;
    }

    public ItemInput Parse(StringReader reader)
    {
        var start = reader.Cursor;
        try
        {
            return ParseInternal(reader, new ParseState());
        }
        catch (CommandSyntaxException)
        {
            reader.SetCursor(start);
            throw;
        }
    }

    //ParseInternal item and components share one parse flow; during suggestions State.Suggest collects the current candidates
    private static ItemInput ParseInternal(StringReader reader, ParseState state)
    {
        state.Suggest = SuggestItem;
        var start = reader.Cursor;
        var id = IdentifierArgument.ReadIdentifier(reader);
        //Look up by ResourceKey; the item registry is defaulted, so looking up an unknown item by Identifier falls back to the default
        var item = BuiltInRegistries.ITEM.GetValue(ResourceKey<Item>.Create(Registries.ITEM, id));
        if (item is null)
        {
            reader.SetCursor(start);
            throw ErrorUnknownItem.CreateWithContext(reader, id.ToString());
        }
        state.Suggest = SuggestStartComponents;
        if (reader.CanRead() && reader.Peek() == SyntaxStartComponents)
        {
            state.Suggest = null;
            ParseComponents(reader, state);
        }
        //A successful parse does not reset suggestions; after the item position [ can still be suggested, matching vanilla parse not touching suggestions
        return new ItemInput(item, id, new DataComponentPatch(state.Components));
    }

    //ParseComponents parses [component=value,!component,...], aligned with vanilla ItemParser.State.readComponents
    private static void ParseComponents(StringReader reader, ParseState state)
    {
        reader.Expect(SyntaxStartComponents);
        state.Suggest = SuggestComponentAssignmentOrRemoval;
        while (reader.CanRead() && reader.Peek() != SyntaxEndComponents)
        {
            reader.SkipWhitespace();
            if (reader.CanRead() && reader.Peek() == SyntaxRemovedComponent)
            {
                reader.Skip();
                state.Suggest = builder => SuggestComponent(builder, string.Empty);
                var removed = ReadComponentType(reader);
                if (!state.Seen.Add(removed))
                    throw ErrorRepeatedComponent.Create(removed.ToString()!);
                state.Components[removed] = Optional<object>.Empty();
                state.Suggest = null;
                reader.SkipWhitespace();
            }
            else
            {
                var type = ReadComponentType(reader);
                if (!state.Seen.Add(type))
                    throw ErrorRepeatedComponent.Create(type.ToString()!);
                state.Suggest = SuggestAssignment;
                reader.SkipWhitespace();
                reader.Expect(SyntaxComponentAssignment);
                state.Suggest = null;
                reader.SkipWhitespace();
                state.Components[type] = Optional<object>.Of(ReadComponentValue(reader, type));
                reader.SkipWhitespace();
            }
            state.Suggest = SuggestNextOrEndComponents;
            if (!reader.CanRead() || reader.Peek() != SyntaxComponentSeparator)
                break;
            reader.Skip();
            reader.SkipWhitespace();
            state.Suggest = SuggestComponentAssignmentOrRemoval;
            if (!reader.CanRead())
                throw ErrorExpectedComponent.CreateWithContext(reader);
        }
        reader.Expect(SyntaxEndComponents);
        state.Suggest = null;
    }

    //ReadComponentType reads the component identifier and looks it up in the registry; a non-persistent component is reported as unknown like vanilla
    private static DataComponentType<object> ReadComponentType(StringReader reader)
    {
        if (!reader.CanRead())
            throw ErrorExpectedComponent.CreateWithContext(reader);
        var start = reader.Cursor;
        var id = IdentifierArgument.ReadIdentifier(reader);
        if (BuiltInRegistries.DATA_COMPONENT_TYPE.GetValue(id) is not DataComponentType<object> type
            || type.IsTransient)
        {
            reader.SetCursor(start);
            throw ErrorUnknownComponent.CreateWithContext(reader, id.ToString());
        }
        return type;
    }

    //ReadComponentValue reads the component value as SNBT into a Tag then hands it to the component Codec
    private static object ReadComponentValue(StringReader reader, DataComponentType<object> type)
    {
        var start = reader.Cursor;
        //The SNBT parser uses a separate reader and syncs the cursor after parsing
        var nbtReader = new CommandStringReader(reader.String) { Cursor = reader.Cursor };
        var tag = ComponentTagParser.ParseAsArgument(nbtReader);
        reader.SetCursor(nbtReader.Cursor);
        var parsed = type.CodecOrThrow().Parse(RegistryOpsForCommands, tag);
        return parsed.MapOrElse<object>(
            value => value,
            message =>
            {
                reader.SetCursor(start);
                throw ErrorMalformedComponent.CreateWithContext(reader, type.ToString()!, message);
            });
    }

    //GetItemInput gets the parsed item and identifier
    public static ItemInput GetItemInput(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<ItemInput>(name);

    //GetItem gets the parsed item
    public static Item GetItem(CommandContext<CommandSourceStack> context, string name)
        => GetItemInput(context, name).Item;

    //ListSuggestions runs the same parse flow; at the interruption point it suggests for the current syntax position, aligned with vanilla ItemParser.fillSuggestions
    public Task<Suggestions> ListSuggestions<S>(CommandContext<S> context, SuggestionsBuilder builder)
    {
        var reader = new StringReader(builder.Input);
        reader.SetCursor(builder.Start);
        var state = new ParseState();
        try
        {
            ParseInternal(reader, state);
        }
        catch (CommandSyntaxException)
        {
        }
        //Suggestions are based on the current cursor offset; the offset builder result is returned, maps to vanilla resolveSuggestions
        var offset = builder.CreateOffset(reader.Cursor);
        state.Suggest?.Invoke(offset);
        return offset.BuildFuture();
    }

    //SuggestItem suggests all item identifiers in the registry, omitting the prefix for the default namespace
    private static void SuggestItem(SuggestionsBuilder builder)
    {
        foreach (var id in BuiltInRegistries.ITEM.KeySet)
        {
            var text = id.ToShortString();
            if (text.StartsWith(builder.RemainingLowerCase, StringComparison.Ordinal))
                builder.Add(text);
        }
    }

    //SuggestStartComponents after the item identifier only a component list can follow
    private static void SuggestStartComponents(SuggestionsBuilder builder)
    {
        if (builder.Remaining.Length == 0)
            builder.Add(SyntaxStartComponents.ToString());
    }

    //SuggestComponentAssignmentOrRemoval at a component position you can write a ! prefix removal or a component identifier
    private static void SuggestComponentAssignmentOrRemoval(SuggestionsBuilder builder)
    {
        builder.Add(SyntaxRemovedComponent.ToString());
        SuggestComponent(builder, SyntaxComponentAssignment.ToString());
    }

    //SuggestComponent lists component identifiers with a persistent Codec, optionally with a suffix
    private static void SuggestComponent(SuggestionsBuilder builder, string suffix)
    {
        var contents = builder.Remaining.ToLowerInvariant();
        foreach (var id in BuiltInRegistries.DATA_COMPONENT_TYPE.KeySet)
        {
            if (BuiltInRegistries.DATA_COMPONENT_TYPE.GetValue(id) is not DataComponentType<object> type
                || type.Codec is null)
                continue;
            var text = id.ToString();
            if (text.StartsWith(contents, StringComparison.Ordinal))
                builder.Add(text + suffix);
        }
    }

    private static void SuggestAssignment(SuggestionsBuilder builder)
    {
        if (builder.Remaining.Length == 0)
            builder.Add(SyntaxComponentAssignment.ToString());
    }

    private static void SuggestNextOrEndComponents(SuggestionsBuilder builder)
    {
        if (builder.Remaining.Length == 0)
        {
            builder.Add(SyntaxComponentSeparator.ToString());
            builder.Add(SyntaxEndComponents.ToString());
        }
    }

    public IReadOnlyList<string> Examples => ExamplesList;
}
