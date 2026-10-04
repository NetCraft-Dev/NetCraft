using NetCraft.Commands;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Server;
using NetCraft.Game.World.Level;
using NetCraft.Game.World.Level.Block;
using NetCraft.Game.World.Phys.Collision;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Updates;

namespace NetCraft.Game.Commands;

//CloneCommand /clone 命令对应原版 net.minecraft.server.commands.CloneCommands
//语法 clone [from <源维度>] <起点> <终点> [to <目标维度>] <目标点> [strict] [replace|masked|filtered <谓词>] [force|move|normal]
//replace 复制全部 masked 跳过空气 filtered 按方块谓词筛
//normal 目标与原区域重叠时报错 force 允许重叠 move 复制后把源区清空
//strict 表示这一批改动不产生任何副作用(不发光照不发邻居更新)
public static class CloneCommand
{
    //FilterAir masked 模式的过滤器 只复制非空气 对应原版 FILTER_AIR
    private static readonly Predicate<BlockInWorld> FilterAir = world =>
        world.State is { } state && state != Blocks.AIR.DefaultBlockState;

    //AlwaysTrue replace 模式与不带过滤器时的过滤器 对应原版 c -> b -> true
    private static readonly Predicate<BlockInWorld> AlwaysTrue = _ => true;

    //_barrierState barrier 占位方块 复制期间先把目标格占住免得邻居更新把还没写的位置改掉
    private static BlockState? _barrierState;

    //Mode 复制模式对应原版 CloneCommands.Mode
    private enum Mode
    {
        Normal,
        Force,
        Move,
    }

    //ModeCanOverlap 该模式是否允许源区与目标区重叠 对应原版 Mode.canOverlap
    private static bool ModeCanOverlap(Mode mode) => mode is Mode.Force or Mode.Move;

    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        var root = LiteralArgumentBuilder<CommandSourceStack>.Literal("clone");
        root.Requires(s => s.HasPermission(2));
        root.Then(BeginEndDestinationAndModeSuffix(SourceLevel));
        var from = LiteralArgumentBuilder<CommandSourceStack>.Literal("from");
        from.Then(RequiredArgumentBuilder<CommandSourceStack, Identifier>
            .Argument("sourceDimension", DimensionArgument.Dimension())
            .Then(BeginEndDestinationAndModeSuffix(c => DimensionArgument.GetDimension(c, "sourceDimension"))));
        root.Then(from);
        dispatcher.Register(root);
    }

    //BeginEndDestinationAndModeSuffix 起点终点与目标点的分支 对应原版同名方法
    //end 下分两路 直接给目标点(同维度) 或 to <目标维度> 再给目标点(跨维度)
    private static ArgumentBuilder<CommandSourceStack> BeginEndDestinationAndModeSuffix(
        Func<CommandContext<CommandSourceStack>, PersistentServerLevel?> fromDimension)
    {
        var begin = RequiredArgumentBuilder<CommandSourceStack, BlockCoordinates>
            .Argument("begin", BlockPosArgument.BlockPos());
        var end = RequiredArgumentBuilder<CommandSourceStack, BlockCoordinates>
            .Argument("end", BlockPosArgument.BlockPos());
        end.Then(DestinationAndStrictSuffix(fromDimension, SourceLevel));
        var to = LiteralArgumentBuilder<CommandSourceStack>.Literal("to");
        to.Then(RequiredArgumentBuilder<CommandSourceStack, Identifier>
            .Argument("targetDimension", DimensionArgument.Dimension())
            .Then(DestinationAndStrictSuffix(fromDimension,
                c => DimensionArgument.GetDimension(c, "targetDimension"))));
        end.Then(to);
        begin.Then(end);
        return begin;
    }

    //DestinationAndStrictSuffix 目标点与 strict 后缀 对应原版同名方法
    //不加 strict 表示这一批写入要带邻居更新 加了 strict 走无副作用那一套
    private static ArgumentBuilder<CommandSourceStack> DestinationAndStrictSuffix(
        Func<CommandContext<CommandSourceStack>, PersistentServerLevel?> fromDimension,
        Func<CommandContext<CommandSourceStack>, PersistentServerLevel?> toDimension)
    {
        var destination = RequiredArgumentBuilder<CommandSourceStack, BlockCoordinates>
            .Argument("destination", BlockPosArgument.BlockPos());
        ModeSuffix(destination, fromDimension, toDimension, false);
        var strict = LiteralArgumentBuilder<CommandSourceStack>.Literal("strict");
        ModeSuffix(strict, fromDimension, toDimension, true);
        destination.Then(strict);
        return destination;
    }

    //ModeSuffix 过滤器与模式后缀 对应原版 modeSuffix
    private static void ModeSuffix<T>(ArgumentBuilder<CommandSourceStack, T> builder,
        Func<CommandContext<CommandSourceStack>, PersistentServerLevel?> fromDimension,
        Func<CommandContext<CommandSourceStack>, PersistentServerLevel?> toDimension, bool strict)
        where T : ArgumentBuilder<CommandSourceStack, T>
    {
        builder.Executes(c => Clone(c, fromDimension(c), toDimension(c), AlwaysTrue, Mode.Normal, strict));

        var replace = LiteralArgumentBuilder<CommandSourceStack>.Literal("replace");
        AddModeBranch(replace, _ => AlwaysTrue, fromDimension, toDimension, strict);
        builder.Then(replace);

        var masked = LiteralArgumentBuilder<CommandSourceStack>.Literal("masked");
        AddModeBranch(masked, _ => FilterAir, fromDimension, toDimension, strict);
        builder.Then(masked);

        var filtered = LiteralArgumentBuilder<CommandSourceStack>.Literal("filtered");
        var filter = RequiredArgumentBuilder<CommandSourceStack, Predicate<BlockInWorld>>
            .Argument("filter", BlockPredicateArgument.BlockPredicate());
        AddModeBranch(filter, c => BlockPredicateArgument.GetBlockPredicate(c, "filter"),
            fromDimension, toDimension, strict);
        filtered.Then(filter);
        builder.Then(filtered);
    }

    //AddModeBranch 过滤器节点下挂默认与 force/move/normal 四支 对应原版 wrapWithCloneMode
    private static void AddModeBranch<T>(ArgumentBuilder<CommandSourceStack, T> builder,
        Func<CommandContext<CommandSourceStack>, Predicate<BlockInWorld>> filter,
        Func<CommandContext<CommandSourceStack>, PersistentServerLevel?> fromDimension,
        Func<CommandContext<CommandSourceStack>, PersistentServerLevel?> toDimension, bool strict)
        where T : ArgumentBuilder<CommandSourceStack, T>
    {
        builder.Executes(c => Clone(c, fromDimension(c), toDimension(c), filter(c), Mode.Normal, strict));
        var force = LiteralArgumentBuilder<CommandSourceStack>.Literal("force");
        force.Executes(c => Clone(c, fromDimension(c), toDimension(c), filter(c), Mode.Force, strict));
        builder.Then(force);
        var move = LiteralArgumentBuilder<CommandSourceStack>.Literal("move");
        move.Executes(c => Clone(c, fromDimension(c), toDimension(c), filter(c), Mode.Move, strict));
        builder.Then(move);
        var normal = LiteralArgumentBuilder<CommandSourceStack>.Literal("normal");
        normal.Executes(c => Clone(c, fromDimension(c), toDimension(c), filter(c), Mode.Normal, strict));
        builder.Then(normal);
    }

    //SourceLevel 取命令源所在维度 对应原版 c -> c.getSource().getLevel()
    private static PersistentServerLevel? SourceLevel(CommandContext<CommandSourceStack> context)
        => context.GetSource() is ServerCommandSource source
            && source.PlayerOrThrow.Level is PersistentServerLevel level
                ? level
                : null;

    //CloneInfo 一格待写的复制结果 对应原版 CloneBlockInfo
    private sealed record CloneInfo(BlockPos Pos, BlockState State, CompoundTag? EntityTag,
        BlockState? PreviousAtDestination);

    //Clone 执行复制 对应原版 clone
    //先按方块实体/实心/其他分三组读出来 再反向占位 正向写入 最后统一补邻居更新与调度刻
    private static int Clone(CommandContext<CommandSourceStack> context,
        PersistentServerLevel? fromLevel, PersistentServerLevel? toLevel,
        Predicate<BlockInWorld> filter, Mode mode, bool strict)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        if (fromLevel is null || toLevel is null)
        {
            source.SendFailure("命令源不在持久化维度里");
            return 0;
        }

        var begin = BlockPosArgument.GetBlockPos(context, "begin");
        var end = BlockPosArgument.GetBlockPos(context, "end");
        var destination = BlockPosArgument.GetBlockPos(context, "destination");

        var fromMinX = Math.Min(begin.X, end.X);
        var fromMinY = Math.Min(begin.Y, end.Y);
        var fromMinZ = Math.Min(begin.Z, end.Z);
        var fromMaxX = Math.Max(begin.X, end.X);
        var fromMaxY = Math.Max(begin.Y, end.Y);
        var fromMaxZ = Math.Max(begin.Z, end.Z);
        var lenX = fromMaxX - fromMinX + 1;
        var lenY = fromMaxY - fromMinY + 1;
        var lenZ = fromMaxZ - fromMinZ + 1;

        var destMaxX = destination.X + lenX - 1;
        var destMaxY = destination.Y + lenY - 1;
        var destMaxZ = destination.Z + lenZ - 1;

        //重叠检测 normal 模式不允许源与目标相交 否则复制过程会自己吃掉自己
        if (!ModeCanOverlap(mode) && ReferenceEquals(fromLevel, toLevel)
            && destination.X <= fromMaxX && destMaxX >= fromMinX
            && destination.Y <= fromMaxY && destMaxY >= fromMinY
            && destination.Z <= fromMaxZ && destMaxZ >= fromMinZ)
        {
            source.SendFailure("源区域与目标区域重叠");
            return 0;
        }

        //上限按整块体积算 move 不额外折半 上限由世界规则 max_block_modifications 控制
        var area = (long)lenX * lenY * lenZ;
        var limit = source.Server.GameRules.GetInt(GameRules.MaxBlockModifications);
        if (area > limit)
        {
            source.SendFailure($"复制区域过大 上限 {limit} 实际 {area}");
            return 0;
        }

        if (!HasChunksAt(fromLevel, fromMinX, fromMinZ, fromMaxX, fromMaxZ)
            || !HasChunksAt(toLevel, destination.X, destination.Z, destMaxX, destMaxZ))
        {
            source.SendFailure("源区域或目标区域存在未加载的区块");
            return 0;
        }

        var offset = new BlockPos(destination.X - fromMinX, destination.Y - fromMinY, destination.Z - fromMinZ);
        var blockEntities = source.Server.BlockEntities;

        var solid = new List<CloneInfo>();
        var withEntity = new List<CloneInfo>();
        var other = new List<CloneInfo>();
        var clear = new LinkedList<BlockPos>();

        for (var z = fromMinZ; z <= fromMaxZ; z++)
        for (var y = fromMinY; y <= fromMaxY; y++)
        for (var x = fromMinX; x <= fromMaxX; x++)
        {
            var sourcePos = new BlockPos(x, y, z);
            var state = fromLevel.GetBlockState(sourcePos);
            if (state is not { } blockState) continue;
            if (!filter(new BlockInWorld(fromLevel, sourcePos, blockEntities))) continue;

            var destinationPos = new BlockPos(sourcePos.X + offset.X, sourcePos.Y + offset.Y,
                sourcePos.Z + offset.Z);
            var previous = toLevel.GetBlockState(destinationPos);

            //有方块实体的先存实体再存状态 实心方块与其余方块分开 写入顺序按原版三段走
            if (blockEntities.Get(sourcePos) is { } entity)
            {
                withEntity.Add(new CloneInfo(destinationPos, blockState, entity.SaveWithFullMetadata(), previous));
                clear.AddLast(sourcePos);
                continue;
            }
            if (IsFullBlock(blockState, fromLevel, sourcePos))
            {
                solid.Add(new CloneInfo(destinationPos, blockState, null, previous));
                clear.AddLast(sourcePos);
                continue;
            }
            other.Add(new CloneInfo(destinationPos, blockState, null, previous));
            clear.AddFirst(sourcePos);
        }

        //strict 时这一批写入连邻居更新与形状更新都不发 非 strict 只发客户端包 最后统一补邻居
        var writeFlags = BlockUpdateFlags.Clients
            | (strict ? BlockUpdateFlags.SkipAllSideEffects : 0);
        var barrierFlags = writeFlags | BlockUpdateFlags.SkipAllSideEffects;
        var barrier = BarrierState;

        if (mode == Mode.Move)
        {
            //先把源区换成 barrier 挡住更新链再把它们清成空气 否则掉落的方块会在搬运途中出事
            foreach (var pos in clear) fromLevel.SetBlock(pos, barrier, barrierFlags);
            var clearFlags = strict ? writeFlags : BlockUpdateFlags.All;
            foreach (var pos in clear) fromLevel.SetBlock(pos, Blocks.AIR.DefaultBlockState, clearFlags);
        }

        var ordered = new List<CloneInfo>(solid.Count + withEntity.Count + other.Count);
        ordered.AddRange(solid);
        ordered.AddRange(withEntity);
        ordered.AddRange(other);

        //反向先占位 正向再写 同格被两次写入时后写的赢
        for (var i = ordered.Count - 1; i >= 0; i--)
            toLevel.SetBlock(ordered[i].Pos, barrier, barrierFlags);

        var count = 0;
        foreach (var info in ordered)
            if (toLevel.SetBlock(info.Pos, info.State, writeFlags)) count++;

        //方块实体数据在新方块实体建出来之后回填 再写一次状态让它落定
        foreach (var info in withEntity)
        {
            if (info.EntityTag is { } tag && blockEntities.Get(info.Pos) is { } target)
                target.LoadCustomOnly(tag);
            toLevel.SetBlock(info.Pos, info.State, writeFlags);
        }

        //非 strict 时把目标格原本的状态当作源方块逐格补邻居更新 对应原版 updateNeighboursOnBlockSet
        if (!strict)
        {
            for (var i = ordered.Count - 1; i >= 0; i--)
                if (ordered[i].PreviousAtDestination is { } previous)
                    toLevel.UpdateNeighborsAt(ordered[i].Pos, previous.Owner);
        }

        toLevel.BlockTicks.CopyAreaFrom(fromLevel.BlockTicks,
            fromMinX, fromMinY, fromMinZ, fromMaxX, fromMaxY, fromMaxZ, offset);

        if (count == 0)
        {
            source.SendFailure("没有方块被复制");
            return 0;
        }
        source.SendSuccess($"已复制 {count} 个方块");
        return count;
    }

    //BarrierState barrier 方块状态 首次使用时从方块注册表取
    private static BlockState BarrierState
        => _barrierState ??= BuiltInRegistries.BLOCK
            .GetValue(Identifier.WithDefaultNamespace("barrier"))!.DefaultBlockState;

    //IsFullBlock 实心判定 原版用 isSolidRender 或 isCollisionShapeFullBlock
    //本作的遮挡与碰撞形状是分开算的 这里按碰撞形状占满整格近似
    private static bool IsFullBlock(BlockState state, PersistentServerLevel level, BlockPos pos)
        => state.Owner is BlockBehaviour behaviour
            && behaviour.IsCollisionShapeFullBlock(state, EmptyBlockGetter.Instance, pos);

    //HasChunksAt 该矩形覆盖的区块是否全部已加载 对应原版 hasChunksAt
    private static bool HasChunksAt(PersistentServerLevel level, int minX, int minZ, int maxX, int maxZ)
    {
        for (var chunkX = minX >> 4; chunkX <= (maxX >> 4); chunkX++)
        for (var chunkZ = minZ >> 4; chunkZ <= (maxZ >> 4); chunkZ++)
            if (level.GetChunk(new ChunkPos(chunkX, chunkZ)) is null) return false;
        return true;
    }
}
