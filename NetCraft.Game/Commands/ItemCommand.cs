using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Server;
using NetCraft.Game.World.Inventory;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Level;
using NetCraft.Primitives;
using NetCraft.Storage;

namespace NetCraft.Game.Commands;

//ItemCommand /item 命令对应原版 net.minecraft.server.commands.ItemCommands
//replace block <坐标> <槽位> with <物品> [数量] 把物品写进方块实体容器
//replace ... from block <源坐标> <源槽位> 从另一个容器整栈搬过来
//replace ... from entity <源实体> <源槽位> 从实体身上的槽整栈搬过来
//replace entity <目标> <槽位> 同上三个分支 写的是实体身上的槽
//modify 分支依赖 item_modifier 这个数据驱动注册表 本作还没接入 暂不注册
public static class ItemCommand
{
    //原版数量参数范围 1..99 对应 ItemInstance.FIELD_COUNT
    private const int MaxCount = 99;

    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        var replace = LiteralArgumentBuilder<CommandSourceStack>.Literal("replace");
        RegisterBlockReplace(replace);
        RegisterEntityReplace(replace);
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("item")
            .Requires(s => s.HasPermission(2))
            .Then(replace));
    }

    //RegisterBlockReplace item replace block <坐标> <槽位> 的 with 与 from 分支
    private static void RegisterBlockReplace(LiteralArgumentBuilder<CommandSourceStack> replace)
    {
        var slot = RequiredArgumentBuilder<CommandSourceStack, int>.Argument("slot", SlotArgument.Slot());
        slot.Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("with")
            .Then(RequiredArgumentBuilder<CommandSourceStack, ItemInput>.Argument("item", ItemArgument.Item())
                .Executes(context => BlockWithItem(context, 1))
                .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("count", IntegerArgumentType.Integer(1, MaxCount))
                    .Executes(context => BlockWithItem(context, IntegerArgumentType.GetInteger(context, "count"))))));
        slot.Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("from")
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("block")
                .Then(RequiredArgumentBuilder<CommandSourceStack, BlockCoordinates>.Argument("source", BlockPosArgument.BlockPos())
                    .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("sourceSlot", SlotArgument.Slot())
                        .Executes(BlockFromBlock))))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("entity")
                .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("source", EntityArgument.Entity())
                    .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("sourceSlot", SlotArgument.Slot())
                        .Executes(BlockFromEntity)))));
        replace.Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("block")
            .Then(RequiredArgumentBuilder<CommandSourceStack, BlockCoordinates>.Argument("pos", BlockPosArgument.BlockPos())
                .Then(slot)));
    }

    //RegisterEntityReplace item replace entity <目标> <槽位> 的 with 与 from 分支
    private static void RegisterEntityReplace(LiteralArgumentBuilder<CommandSourceStack> replace)
    {
        var slot = RequiredArgumentBuilder<CommandSourceStack, int>.Argument("slot", SlotArgument.Slot());
        slot.Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("with")
            .Then(RequiredArgumentBuilder<CommandSourceStack, ItemInput>.Argument("item", ItemArgument.Item())
                .Executes(context => EntityWithItem(context, 1))
                .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("count", IntegerArgumentType.Integer(1, MaxCount))
                    .Executes(context => EntityWithItem(context, IntegerArgumentType.GetInteger(context, "count"))))));
        slot.Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("from")
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("block")
                .Then(RequiredArgumentBuilder<CommandSourceStack, BlockCoordinates>.Argument("source", BlockPosArgument.BlockPos())
                    .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("sourceSlot", SlotArgument.Slot())
                        .Executes(EntityFromBlock))))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("entity")
                .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("source", EntityArgument.Entity())
                    .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("sourceSlot", SlotArgument.Slot())
                        .Executes(EntityFromEntity)))));
        replace.Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("entity")
            .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("targets", EntityArgument.Entities())
                .Then(slot)));
    }

    //BlockWithItem item replace block <坐标> <槽位> with <物品> [数量]
    private static int BlockWithItem(CommandContext<CommandSourceStack> context, int count)
    {
        var source = Source(context);
        var input = ItemArgument.GetItemInput(context, "item");
        return SetBlockItem(source, BlockPosArgument.GetBlockPos(context, "pos"),
            SlotArgument.GetSlot(context, "slot"),
            new ItemStack(input.Item.BuiltInRegistryHolder, count, input.Components));
    }

    //EntityWithItem item replace entity <目标> <槽位> with <物品> [数量]
    private static int EntityWithItem(CommandContext<CommandSourceStack> context, int count)
    {
        var source = Source(context);
        var input = ItemArgument.GetItemInput(context, "item");
        return SetEntityItem(source, EntityArgument.GetEntities(context, "targets"),
            SlotArgument.GetSlot(context, "slot"),
            new ItemStack(input.Item.BuiltInRegistryHolder, count, input.Components));
    }

    //BlockFromBlock 从源容器的槽位整栈搬进目标容器的槽位 对应原版 blockToBlock
    private static int BlockFromBlock(CommandContext<CommandSourceStack> context)
    {
        var source = Source(context);
        var stack = ReadBlockStack(source, BlockPosArgument.GetBlockPos(context, "source"),
            SlotArgument.GetSlot(context, "sourceSlot"));
        return stack is null
            ? 0
            : SetBlockItem(source, BlockPosArgument.GetBlockPos(context, "pos"),
                SlotArgument.GetSlot(context, "slot"), stack);
    }

    //EntityFromBlock 从源容器的槽位整栈搬进实体槽位 对应原版 blockToEntities
    private static int EntityFromBlock(CommandContext<CommandSourceStack> context)
    {
        var source = Source(context);
        var stack = ReadBlockStack(source, BlockPosArgument.GetBlockPos(context, "source"),
            SlotArgument.GetSlot(context, "sourceSlot"));
        return stack is null
            ? 0
            : SetEntityItem(source, EntityArgument.GetEntities(context, "targets"),
                SlotArgument.GetSlot(context, "slot"), stack);
    }

    //BlockFromEntity 从实体槽位整栈搬进目标容器的槽位 对应原版 entityToBlock
    //本作实体槽位只实现了玩家 源实体按玩家取 非玩家按原版只允许玩家的报错处理
    private static int BlockFromEntity(CommandContext<CommandSourceStack> context)
    {
        var source = Source(context);
        var from = EntityArgument.GetEntity(context, "source");
        var stack = ReadEntityStack(source, from, SlotArgument.GetSlot(context, "sourceSlot"));
        return stack is null
            ? 0
            : SetBlockItem(source, BlockPosArgument.GetBlockPos(context, "pos"),
                SlotArgument.GetSlot(context, "slot"), stack);
    }

    //EntityFromEntity 从实体槽位整栈搬进实体槽位 对应原版 entityToEntities
    private static int EntityFromEntity(CommandContext<CommandSourceStack> context)
    {
        var source = Source(context);
        var from = EntityArgument.GetEntity(context, "source");
        var stack = ReadEntityStack(source, from, SlotArgument.GetSlot(context, "sourceSlot"));
        return stack is null
            ? 0
            : SetEntityItem(source, EntityArgument.GetEntities(context, "targets"),
                SlotArgument.GetSlot(context, "slot"), stack);
    }

    //ReadBlockStack 读方块实体槽位上的一份物品拷贝 对应原版 getBlockItem
    //取不到容器或槽位不适用时已发回执 返回 null 表示失败
    private static ItemStack? ReadBlockStack(ServerCommandSource source, BlockPos pos, int slot)
    {
        if (GetContainer(source, pos) is not { } container)
        {
            source.SendFailure($"位置 {pos.X} {pos.Y} {pos.Z} 处没有容器");
            return null;
        }
        return ReadStack(source, SlotAccess.ForContainer(container, slot), $"位置 {pos.X} {pos.Y} {pos.Z}");
    }

    //ReadEntityStack 读实体槽位上的一份物品拷贝 对应原版 getItemInSlot
    private static ItemStack? ReadEntityStack(ServerCommandSource source, ServerPlayer player, int slot)
        => ReadStack(source, SlotAccess.ForPlayer(player, slot), player.Profile.Name);

    //ReadStack 读槽位内容 空槽返回空栈 槽位不适用按原版报源槽位不存在
    private static ItemStack? ReadStack(ServerCommandSource source, SlotAccess access, string where)
    {
        if (ReferenceEquals(access, SlotAccess.Null))
        {
            source.SendFailure($"{where} 上没有该槽位");
            return null;
        }
        return access.Get().Copy();
    }

    //SetBlockItem 把物品写进方块实体容器 对应原版 setBlockItem
    private static int SetBlockItem(ServerCommandSource source, BlockPos pos, int slot, ItemStack stack)
    {
        if (GetContainer(source, pos) is not { } container)
        {
            source.SendFailure($"位置 {pos.X} {pos.Y} {pos.Z} 处没有容器");
            return 0;
        }
        var where = $"位置 {pos.X} {pos.Y} {pos.Z}";
        if (!SetSlot(source, SlotAccess.ForContainer(container, slot), slot, stack, where)) return 0;
        source.SendSuccess($"已将 {where} 的槽位物品设置为 {Describe(stack)}");
        return 1;
    }

    //SetEntityItem 把物品写进目标玩家身上的槽 对应原版 setEntityItem
    //原版对每个目标写一份 copy 一次成功的回执里只有单个目标时才带目标名
    private static int SetEntityItem(ServerCommandSource source, IReadOnlyList<CommandTarget> targets, int slot, ItemStack stack)
    {
        List<string>? changed = null;
        foreach (var target in targets)
        {
            if (target.Player is not { } player) continue;
            if (!SetSlot(source, SlotAccess.ForPlayer(player, slot), slot, stack, player.Profile.Name)) continue;
            player.ContainerMenu?.SendAllDataToRemote();
            changed ??= new List<string>();
            changed.Add(player.Profile.Name);
        }
        if (changed is null)
        {
            source.SendFailure($"槽位 {slot} 上没有任何目标发生变化");
            return 0;
        }
        source.SendSuccess(changed.Count == 1
            ? $"已将 {changed[0]} 的槽位物品设置为 {Describe(stack)}"
            : $"已将 {changed.Count} 个目标的槽位物品设置为 {Describe(stack)}");
        return changed.Count;
    }

    //SetSlot 写槽位 成功返回 true 失败按原因发回执
    //静默判错 写失败了不报 由调用方汇总后统一报 这里只在槽位不适用时报
    private static bool SetSlot(ServerCommandSource source, SlotAccess access, int slot, ItemStack stack, string where)
    {
        if (ReferenceEquals(access, SlotAccess.Null))
        {
            source.SendFailure($"{where} 上没有槽位 {slot}");
            return false;
        }
        return access.Set(stack.Copy());
    }

    //GetContainer 取方块实体容器 对应原版 getContainer
    private static Container? GetContainer(ServerCommandSource source, BlockPos pos)
        => source.PlayerOrThrow.Level is PersistentServerLevel level
            ? level.BlockUpdateSink?.GetBlockEntity(pos) as Container
            : null;

    //Describe 取物品显示名 自定义名称组件接入前用标识符代替
    private static string Describe(ItemStack stack)
        => stack.IsEmpty() ? "空气" : stack.GetItem().Id.ToShortString();

    private static ServerCommandSource Source(CommandContext<CommandSourceStack> context)
        => (ServerCommandSource)context.GetSource();
}
