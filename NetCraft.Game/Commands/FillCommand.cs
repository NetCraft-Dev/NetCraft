using NetCraft.Commands;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Server;
using NetCraft.Game.World.Level;
using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Storage;

namespace NetCraft.Game.Commands;

//FillCommand /fill command, maps to vanilla net.minecraft.server.commands.FillCommand
//fill <begin> <end> <block> fills a cuboid region; begin and end in any order
//Modes: outline fills only the shell; hollow adds an air core; destroy breaks the original blocks with drops
//After replace a block predicate may follow to replace only matching cells; strict skips light updates, packets and neighbor notifications
//The per-call change limit is controlled by the world rule max_block_modifications, default 32768
public static class FillCommand
{
    //EmptyOnly the keep mode filter handles only air positions, maps to vanilla isEmptyBlock
    private static readonly Predicate<BlockInWorld> EmptyOnly = world =>
        world.State is { } state && state == Blocks.AIR.DefaultBlockState;

    //Mode fill mode, maps to vanilla FillCommand.Mode
    private enum Mode
    {
        Replace,
        Outline,
        Hollow,
        Destroy,
    }

    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        //The mode and replace/keep both hang under the block argument node, positions matching vanilla wrapWithMode
        var block = RequiredArgumentBuilder<CommandSourceStack, BlockInput>
            .Argument("block", BlockStateArgument.Block());
        block.Executes(context => Fill(context, Mode.Replace, false, null));
        block.Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("outline")
            .Executes(context => Fill(context, Mode.Outline, false, null)));
        block.Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("hollow")
            .Executes(context => Fill(context, Mode.Hollow, false, null)));
        block.Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("destroy")
            .Executes(context => Fill(context, Mode.Destroy, false, null)));
        block.Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("strict")
            .Executes(context => Fill(context, Mode.Replace, true, null)));
        block.Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("replace")
            .Executes(context => Fill(context, Mode.Replace, false, null))
            .Then(RequiredArgumentBuilder<CommandSourceStack, Predicate<BlockInWorld>>
                .Argument("filter", BlockPredicateArgument.BlockPredicate())
                .Executes(context => Fill(context, Mode.Replace, false,
                    BlockPredicateArgument.GetBlockPredicate(context, "filter")))));
        block.Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("keep")
            .Executes(context => Fill(context, Mode.Replace, false, EmptyOnly)));

        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("fill")
            .Requires(s => s.HasPermission(2))
            .Then(RequiredArgumentBuilder<CommandSourceStack, BlockCoordinates>
                .Argument("from", BlockPosArgument.BlockPos())
                .Then(RequiredArgumentBuilder<CommandSourceStack, BlockCoordinates>
                    .Argument("to", BlockPosArgument.BlockPos())
                    .Then(block))));
    }

    //Fill performs the fill, maps to vanilla fillBlocks
    //Per cell the mode decides what to place; unloaded chunk cells are skipped; a change count of 0 fails like vanilla
    private static int Fill(CommandContext<CommandSourceStack> context, Mode mode, bool strict,
        Predicate<BlockInWorld>? filter)
    {
        var source = context.GetSource() as ServerCommandSource;
        if (source is null) return 0;
        if (source.PlayerOrThrow.Level is not PersistentServerLevel level) return 0;
        var from = BlockPosArgument.GetBlockPos(context, "from");
        var to = BlockPosArgument.GetBlockPos(context, "to");
        var input = context.GetArgument<BlockInput>("block");
        var players = source.Server.PlayerList;
        var blockEntities = source.Server.BlockEntities;

        var minX = Math.Min(from.X, to.X);
        var minY = Math.Min(from.Y, to.Y);
        var minZ = Math.Min(from.Z, to.Z);
        var maxX = Math.Max(from.X, to.X);
        var maxY = Math.Max(from.Y, to.Y);
        var maxZ = Math.Max(from.Z, to.Z);

        var area = (long)(maxX - minX + 1) * (maxY - minY + 1) * (maxZ - minZ + 1);
        //The limit is bounded by the world rule, maps to vanilla max_block_modifications default 32768
        var limit = source.Server.GameRules.GetInt(GameRules.MaxBlockModifications);
        if (area > limit)
        {
            source.SendFailure($"fill region too large, limit {limit}, actual {area}");
            return 0;
        }

        //Collect changes in a batch, maps to vanilla writing only the state per cell and flushing light and block packets once at the end
        var batch = new BlockChangeBatch(level);
        var count = 0;
        for (var x = minX; x <= maxX; x++)
        for (var y = minY; y <= maxY; y++)
        for (var z = minZ; z <= maxZ; z++)
        {
            var pos = new BlockPos(x, y, z);
            //Cells in unloaded chunks are skipped like vanilla and do not count
            if (level.GetBlockState(pos) is null) continue;
            if (filter is not null && !filter(new BlockInWorld(level, pos, blockEntities))) continue;

            var affected = mode == Mode.Destroy
                && ServerBlockUpdates.BreakBlock(level, players, null, pos);

            if (!TryResolveTarget(mode, pos, minX, minY, minZ, maxX, maxY, maxZ, input, out var target))
            {
                //No block is placed in that cell; only a real destroy counts as a change
                if (affected) count++;
                continue;
            }
            if (!batch.Apply(pos, target))
            {
                if (affected) count++;
                continue;
            }
            //Block entity data is written only when not strict; strict means no side effects at all
            if (!strict && input.Nbt is not null && blockEntities.Get(pos) is { } blockEntity)
            {
                blockEntity.LoadAdditional(input.Nbt);
                players.BroadcastAll(blockEntity.GetUpdatePacket());
            }
            count++;
        }
        //strict only writes the state: no light, no packet, no neighbor notification
        batch.Flush(players, sideEffects: !strict);

        if (count == 0)
        {
            source.SendFailure("no blocks were changed");
            return 0;
        }
        source.SendSuccess($"filled {count} blocks");
        return count;
    }

    //TryResolveTarget decides by mode what to place in a cell; returns false when the cell is not touched at all
    //outline only handles the cells on the six faces; hollow places the target block on the faces and air inside
    private static bool TryResolveTarget(Mode mode, BlockPos pos,
        int minX, int minY, int minZ, int maxX, int maxY, int maxZ, BlockInput input, out BlockState target)
    {
        var onShell = pos.X == minX || pos.X == maxX
            || pos.Y == minY || pos.Y == maxY
            || pos.Z == minZ || pos.Z == maxZ;
        if (mode == Mode.Outline && !onShell)
        {
            target = default;
            return false;
        }
        target = mode == Mode.Hollow && !onShell ? Blocks.AIR.DefaultBlockState : input.State;
        return true;
    }
}
