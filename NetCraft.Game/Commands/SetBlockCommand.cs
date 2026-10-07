using NetCraft.Commands;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Server;
using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;

namespace NetCraft.Game.Commands;

//SetBlockCommand /setblock command, maps to vanilla net.minecraft.server.commands.SetBlockCommand
//setblock <pos> <block> overwrites directly; also destroy breaks the original block first, keep only fills empty slots, replace overwrites
//strict skips light updates, packets and neighbor notifications; visible only after a chunk reload, maps to vanilla UPDATE_SKIP_ALL_SIDEEFFECTS
public static class SetBlockCommand
{
    //Mode placement mode, maps to vanilla SetBlockCommand.Mode
    private enum Mode
    {
        Replace,
        Destroy,
    }

    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("setblock")
            .Requires(s => s.HasPermission(2))
            .Then(RequiredArgumentBuilder<CommandSourceStack, BlockCoordinates>
                .Argument("pos", BlockPosArgument.BlockPos())
                .Then(RequiredArgumentBuilder<CommandSourceStack, BlockInput>
                    .Argument("block", BlockStateArgument.Block())
                    .Executes(context => SetBlock(context, Mode.Replace, false, false))
                    .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("destroy")
                        .Executes(context => SetBlock(context, Mode.Destroy, false, false)))
                    .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("keep")
                        .Executes(context => SetBlock(context, Mode.Replace, true, false)))
                    .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("replace")
                        .Executes(context => SetBlock(context, Mode.Replace, false, false)))
                    .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("strict")
                        .Executes(context => SetBlock(context, Mode.Replace, false, true))))));
    }

    //SetBlock performs the placement, maps to vanilla setBlock
    //keep replaces only when the position is empty; destroy breaks the original block with drops first
    private static int SetBlock(CommandContext<CommandSourceStack> context, Mode mode, bool keepOnly, bool strict)
    {
        var source = context.GetSource() as ServerCommandSource;
        if (source is null) return 0;
        if (source.PlayerOrThrow.Level is not PersistentServerLevel level) return 0;
        var pos = BlockPosArgument.GetBlockPos(context, "pos");
        var input = context.GetArgument<BlockInput>("block");
        var players = source.Server.PlayerList;

        var state = level.GetBlockState(pos);
        if (state is null)
        {
            source.SendFailure($"the chunk containing {FormatPos(pos)} is not loaded or out of the world bounds");
            return 0;
        }
        if (keepOnly && state.Value != Blocks.AIR.DefaultBlockState)
        {
            source.SendFailure($"{FormatPos(pos)} already has a block");
            return 0;
        }

        var placeNeeded = true;
        if (mode == Mode.Destroy)
        {
            //destroy first breaks the original block with drops; both the break effect and the drops go through the common chain
            ServerBlockUpdates.BreakBlock(level, players, null, pos);
            //When the target is air and it was already broken to air there is nothing to do, maps to vanilla placeNeeded
            placeNeeded = !(input.State == Blocks.AIR.DefaultBlockState
                && level.GetBlockState(pos) == Blocks.AIR.DefaultBlockState);
        }

        if (placeNeeded && !ServerBlockUpdates.SetBlock(level, players, pos, input.State, strict: strict))
        {
            source.SendFailure($"failed to place at {FormatPos(pos)}: the state did not change");
            return 0;
        }
        source.SendSuccess($"set block to {BuiltInRegistries.BLOCK.GetKey(input.State.Owner)} {FormatPos(pos)}");
        return 1;
    }

    private static string FormatPos(BlockPos pos) => $"{pos.X} {pos.Y} {pos.Z}";
}
