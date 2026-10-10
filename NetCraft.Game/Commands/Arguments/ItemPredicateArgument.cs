using NetCraft.Codec;
using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Suggestion;
using NetCraft.Game.Commands;
using NetCraft.Game.World.Items;
using NetCraft.Nbt;
using NetCraft.Game.World.Items.Component;
using NetCraft.Registry;
using NetCraft.Util;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//ItemPredicateArgument item predicate argument, maps to vanilla net.minecraft.commands.arguments.item.ItemPredicateArgument
//Syntax: <item> | #<tag> | *, optionally followed by [condition,condition...]
//Conditions are ANDed; within a condition | means OR and ! negates
//Three condition forms: <component>=<SNBT value> value match / <component> alone means existence / <predicate>~<SNBT value>
//Vanilla uses a packrat syntax tree; this project has no such framework and uses an equivalent recursive descent with the same branch order as vanilla
public sealed class ItemPredicateArgument : ArgumentType<Predicate<ItemStack>>
{
    //CountId count pseudo-component id, maps to vanilla ItemPredicateArgument.COUNT_ID
    //count is not in the component registry and is judged by a count range on its own
    private static readonly Identifier CountId = Identifier.WithDefaultNamespace("count");

    private static readonly IReadOnlyList<string> ExamplesList =
        new[] { "stick", "minecraft:stick", "#stick", "#stick[foo='bar']" };

    public static readonly DynamicCommandExceptionType ErrorUnknownItem =
        new(id => new LiteralMessage($"unknown item {id}"));

    public static readonly DynamicCommandExceptionType ErrorUnknownTag =
        new(id => new LiteralMessage($"unknown item tag {id}"));

    public static readonly DynamicCommandExceptionType ErrorUnknownComponent =
        new(id => new LiteralMessage($"unknown component {id}"));

    public static readonly Dynamic2CommandExceptionType ErrorMalformedComponent =
        new((type, message) => new LiteralMessage($"component {type} malformed: {message}"));

    private static RegistryOps<Tag>? _registryOps;
    private static TagParser<Tag>? _componentTagParser;

    //RegistryOpsForCommands lazily constructed; the registry is only needed when parsing component values
    private static RegistryOps<Tag> RegistryOpsForCommands
        => _registryOps ??= new RegistryOps<Tag>(NbtOps.Instance, BuiltInRegistries.CreateRegistryAccess());

    private static TagParser<Tag> ComponentTagParser
        => _componentTagParser ??= TagParser<Tag>.Create(RegistryOpsForCommands);

    //ItemPredicate creates a predicate argument instance, used for command tree registration
    public static ItemPredicateArgument ItemPredicate() => new();

    public Predicate<ItemStack> Parse(StringReader reader)
    {
        var start = reader.Cursor;
        try
        {
            return ParseTop(reader);
        }
        catch (CommandSyntaxException)
        {
            reader.SetCursor(start);
            throw;
        }
    }

    //ParseTop base type plus an optional condition list, maps to vanilla top rule
    private static Predicate<ItemStack> ParseTop(StringReader reader)
    {
        var basePredicate = ParseAnyType(reader);
        //Without a condition list, the wildcard and an item tag are themselves a complete predicate, maps to the second branch of vanilla top
        if (!reader.CanRead() || reader.Peek() != '[') return basePredicate;
        reader.Skip();
        Predicate<ItemStack>? conditions = null;
        if (reader.CanRead() && reader.Peek() != ']')
            conditions = ParseConditions(reader);
        reader.Expect(']');
        if (conditions is null) return basePredicate;
        //The base type and the condition list are ANDed, maps to vanilla Util.allOf
        var rest = conditions;
        return stack => basePredicate(stack) && rest(stack);
    }

    //ParseAnyType item identifier, #tag or * wildcard, maps to vanilla any_type rule
    private static Predicate<ItemStack> ParseAnyType(StringReader reader)
    {
        if (!reader.CanRead())
            throw ErrorUnknownItem.CreateWithContext(reader, "");
        //* matches any item, maps to vanilla all_type
        if (reader.Peek() == '*')
        {
            reader.Skip();
            return _ => true;
        }
        if (reader.Peek() == '#')
        {
            reader.Skip();
            var tagStart = reader.Cursor;
            var tagId = IdentifierArgument.ReadIdentifier(reader);
            var key = TagKey<Item>.Create(Registries.ITEM, tagId);
            var set = BuiltInRegistries.ITEM.Get(key);
            if (set is null)
            {
                reader.SetCursor(tagStart);
                throw ErrorUnknownTag.CreateWithContext(reader, tagId.ToString());
            }
            return stack => !stack.IsEmpty() && set.Contains(stack.GetTypeHolder()!);
        }
        var itemStart = reader.Cursor;
        var itemId = IdentifierArgument.ReadIdentifier(reader);
        //The registry is defaulted, so look up by ResourceKey; otherwise an unknown item silently falls back to the default
        var item = BuiltInRegistries.ITEM.GetValue(ResourceKey<Item>.Create(Registries.ITEM, itemId));
        if (item is null)
        {
            reader.SetCursor(itemStart);
            throw ErrorUnknownItem.CreateWithContext(reader, itemId.ToString());
        }
        return stack => !stack.IsEmpty() && ReferenceEquals(stack.GetItem(), item);
    }

    //ParseConditions all comma-separated conditions must hold, maps to vanilla conditions rule
    private static Predicate<ItemStack> ParseConditions(StringReader reader)
    {
        var first = ParseAlternatives(reader);
        if (!reader.CanRead() || reader.Peek() != ',') return first;
        reader.Skip();
        var rest = ParseConditions(reader);
        return stack => first(stack) && rest(stack);
    }

    //ParseAlternatives any of the pipe-separated candidates must hold, maps to vanilla alternatives rule
    private static Predicate<ItemStack> ParseAlternatives(StringReader reader)
    {
        var first = ParseTerm(reader);
        if (!reader.CanRead() || reader.Peek() != '|') return first;
        reader.Skip();
        var rest = ParseAlternatives(reader);
        return stack => first(stack) || rest(stack);
    }

    //ParseTerm a negation prefix followed by a single condition, maps to vanilla term rule; ! cannot be chained
    private static Predicate<ItemStack> ParseTerm(StringReader reader)
    {
        if (reader.CanRead() && reader.Peek() == '!')
        {
            reader.Skip();
            var inner = ParseTest(reader);
            return stack => !inner(stack);
        }
        return ParseTest(reader);
    }

    //ParseTest component value match, predicate match, component existence, maps to the three branches of vanilla test rule
    //The vanilla atomic order of the three branches is component_type '=' tag / predicate_type '~' tag / component_type
    //Component type parsing takes precedence over predicate type, because the first two branches both require the component type to match first
    private static Predicate<ItemStack> ParseTest(StringReader reader)
    {
        var start = reader.Cursor;
        var id = IdentifierArgument.ReadIdentifier(reader);
        //Count pseudo-component; the value is an integer range and is always true when present alone, maps to count in vanilla PSEUDO_COMPONENTS
        if (id == CountId)
        {
            if (reader.CanRead() && reader.Peek() == '=')
            {
                reader.Skip();
                var range = ReadCountRange(reader, ReadNbt(reader));
                return stack => !stack.IsEmpty() && range.Matches(stack.GetCount());
            }
            return _ => true;
        }
        if (LookupComponentType(id) is { } componentType)
        {
            if (reader.CanRead() && reader.Peek() == '=')
            {
                reader.Skip();
                var expected = ReadComponentValue(reader, componentType, start);
                //Value match semantics are an exact predicate, maps to vanilla DataComponentExactPredicate.expect(type, value)
                var exact = DataComponentExactPredicate.Expect(componentType, expected);
                return stack => !stack.IsEmpty() && exact.Test(stack.GetComponents());
            }
            return stack => !stack.IsEmpty() && stack.GetComponents().Get(componentType) is not null;
        }
        //Predicate branch; the predicate type is dispatched through the registry and the value is decoded from SNBT by that type's codec, maps to vanilla predicate_type '~' tag
        if (LookupPredicateType(id) is { } predicateType
            && reader.CanRead() && reader.Peek() == '~')
        {
            reader.Skip();
            var predicateTag = ReadNbt(reader);
            var parsed = predicateType.Codec.Parse(RegistryOpsForCommands, predicateTag);
            if (!parsed.Result().IsPresent)
            {
                reader.SetCursor(start);
                throw ErrorMalformedComponent.CreateWithContext(reader, id.ToString()!, "invalid predicate value");
            }
            var predicateValue = parsed.GetOrThrow();
            return stack => !stack.IsEmpty() && predicateType.Matches(stack.GetComponents(), predicateValue);
        }
        reader.SetCursor(start);
        throw ErrorUnknownComponent.CreateWithContext(reader, id.ToString());
    }

    //LookupComponentType looks up a persistent component type by identifier; a non-persistent component is reported unknown like vanilla
    private static DataComponentType<object>? LookupComponentType(Identifier id)
        => BuiltInRegistries.DATA_COMPONENT_TYPE.GetValue(id) is DataComponentType<object> type && !type.IsTransient
            ? type
            : null;

    //LookupPredicateType looks up the predicate type by identifier, maps to vanilla lookupPredicateType
    //The component type side does not go through here; component existence and value matching are handled in the first two branches
    private static DataComponentPredicate.Type? LookupPredicateType(Identifier id)
        => BuiltInRegistries.DATA_COMPONENT_PREDICATE_TYPE.GetValue(id) as DataComponentPredicate.Type;

    //ReadComponentValue reads the component value as SNBT into a Tag then hands it to the component Codec
    private static object ReadComponentValue(StringReader reader, DataComponentType<object> type, int errorStart)
    {
        var tag = ReadNbt(reader);
        var parsed = type.CodecOrThrow().Parse(RegistryOpsForCommands, tag);
        return parsed.MapOrElse<object>(
            value => value,
            message =>
            {
                reader.SetCursor(errorStart);
                throw ErrorMalformedComponent.CreateWithContext(reader, type.ToString()!, message);
            });
    }

    //ReadCountRange the value of the count pseudo-component; a single integer treats both bounds as equal, a compound tag reads min/max
    private static MinMaxBounds.Ints ReadCountRange(StringReader reader, Tag tag)
    {
        if (tag is IntTag intTag)
            return new MinMaxBounds.Ints(intTag.Value, intTag.Value);
        if (tag is CompoundTag compound && (compound.Contains("min") || compound.Contains("max")))
            return new MinMaxBounds.Ints(compound.GetInt("min")?.Value, compound.GetInt("max")?.Value);
        throw ErrorMalformedComponent.CreateWithContext(reader, CountId.ToString(), "expected an integer or a min/max range");
    }

    //ReadNbt parses SNBT with a separate reader and syncs the cursor back to the command reader
    private static Tag ReadNbt(StringReader reader)
    {
        var nbtReader = new CommandStringReader(reader.String) { Cursor = reader.Cursor };
        var tag = ComponentTagParser.ParseAsArgument(nbtReader);
        reader.SetCursor(nbtReader.Cursor);
        return tag;
    }

    //GetItemPredicate gets the parsed predicate
    public static Predicate<ItemStack> GetItemPredicate(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<Predicate<ItemStack>>(name);

    //ListSuggestions suggests items or tags for the current cursor position, maps to vanilla ResourceLookupRule suggestions
    public Task<Suggestions> ListSuggestions<S>(CommandContext<S> context, SuggestionsBuilder builder)
    {
        var remaining = builder.Remaining;
        if (remaining.StartsWith('#'))
        {
            foreach (var set in BuiltInRegistries.ITEM.GetTags())
            {
                var text = "#" + set.Key.Location.ToShortString();
                if (text.StartsWith(remaining, StringComparison.Ordinal))
                    builder.Add(text);
            }
        }
        else
        {
            foreach (var id in BuiltInRegistries.ITEM.KeySet)
            {
                var text = id.ToShortString();
                if (text.StartsWith(remaining, StringComparison.Ordinal))
                    builder.Add(text);
            }
            //Tags and the wildcard are also suggested, maps to vanilla listTagTypes and all_type
            if ("#".StartsWith(remaining, StringComparison.Ordinal)) builder.Add("#");
            if ("*".StartsWith(remaining, StringComparison.Ordinal)) builder.Add("*");
        }
        return builder.BuildFuture();
    }

    public IReadOnlyList<string> Examples => ExamplesList;
}
