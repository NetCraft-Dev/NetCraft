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

//SetBlockCommand /setblock 命令对应原版 net.minecraft.server.commands.SetBlockCommand
//setblock <坐标> <方块> 直接覆盖 另有 destroy 先破坏原方块 keep 只填空位 replace 覆盖
//strict 跳过光照发包与邻居通知 只在区块重载后可见 对应原版 UPDATE_SKIP_ALL_SIDEEFFECTS
public static class SetBlockCommand
{
    //Mode 放置模式对应原版 SetBlockCommand.Mode
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

    //SetBlock 执行放置 对应原版 setBlock
    //keep 只在该位置为空时替换 destroy 先按掉落破坏原方块
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
            source.SendFailure($"位置 {FormatPos(pos)} 所在区块未加载或超出世界范围");
            return 0;
        }
        if (keepOnly && state.Value != Blocks.AIR.DefaultBlockState)
        {
            source.SendFailure($"位置 {FormatPos(pos)} 已有方块");
            return 0;
        }

        var placeNeeded = true;
        if (mode == Mode.Destroy)
        {
            //destroy 先把原方块按掉落破坏 破坏表现与掉落都走通用链路
            ServerBlockUpdates.BreakBlock(level, players, null, pos);
            //目标是空气且原地已被破坏成空气时无事可做 对应原版 placeNeeded 的判定
            placeNeeded = !(input.State == Blocks.AIR.DefaultBlockState
                && level.GetBlockState(pos) == Blocks.AIR.DefaultBlockState);
        }

        if (placeNeeded && !ServerBlockUpdates.SetBlock(level, players, pos, input.State, strict: strict))
        {
            source.SendFailure($"位置 {FormatPos(pos)} 放置失败 状态未变化");
            return 0;
        }
        source.SendSuccess($"已将方块设置为 {BuiltInRegistries.BLOCK.GetKey(input.State.Owner)} {FormatPos(pos)}");
        return 1;
    }

    private static string FormatPos(BlockPos pos) => $"{pos.X} {pos.Y} {pos.Z}";
}
