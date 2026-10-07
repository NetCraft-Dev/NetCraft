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

//CloneCommand /clone command, maps to vanilla net.minecraft.server.commands.CloneCommands
//Syntax: clone [from <source dimension>] <begin> <end> [to <target dimension>] <destination> [strict] [replace|masked|filtered <predicate>] [force|move|normal]
//replace copies everything; masked skips air; filtered selects by block predicate
//normal errors when the destination overlaps the source region; force allows overlap; move clears the source after copying
//strict means this batch of changes produces no side effects (no light, no neighbor updates)
public static class CloneCommand
{
    //FilterAir the masked mode filter copies only non-air, maps to vanilla FILTER_AIR
    private static readonly Predicate<BlockInWorld> FilterAir = world =>
        world.State is { } state && state != Blocks.AIR.DefaultBlockState;

    //AlwaysTrue the replace mode filter and the filter when none is given, maps to vanilla c -> b -> true
    private static readonly Predicate<BlockInWorld> AlwaysTrue = _ => true;

    //_barrierState barrier placeholder block; during copying it holds the target cell first so neighbor updates do not change positions not yet written
    private static BlockState? _barrierState;

    //Mode copy mode, maps to vanilla CloneCommands.Mode
    private enum Mode
    {
        Normal,
        Force,
        Move,
    }

    //ModeCanOverlap whether the mode allows the source and destination regions to overlap, maps to vanilla Mode.canOverlap
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

    //BeginEndDestinationAndModeSuffix branch for begin/end and destination, maps to the vanilla same-named method
    //end splits two ways: a destination directly (same dimension), or to <target dimension> then a destination (cross-dimension)
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

    //DestinationAndStrictSuffix destination and strict suffix, maps to the vanilla same-named method
    //Without strict this batch of writes includes neighbor updates; with strict it uses the side-effect-free set
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

    //ModeSuffix filter and mode suffix, maps to vanilla modeSuffix
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

    //AddModeBranch under the filter node hangs the default and force/move/normal branches, maps to vanilla wrapWithCloneMode
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

    //SourceLevel gets the command source's dimension, maps to vanilla c -> c.getSource().getLevel()
    private static PersistentServerLevel? SourceLevel(CommandContext<CommandSourceStack> context)
        => context.GetSource() is ServerCommandSource source
            && source.PlayerOrThrow.Level is PersistentServerLevel level
                ? level
                : null;

    //CloneInfo one cell's pending copy result, maps to vanilla CloneBlockInfo
    private sealed record CloneInfo(BlockPos Pos, BlockState State, CompoundTag? EntityTag,
        BlockState? PreviousAtDestination);

    //Clone performs the copy, maps to vanilla clone
    //First reads into three groups (block entity / full block / other), then places in reverse, writes forward, and finally adds neighbor updates and scheduled ticks
    private static int Clone(CommandContext<CommandSourceStack> context,
        PersistentServerLevel? fromLevel, PersistentServerLevel? toLevel,
        Predicate<BlockInWorld> filter, Mode mode, bool strict)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        if (fromLevel is null || toLevel is null)
        {
            source.SendFailure("the command source is not in a persistent dimension");
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

        //Overlap check: normal mode does not allow source and destination to intersect, or the copy would eat itself
        if (!ModeCanOverlap(mode) && ReferenceEquals(fromLevel, toLevel)
            && destination.X <= fromMaxX && destMaxX >= fromMinX
            && destination.Y <= fromMaxY && destMaxY >= fromMinY
            && destination.Z <= fromMaxZ && destMaxZ >= fromMinZ)
        {
            source.SendFailure("the source and destination regions overlap");
            return 0;
        }

        //The limit is by the whole volume; move is not additionally halved; the limit is controlled by the world rule max_block_modifications
        var area = (long)lenX * lenY * lenZ;
        var limit = source.Server.GameRules.GetInt(GameRules.MaxBlockModifications);
        if (area > limit)
        {
            source.SendFailure($"copy region too large, limit {limit}, actual {area}");
            return 0;
        }

        if (!HasChunksAt(fromLevel, fromMinX, fromMinZ, fromMaxX, fromMaxZ)
            || !HasChunksAt(toLevel, destination.X, destination.Z, destMaxX, destMaxZ))
        {
            source.SendFailure("the source or destination region has unloaded chunks");
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

            //Those with block entities store the entity before the state; full blocks and the rest are separated; the write order follows the vanilla three phases
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

        //Under strict this batch of writes sends not even neighbor and shape updates; under non-strict only the client packet is sent and neighbors are added at the end
        var writeFlags = BlockUpdateFlags.Clients
            | (strict ? BlockUpdateFlags.SkipAllSideEffects : 0);
        var barrierFlags = writeFlags | BlockUpdateFlags.SkipAllSideEffects;
        var barrier = BarrierState;

        if (mode == Mode.Move)
        {
            //Replace the source region with barrier first to block the update chain then clear to air, otherwise falling blocks break mid-move
            foreach (var pos in clear) fromLevel.SetBlock(pos, barrier, barrierFlags);
            var clearFlags = strict ? writeFlags : BlockUpdateFlags.All;
            foreach (var pos in clear) fromLevel.SetBlock(pos, Blocks.AIR.DefaultBlockState, clearFlags);
        }

        var ordered = new List<CloneInfo>(solid.Count + withEntity.Count + other.Count);
        ordered.AddRange(solid);
        ordered.AddRange(withEntity);
        ordered.AddRange(other);

        //Place in reverse first, then write forward; when a cell is written twice the later write wins
        for (var i = ordered.Count - 1; i >= 0; i--)
            toLevel.SetBlock(ordered[i].Pos, barrier, barrierFlags);

        var count = 0;
        foreach (var info in ordered)
            if (toLevel.SetBlock(info.Pos, info.State, writeFlags)) count++;

        //Block entity data is backfilled after the new block entity is created, then the state is written again to settle it
        foreach (var info in withEntity)
        {
            if (info.EntityTag is { } tag && blockEntities.Get(info.Pos) is { } target)
                target.LoadCustomOnly(tag);
            toLevel.SetBlock(info.Pos, info.State, writeFlags);
        }

        //Under non-strict, for each cell the destination's original state is treated as the source block for neighbor updates, maps to vanilla updateNeighboursOnBlockSet
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
            source.SendFailure("no blocks were copied");
            return 0;
        }
        source.SendSuccess($"copied {count} blocks");
        return count;
    }

    //BarrierState the barrier block state, fetched from the block registry on first use
    private static BlockState BarrierState
        => _barrierState ??= BuiltInRegistries.BLOCK
            .GetValue(Identifier.WithDefaultNamespace("barrier"))!.DefaultBlockState;

    //IsFullBlock full-block test; vanilla uses isSolidRender or isCollisionShapeFullBlock
    //This project computes occlusion and collision shapes separately; approximated here as the collision shape filling the whole cell
    private static bool IsFullBlock(BlockState state, PersistentServerLevel level, BlockPos pos)
        => state.Owner is BlockBehaviour behaviour
            && behaviour.IsCollisionShapeFullBlock(state, EmptyBlockGetter.Instance, pos);

    //HasChunksAt whether all chunks covered by the rectangle are loaded, maps to vanilla hasChunksAt
    private static bool HasChunksAt(PersistentServerLevel level, int minX, int minZ, int maxX, int maxZ)
    {
        for (var chunkX = minX >> 4; chunkX <= (maxX >> 4); chunkX++)
        for (var chunkZ = minZ >> 4; chunkZ <= (maxZ >> 4); chunkZ++)
            if (level.GetChunk(new ChunkPos(chunkX, chunkZ)) is null) return false;
        return true;
    }
}
