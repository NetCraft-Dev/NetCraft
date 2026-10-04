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

//FillCommand /fill 命令对应原版 net.minecraft.server.commands.FillCommand
//fill <起点> <终点> <方块> 填充长方体区域 起点终点任意顺序
//模式 outline 只填外壳 hollow 外壳加空气芯 destroy 先按掉落破坏原方块
//replace 后可接方块谓词 只替换匹配的格子 strict 跳过光照发包与邻居通知
//单次改动上限由世界规则 max_block_modifications 控制 默认 32768
public static class FillCommand
{
    //EmptyOnly keep 模式的过滤器 只处理空气位置 对应原版 isEmptyBlock 判定
    private static readonly Predicate<BlockInWorld> EmptyOnly = world =>
        world.State is { } state && state == Blocks.AIR.DefaultBlockState;

    //Mode 填充模式对应原版 FillCommand.Mode
    private enum Mode
    {
        Replace,
        Outline,
        Hollow,
        Destroy,
    }

    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        //模式与 replace/keep 都挂在 block 参数节点下 位置对应原版 wrapWithMode
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

    //Fill 执行填充 对应原版 fillBlocks
    //逐格按模式决定放什么 未加载区块的格子跳过 改动数为 0 时按原版报失败
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
        //上限受世界规则限制 对应原版 max_block_modifications 默认 32768
        var limit = source.Server.GameRules.GetInt(GameRules.MaxBlockModifications);
        if (area > limit)
        {
            source.SendFailure($"填充区域过大 上限 {limit} 实际 {area}");
            return 0;
        }

        //批量收集变更 对应原版每格只写状态 光照与方块包等全部写完再统一刷一次
        var batch = new BlockChangeBatch(level);
        var count = 0;
        for (var x = minX; x <= maxX; x++)
        for (var y = minY; y <= maxY; y++)
        for (var z = minZ; z <= maxZ; z++)
        {
            var pos = new BlockPos(x, y, z);
            //区块未加载的格子按原版跳过 不作数
            if (level.GetBlockState(pos) is null) continue;
            if (filter is not null && !filter(new BlockInWorld(level, pos, blockEntities))) continue;

            var affected = mode == Mode.Destroy
                && ServerBlockUpdates.BreakBlock(level, players, null, pos);

            if (!TryResolveTarget(mode, pos, minX, minY, minZ, maxX, maxY, maxZ, input, out var target))
            {
                //该格不放方块 只有 destroy 真的破坏了才算一次改动
                if (affected) count++;
                continue;
            }
            if (!batch.Apply(pos, target))
            {
                if (affected) count++;
                continue;
            }
            //方块实体数据只在非 strict 时写入 strict 语义是不产生任何副作用
            if (!strict && input.Nbt is not null && blockEntities.Get(pos) is { } blockEntity)
            {
                blockEntity.LoadAdditional(input.Nbt);
                players.BroadcastAll(blockEntity.GetUpdatePacket());
            }
            count++;
        }
        //strict 只落状态 不发光照不发包不通知邻居
        batch.Flush(players, sideEffects: !strict);

        if (count == 0)
        {
            source.SendFailure("没有方块被改动");
            return 0;
        }
        source.SendSuccess($"已填充 {count} 个方块");
        return count;
    }

    //TryResolveTarget 按模式决定该格放什么 返回 false 表示这格完全不动
    //outline 只处理六个面上的格子 hollow 面上放目标方块内部塞空气
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
