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

//ClearCommand clear 命令对应原版 net.minecraft.server.commands.ClearInventoryCommands
//clear 清空自己 clear <targets> 清空目标 两者不限物品
//clear <targets> <item> 只清匹配的物品 clear <targets> <item> <maxCount> 最多清 maxCount 个
//maxCount 为 0 时只统计不删除 对应原版的 test 模式
public static class ClearCommand
{
    private static readonly DynamicCommandExceptionType ErrorSingle =
        new(name => new LiteralMessage($"未能清除 {name} 的任何物品"));

    private static readonly DynamicCommandExceptionType ErrorMultiple =
        new(count => new LiteralMessage($"未能清除 {count} 个玩家的任何物品"));

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

    //SelfTargets 省略 targets 时默认作用于执行者自己 对应原版 getPlayerOrException
    //控制台执行时没有玩家 这里按原版抛"只能由玩家执行" 不能往列表里塞 null 后面取背包会炸
    private static IReadOnlyList<ServerPlayer> SelfTargets(CommandContext<CommandSourceStack> context)
        => new[] { ((ServerCommandSource)context.GetSource()).PlayerOrThrow };

    //Clear 清除匹配物品并回执 对应原版 clearInventory
    //maxCount 为 -1 表示不限 为 0 表示只统计不删除
    private static int Clear(CommandContext<CommandSourceStack> context, IReadOnlyList<ServerPlayer> players,
        Predicate<ItemStack> predicate, int maxCount)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var count = 0;
        foreach (var player in players)
        {
            count += ClearOrCountMatching(player.Inventory, predicate, maxCount, maxCount == 0);
            //清完同步背包 增量变更不足以让客户端把整格清空显示出来
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
                ? $"已找到 {count} 个匹配的物品"
                : $"已在 {players.Count} 个玩家处找到 {count} 个匹配的物品");
        }
        else if (players.Count == 1)
        {
            source.SendSuccess($"已从 {players[0].Profile.Name} 清除 {count} 个物品");
        }
        else
        {
            source.SendSuccess($"已从 {players.Count} 个玩家清除 {count} 个物品");
        }
        return count;
    }

    //ClearOrCountMatching 遍历物品栏清除匹配物品 对应原版 ContainerHelper.clearOrCountMatchingItems
    //countOnly 为真只累加数量不动物品 maxCount 为 -1 表示不限
    //只清背包 36 格 护甲与副手不在原版 clearOrCountMatchingItems 的作用范围内
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
