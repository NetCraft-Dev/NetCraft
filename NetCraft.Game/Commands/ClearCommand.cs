using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Server;
using NetCraft.Game.World.Inventory;
using NetCraft.Game.World.Items;

namespace NetCraft.Game.Commands;

//ClearCommand clear command, maps to vanilla net.minecraft.server.commands.ClearInventoryCommands
//clear clears your own items; clear <targets> clears the targets' items, both unrestricted by item
//clear <targets> <item> clears only matching items; clear <targets> <item> <maxCount> clears at most maxCount
//maxCount of 0 only counts without removing, maps to the vanilla test mode
public static class ClearCommand
{
    private static readonly DynamicCommandExceptionType ErrorSingle =
        new(name => new LiteralMessage($"found no items to clear for {name}"));

    private static readonly DynamicCommandExceptionType ErrorMultiple =
        new(count => new LiteralMessage($"found no items to clear for {count} players"));

    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("clear")
            .Requires(s => s.HasPermission(2))
            .Executes(context => Clear(context, SelfTargets(context), _ => true, -1))
            .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("targets", EntityArgument.Players())
                .Executes(context => Clear(context, EntityArgument.GetPlayers(context, "targets"), _ => true, -1))
                .Then(RequiredArgumentBuilder<CommandSourceStack, Predicate<ItemStack>>.Argument(
                        "item", ItemPredicateArgument.ItemPredicate())
                    .Executes(context => Clear(context, EntityArgument.GetPlayers(context, "targets"),
                        ItemPredicateArgument.GetItemPredicate(context, "item"), -1))
                    .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument(
                            "maxCount", IntegerArgumentType.Integer(0))
                        .Executes(context => Clear(context, EntityArgument.GetPlayers(context, "targets"),
                            ItemPredicateArgument.GetItemPredicate(context, "item"),
                            IntegerArgumentType.GetInteger(context, "maxCount")))))));
    }

    //SelfTargets applies to the executor when targets is omitted, maps to vanilla getPlayerOrException
    //There is no player when run from the console; throws vanilla's "players only" here, not a null pushed into the list or the inventory lookup blows up later
    private static IReadOnlyList<ServerPlayer> SelfTargets(CommandContext<CommandSourceStack> context)
        => new[] { ((ServerCommandSource)context.GetSource()).PlayerOrThrow };

    //Clear removes matching items and reports, maps to vanilla clearInventory
    //maxCount -1 means unlimited, 0 means count only
    private static int Clear(CommandContext<CommandSourceStack> context, IReadOnlyList<ServerPlayer> players,
        Predicate<ItemStack> predicate, int maxCount)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var count = 0;
        foreach (var player in players)
        {
            count += ClearOrCountMatching(player.Inventory, predicate, maxCount, maxCount == 0);
            //Sync the inventory after clearing; incremental changes are not enough to clear a whole slot on the client
            player.ContainerMenu?.SendAllDataToRemote();
        }
        if (count == 0)
        {
            if (players.Count == 1) throw ErrorSingle.Create(players[0].Profile.Name);
            throw ErrorMultiple.Create(players.Count);
        }
        if (maxCount == 0)
        {
            source.SendSuccess(players.Count == 1
                ? $"found {count} matching items"
                : $"found {count} matching items across {players.Count} players");
        }
        else if (players.Count == 1)
        {
            source.SendSuccess($"cleared {count} items from {players[0].Profile.Name}");
        }
        else
        {
            source.SendSuccess($"cleared {count} items from {players.Count} players");
        }
        return count;
    }

    //ClearOrCountMatching iterates the inventory to clear matching items, maps to vanilla ContainerHelper.clearOrCountMatchingItems
    //countOnly only accumulates the count; maxCount -1 means unlimited
    //Only the 36 backpack slots are cleared; armor and offhand are outside the vanilla clearOrCountMatchingItems scope
    private static int ClearOrCountMatching(PlayerInventory inventory, Predicate<ItemStack> predicate,
        int maxCount, bool countOnly)
    {
        var count = 0;
        for (var slot = 0; slot < PlayerInventory.BackpackSize && (maxCount < 0 || count < maxCount); slot++)
        {
            var stack = inventory.GetItem(slot);
            if (stack.IsEmpty() || !predicate(stack)) continue;
            if (countOnly)
            {
                count += stack.GetCount();
                continue;
            }
            var taken = maxCount < 0 ? stack.GetCount() : Math.Min(maxCount - count, stack.GetCount());
            count += taken;
            if (taken >= stack.GetCount()) inventory.SetItem(slot, ItemStack.Empty);
            else stack.Shrink(taken);
        }
        return count;
    }
}
