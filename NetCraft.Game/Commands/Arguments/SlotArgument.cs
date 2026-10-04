using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Suggestion;
using NetCraft.Game.World.Inventory;
using NetCraft.Network.Component;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//SlotArgument 槽位参数对应原版 net.minecraft.commands.arguments.SlotArgument
//把槽位名(container.5 / weapon / armor.head ...)解析成槽位号 名字表在 SlotRanges
//只接受单槽名 带 * 的多槽名按原版报错
//网络 id 走 item_slot 与 item 命令的槽位参数一致
public sealed class SlotArgument : ArgumentType<int>
{
    private static readonly IReadOnlyList<string> ExamplesList = new[] { "container.5", "weapon" };

    public static readonly DynamicCommandExceptionType ErrorUnknownSlot =
        new(name => new LiteralMessage($"未知槽位 {name}"));

    public static readonly DynamicCommandExceptionType ErrorOnlySingleSlotAllowed =
        new(name => new LiteralMessage($"槽位 {name} 不是单个槽位"));

    public static SlotArgument Slot() => new();

    //GetSlot 取解析出的槽位号 对应原版 getSlot
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

    //ListSuggestions 补全全部单槽名 对应原版 listSuggestions 走 singleSlotNames
    public Task<Suggestions> ListSuggestions<S>(CommandContext<S> context, SuggestionsBuilder builder)
    {
        var remaining = builder.RemainingLowerCase;
        foreach (var name in SlotRanges.SingleSlotNames())
            if (name.StartsWith(remaining, StringComparison.Ordinal)) builder.Add(name);
        return builder.BuildFuture();
    }

    public IReadOnlyList<string> Examples => ExamplesList;
}
