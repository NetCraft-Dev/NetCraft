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

//ItemCommand /item command, maps to vanilla net.minecraft.server.commands.ItemCommands
//replace block <pos> <slot> with <item> [count] writes an item into a block entity container
//replace ... from block <source pos> <source slot> moves a whole stack from another container
//replace ... from entity <source entity> <source slot> moves a whole stack from a slot on an entity
//replace entity <target> <slot> has the same three branches, writing to slots on the entity
//The modify branch depends on the item_modifier data-driven registry, not wired up here, so it is not registered
public static class ItemCommand
{
    //Vanilla count argument range 1..99, maps to ItemInstance.FIELD_COUNT
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

    //RegisterBlockReplace the with and from branches of item replace block <pos> <slot>
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

    //RegisterEntityReplace the with and from branches of item replace entity <target> <slot>
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

    //BlockWithItem item replace block <pos> <slot> with <item> [count]
    private static int BlockWithItem(CommandContext<CommandSourceStack> context, int count)
    {
        var source = Source(context);
        var input = ItemArgument.GetItemInput(context, "item");
        return SetBlockItem(source, BlockPosArgument.GetBlockPos(context, "pos"),
            SlotArgument.GetSlot(context, "slot"),
            new ItemStack(input.Item.BuiltInRegistryHolder, count, input.Components));
    }

    //EntityWithItem item replace entity <target> <slot> with <item> [count]
    private static int EntityWithItem(CommandContext<CommandSourceStack> context, int count)
    {
        var source = Source(context);
        var input = ItemArgument.GetItemInput(context, "item");
        return SetEntityItem(source, EntityArgument.GetEntities(context, "targets"),
            SlotArgument.GetSlot(context, "slot"),
            new ItemStack(input.Item.BuiltInRegistryHolder, count, input.Components));
    }

    //BlockFromBlock moves a whole stack from a source container slot into the target container slot, maps to vanilla blockToBlock
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

    //EntityFromBlock moves a whole stack from a source container slot into an entity slot, maps to vanilla blockToEntities
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

    //BlockFromEntity moves a whole stack from an entity slot into the target container slot, maps to vanilla entityToBlock
    //This project only implements player entity slots; the source entity is taken as a player, and a non-player is reported with vanilla's players-only error
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

    //EntityFromEntity moves a whole stack from an entity slot into an entity slot, maps to vanilla entityToEntities
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

    //ReadBlockStack reads a copy of the item in a block entity slot, maps to vanilla getBlockItem
    //When the container cannot be fetched or the slot does not apply, a reply has already been sent and null is returned for failure
    private static ItemStack? ReadBlockStack(ServerCommandSource source, BlockPos pos, int slot)
    {
        if (GetContainer(source, pos) is not { } container)
        {
            source.SendFailure($"there is no container at {pos.X} {pos.Y} {pos.Z}");
            return null;
        }
        return ReadStack(source, SlotAccess.ForContainer(container, slot), $"position {pos.X} {pos.Y} {pos.Z}");
    }

    //ReadEntityStack reads a copy of the item in an entity slot, maps to vanilla getItemInSlot
    private static ItemStack? ReadEntityStack(ServerCommandSource source, ServerPlayer player, int slot)
        => ReadStack(source, SlotAccess.ForPlayer(player, slot), player.Profile.Name);

    //ReadStack reads a slot's content; an empty slot returns an empty stack; an inapplicable slot reports source slot missing like vanilla
    private static ItemStack? ReadStack(ServerCommandSource source, SlotAccess access, string where)
    {
        if (ReferenceEquals(access, SlotAccess.Null))
        {
            source.SendFailure($"there is no such slot on {where}");
            return null;
        }
        return access.Get().Copy();
    }

    //SetBlockItem writes an item into a block entity container, maps to vanilla setBlockItem
    private static int SetBlockItem(ServerCommandSource source, BlockPos pos, int slot, ItemStack stack)
    {
        if (GetContainer(source, pos) is not { } container)
        {
            source.SendFailure($"there is no container at {pos.X} {pos.Y} {pos.Z}");
            return 0;
        }
        var where = $"position {pos.X} {pos.Y} {pos.Z}";
        if (!SetSlot(source, SlotAccess.ForContainer(container, slot), slot, stack, where)) return 0;
        source.SendSuccess($"set the slot item at {where} to {Describe(stack)}");
        return 1;
    }

    //SetEntityItem writes an item into a target player's slot, maps to vanilla setEntityItem
    //Vanilla writes a copy per target; the success reply includes the target name only when there is a single target
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
            source.SendFailure($"no target changed at slot {slot}");
            return 0;
        }
        source.SendSuccess(changed.Count == 1
            ? $"set the slot item of {changed[0]} to {Describe(stack)}"
            : $"set the slot item of {changed.Count} targets to {Describe(stack)}");
        return changed.Count;
    }

    //SetSlot writes a slot; returns true on success, reporting failure by reason otherwise
    //Silently checks errors: a failed write is not reported here but summarized by the caller; only an inapplicable slot is reported here
    private static bool SetSlot(ServerCommandSource source, SlotAccess access, int slot, ItemStack stack, string where)
    {
        if (ReferenceEquals(access, SlotAccess.Null))
        {
            source.SendFailure($"there is no slot {slot} on {where}");
            return false;
        }
        return access.Set(stack.Copy());
    }

    //GetContainer gets the block entity container, maps to vanilla getContainer
    private static Container? GetContainer(ServerCommandSource source, BlockPos pos)
        => source.PlayerOrThrow.Level is PersistentServerLevel level
            ? level.BlockUpdateSink?.GetBlockEntity(pos) as Container
            : null;

    //Describe gets the item display name; before the custom name component is wired up the identifier is used
    private static string Describe(ItemStack stack)
        => stack.IsEmpty() ? "air" : stack.GetItem().Id.ToShortString();

    private static ServerCommandSource Source(CommandContext<CommandSourceStack> context)
        => (ServerCommandSource)context.GetSource();
}
