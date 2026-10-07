using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Suggestion;
using NetCraft.Game.World.Inventory;
using NetCraft.Network.Component;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//SlotArgument slot argument, maps to vanilla net.minecraft.commands.arguments.SlotArgument
//Parses slot names (container.5 / weapon / armor.head ...) into slot numbers; the name table is in SlotRanges
//Only accepts single-slot names; multi-slot names with * error out like vanilla
//The network id goes through item_slot, matching the item command's slot argument
public sealed class SlotArgument : ArgumentType<int>
{
    private static readonly IReadOnlyList<string> ExamplesList = new[] { "container.5", "weapon" };

    public static readonly DynamicCommandExceptionType ErrorUnknownSlot =
        new(name => new LiteralMessage($"unknown slot {name}"));

    public static readonly DynamicCommandExceptionType ErrorOnlySingleSlotAllowed =
        new(name => new LiteralMessage($"slot {name} is not a single slot"));

    public static SlotArgument Slot() => new();

    //GetSlot gets the parsed slot number, maps to vanilla getSlot
    public static int GetSlot<S>(CommandContext<S> context, string name)
        => context.GetArgument<int>(name);

    public int Parse(StringReader reader)
    {
        var start = reader.Cursor;
        var name = reader.ReadUnquotedString();
        var ids = SlotRanges.NameToIds(name);
        if (ids is null)
        {
            reader.SetCursor(start);
            throw ErrorUnknownSlot.CreateWithContext(reader, name);
        }
        if (ids.Length != 1)
        {
            reader.SetCursor(start);
            throw ErrorOnlySingleSlotAllowed.CreateWithContext(reader, name);
        }
        return ids[0];
    }

    //ListSuggestions suggests all single-slot names, maps to vanilla listSuggestions through singleSlotNames
    public Task<Suggestions> ListSuggestions<S>(CommandContext<S> context, SuggestionsBuilder builder)
    {
        var remaining = builder.RemainingLowerCase;
        foreach (var name in SlotRanges.SingleSlotNames())
            if (name.StartsWith(remaining, StringComparison.Ordinal)) builder.Add(name);
        return builder.BuildFuture();
    }

    public IReadOnlyList<string> Examples => ExamplesList;
}
