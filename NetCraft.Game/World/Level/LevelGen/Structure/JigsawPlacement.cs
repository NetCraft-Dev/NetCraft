using NetCraft.Logging;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Storage.Chunk;
using NetCraft.Util;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//JigsawPlacement jigsaw assembly, maps to vanilla net.minecraft.world.level.levelgen.structure.pools.JigsawPlacement
//Starts from one element of the start pool and connects elements of target pools layer by layer through jigsaw blocks until depth is exhausted
//Vanilla samples heights through the RandomState WORLD_SURFACE_WG cache; here a separate random source calls the generator for surface heights
public static class JigsawPlacement
{
    //EmptyPoolId built-in empty pool name, maps to vanilla Pools.EMPTY; an empty pool is legal and does not warn
    private static readonly Identifier EmptyPoolId = Identifier.WithDefaultNamespace("empty");

    //AddPieces assembly entry point returning the generation stub; returns null when there is no usable start element or it is out of bounds, maps to vanilla addPieces
    public static GenerationStub? AddPieces(GenerationContext context, StructureTemplateManager structureTemplateManager,
        Holder<NetCraft.Registry.StructureTemplatePool> startPool, Identifier? startJigsaw, int maxDepth,
        BlockPos position, bool doExpansionHack, NetCraft.Registry.Heightmap.Types? projectStartToHeightmap,
        JigsawStructure.MaxDistance maxDistanceFromCenter, PoolAliasLookup poolAliasLookup,
        DimensionPadding dimensionPadding, LiquidSettings liquidSettings)
    {
        var chunkGenerator = context.ChunkGenerator;
        var heightAccessor = context.HeightAccessor;
        var random = context.Random;

        var centerRotation = StructureTransforms.GetRandomRotation(random);
        var centerPool = ResolvePool(startPool, poolAliasLookup);
        var centerElement = centerPool.GetRandomTemplate(random);
        if (ReferenceEquals(centerElement, EmptyPoolElement.Instance)) return null;

        BlockPos anchor;
        if (startJigsaw is { } targetJigsawId)
        {
            var found = GetRandomNamedJigsaw(centerElement, targetJigsawId, position, centerRotation,
                structureTemplateManager, random);
            if (found is null)
            {
                Log.Warning($"No jigsaw block named {targetJigsawId} in the start pool");
                return null;
            }
            anchor = found.Value;
        }
        else
        {
            anchor = position;
        }

        //Offset of the anchor relative to the structure origin; the element is shifted back so the anchor lands on the origin
        var localAnchorPosition = anchor.Offset(-position.X, -position.Y, -position.Z);
        var adjustedPosition = position.Offset(-localAnchorPosition.X, -localAnchorPosition.Y, -localAnchorPosition.Z);
        var centerPiece = new PoolElementStructurePiece(structureTemplateManager, centerElement, adjustedPosition,
            centerElement.GroundLevelDelta, centerRotation,
            centerElement.GetBoundingBox(structureTemplateManager, adjustedPosition, centerRotation), liquidSettings);

        var box = centerPiece.BoundingBox;
        var centerX = (box.MaxX + box.MinX) / 2;
        var centerZ = (box.MaxZ + box.MinZ) / 2;
        var bottomY = projectStartToHeightmap is null
            ? adjustedPosition.Y
            : position.Y + GetFirstFreeHeight(chunkGenerator, centerX, centerZ, heightAccessor, random);
        var oldAbsoluteGroundY = box.MinY + centerPiece.GroundLevelDelta;
        centerPiece.Move(0, bottomY - oldAbsoluteGroundY, 0);
        if (IsStartTooCloseToWorldHeightLimits(heightAccessor, dimensionPadding, centerPiece.BoundingBox))
            return null;

        var centerY = bottomY + localAnchorPosition.Y;
        //In vanilla the box and the piece's bounding box are the same mutable object, so the move applies to both; here the already-moved box is used
        return new GenerationStub(new BlockPos(centerX, centerY, centerZ),
            builder => PlaceChildren(context, structureTemplateManager, chunkGenerator, heightAccessor, random,
                centerPiece, maxDepth, doExpansionHack, centerX, centerY, centerZ, centerPiece.BoundingBox,
                maxDistanceFromCenter, dimensionPadding, poolAliasLookup, liquidSettings, builder));
    }

    //IsStartTooCloseToWorldHeightLimits whether the center piece touches the dimension padding at the top or bottom, maps to the identically named vanilla method
    private static bool IsStartTooCloseToWorldHeightLimits(LevelHeightAccessor heightAccessor,
        DimensionPadding dimensionPadding, BoundingBoxInt centerPieceBox)
    {
        if (dimensionPadding == DimensionPadding.Zero) return false;
        var minYWithPadding = heightAccessor.MinBuildHeight + dimensionPadding.Bottom;
        //Vanilla getMaxY is the highest placeable block; this project's MaxBuildHeight is an exclusive upper bound, so subtract one
        var maxYWithPadding = heightAccessor.MaxBuildHeight - 1 - dimensionPadding.Top;
        return centerPieceBox.MinY < minYWithPadding || centerPieceBox.MaxY > maxYWithPadding;
    }

    //GetRandomNamedJigsaw returns the first jigsaw block position of the given name after shuffling, maps to vanilla getRandomNamedJigsaw
    private static BlockPos? GetRandomNamedJigsaw(StructurePoolElement element, Identifier targetJigsawId,
        BlockPos position, Rotation rotation, StructureTemplateManager structureTemplateManager, RandomSource random)
    {
        foreach (var jigsaw in element.GetShuffledJigsawBlocks(structureTemplateManager, position, rotation, random))
        {
            if (jigsaw.Name == targetJigsawId) return jigsaw.Info.Pos;
        }
        return null;
    }

    //PlaceChildren assembles the pieces other than the center piece, maps to the deferred section of vanilla addPieces
    private static void PlaceChildren(GenerationContext context, StructureTemplateManager structureTemplateManager,
        ChunkGenerator chunkGenerator, LevelHeightAccessor heightAccessor, RandomSource random,
        PoolElementStructurePiece centerPiece, int maxDepth, bool doExpansionHack, int centerX, int centerY,
        int centerZ, BoundingBoxInt box, JigsawStructure.MaxDistance maxDistanceFromCenter,
        DimensionPadding dimensionPadding, PoolAliasLookup poolAliasLookup, LiquidSettings liquidSettings,
        StructurePiecesBuilder builder)
    {
        var pieces = new List<PoolElementStructurePiece> { centerPiece };
        //At zero depth vanilla returns immediately and hands out no pieces at all
        if (maxDepth <= 0) return;

        var limit = new AABB(
            centerX - maxDistanceFromCenter.Horizontal,
            Math.Max(centerY - maxDistanceFromCenter.Vertical, heightAccessor.MinBuildHeight + dimensionPadding.Bottom),
            centerZ - maxDistanceFromCenter.Horizontal,
            centerX + maxDistanceFromCenter.Horizontal + 1,
            Math.Min(centerY + maxDistanceFromCenter.Vertical + 1, heightAccessor.MaxBuildHeight - dimensionPadding.Top),
            centerZ + maxDistanceFromCenter.Horizontal + 1);
        var shape = Shapes.Join(Shapes.Create(limit), Shapes.Create(ToAabb(box)), BooleanOps.OnlyFirst);

        var placer = new Placer(chunkGenerator, structureTemplateManager, maxDepth, pieces, random, context.Seed);
        placer.TryPlacingChildren(centerPiece, new MutableShape(shape), 0, doExpansionHack, heightAccessor,
            poolAliasLookup, liquidSettings);
        while (placer.Placing.HasNext)
        {
            var state = placer.Placing.Next();
            placer.TryPlacingChildren(state.Piece, state.Free, state.Depth, doExpansionHack, heightAccessor,
                poolAliasLookup, liquidSettings);
        }

        foreach (var piece in pieces) builder.AddPiece(piece);
    }

    //ResolvePool rewrites the start pool by alias then looks it up again in the registry, maps to the flatMap at the start of vanilla addPieces
    private static StructureTemplatePool ResolvePool(Holder<NetCraft.Registry.StructureTemplatePool> startPool,
        PoolAliasLookup poolAliasLookup)
    {
        if (startPool.UnwrapKey() is { } key)
        {
            var aliased = BuiltInRegistries.TEMPLATE_POOL.GetValue(poolAliasLookup.Lookup(key.Identifier));
            if (aliased is StructureTemplatePool aliasedPool) return aliasedPool;
        }
        return startPool.Value as StructureTemplatePool
            ?? throw new InvalidOperationException("start pool is not a Game layer template pool implementation");
    }

    //GetFirstFreeHeight returns the first free height at the surface, maps to vanilla ChunkGenerator.getFirstFreeHeight
    //This project's generator lacks that method, so GetBaseHeight plus one is the equivalent; the random state vanilla passes becomes a local random source
    private static int GetFirstFreeHeight(ChunkGenerator generator, int x, int z, LevelHeightAccessor heightAccessor,
        RandomSource random)
        => generator.GetBaseHeight(x, z, (int)NetCraft.Registry.Heightmap.Types.WorldSurfaceWg, heightAccessor, random) + 1;

    //ToAabb converts an integer bounding box to a float box, maps to vanilla AABB.of taking the inclusive upper corner plus one
    private static AABB ToAabb(BoundingBoxInt box)
        => new(box.MinX, box.MinY, box.MinZ, box.MaxX + 1, box.MaxY + 1, box.MaxZ + 1);

    //IsInside inclusive containment test, maps to vanilla BoundingBox.isInside
    private static bool IsInside(BoundingBoxInt box, BlockPos pos)
        => pos.X >= box.MinX && pos.X <= box.MaxX
            && pos.Y >= box.MinY && pos.Y <= box.MaxY
            && pos.Z >= box.MinZ && pos.Z <= box.MaxZ;

    //Moved translates the bounding box along Y, maps to vanilla BoundingBox.move
    private static BoundingBoxInt Moved(BoundingBoxInt box, int dy)
        => box with { MinY = box.MinY + dy, MaxY = box.MaxY + dy };

    //MutableShape a mutable shape reference, maps to vanilla MutableObject<VoxelShape>
    private sealed class MutableShape
    {
        public MutableShape(VoxelShape? value) => Value = value;

        public VoxelShape? Value { get; set; }
    }

    //PieceState a piece to expand with the free shape available downstream, maps to vanilla record PieceState
    private sealed record PieceState(PoolElementStructurePiece Piece, MutableShape Free, int Depth);

    //Placer the assembler; connects target pieces layer by layer through the source piece's jigsaw blocks, maps to vanilla JigsawPlacement.Placer
    private sealed class Placer
    {
        private readonly ChunkGenerator _chunkGenerator;
        private readonly StructureTemplateManager _structureTemplateManager;
        private readonly List<PoolElementStructurePiece> _pieces;
        private readonly RandomSource _random;
        //Random source dedicated to height sampling; vanilla separates it via RandomState from the shuffle random source, so here it is forked separately to avoid perturbing the shuffle sequence
        private readonly RandomSource _heightRandom;

        public Placer(ChunkGenerator chunkGenerator, StructureTemplateManager structureTemplateManager, int maxDepth,
            List<PoolElementStructurePiece> pieces, RandomSource random, long seed)
        {
            _chunkGenerator = chunkGenerator;
            _structureTemplateManager = structureTemplateManager;
            MaxDepth = maxDepth;
            _pieces = pieces;
            _random = random;
            _heightRandom = RandomSource.Create(seed);
        }

        //MaxDepth maximum assembly depth
        public int MaxDepth { get; }

        //Placing queue of pieces to expand, dequeued by placement priority, maps to vanilla SequencedPriorityIterator
        public SequencedPriorityIterator<PieceState> Placing { get; } = new();

        //TryPlacingChildren connects target pieces from the source piece's jigsaw blocks, maps to vanilla tryPlacingChildren
        public void TryPlacingChildren(PoolElementStructurePiece sourcePiece, MutableShape contextFree, int depth,
            bool doExpansionHack, LevelHeightAccessor heightAccessor, PoolAliasLookup poolAliasLookup,
            LiquidSettings liquidSettings)
        {
            var sourceElement = sourcePiece.Element;
            var sourceBoxPosition = sourcePiece.Position;
            var sourceRotation = sourcePiece.Rotation;
            var sourceRigid = sourceElement.Projection == StructureTemplatePool.Projection.Rigid;
            var sourceFree = new MutableShape(null);
            var sourceBox = sourcePiece.BoundingBox;
            var sourceBoxY = sourceBox.MinY;

            foreach (var sourceJigsaw in sourceElement.GetShuffledJigsawBlocks(_structureTemplateManager,
                         sourceBoxPosition, sourceRotation, _random))
            {
                var sourceJigsawInfo = sourceJigsaw.Info;
                var sourceDirection = JigsawBlock.GetFrontFacing(sourceJigsawInfo.State);
                var sourceJigsawPos = sourceJigsawInfo.Pos;
                var targetJigsawPos = sourceJigsawPos.Relative(sourceDirection, 1);
                var sourceJigsawLocalY = sourceJigsawPos.Y - sourceBoxY;
                var sourceJigsawBaseHeight = int.MinValue;

                var poolName = poolAliasLookup.Lookup(sourceJigsaw.Pool);
                var targetPool = BuiltInRegistries.TEMPLATE_POOL.GetValue(poolName) as StructureTemplatePool;
                if (targetPool is null)
                {
                    Log.Warning($"Empty or missing pool {poolName}");
                    continue;
                }
                if (targetPool.Size() == 0 && poolName != EmptyPoolId)
                {
                    Log.Warning($"Empty or missing pool {poolName}");
                    continue;
                }
                var fallbackHolder = targetPool.Fallback;
                //When the reference is not bound yet, treat it as the vanilla "empty and not the built-in empty pool" case and conservatively skip rather than let it throw
                var fallbackPool = fallbackHolder.IsBound() ? fallbackHolder.Value as StructureTemplatePool : null;
                var fallbackName = fallbackHolder.UnwrapKey()?.Identifier;
                if (fallbackPool is null || (fallbackPool.Size() == 0 && fallbackName != EmptyPoolId))
                {
                    Log.Warning($"Empty or missing fallback pool {fallbackName?.ToString() ?? "<unbound>"}");
                    continue;
                }

                //If the target jigsaw falls inside the source bounding box, use the source piece's own free shape; otherwise use the one passed down from the parent
                var attachInsideSource = IsInside(sourceBox, targetJigsawPos);
                MutableShape childrenFree;
                if (attachInsideSource)
                {
                    childrenFree = sourceFree;
                    sourceFree.Value ??= Shapes.Create(ToAabb(sourceBox));
                }
                else
                {
                    childrenFree = contextFree;
                }

                var targetPieces = new List<StructurePoolElement>();
                if (depth != MaxDepth) targetPieces.AddRange(targetPool.GetShuffledTemplates(_random));
                targetPieces.AddRange(fallbackPool.GetShuffledTemplates(_random));

                var placementPriority = sourceJigsaw.PlacementPriority;
                var placed = false;
                foreach (var targetElement in targetPieces)
                {
                    //Once an empty element is drawn, stop looking further at this source jigsaw
                    if (ReferenceEquals(targetElement, EmptyPoolElement.Instance)) break;
                    foreach (var targetRotation in StructureTransforms.GetShuffledRotations(_random))
                    {
                        var targetJigsaws = targetElement.GetShuffledJigsawBlocks(_structureTemplateManager, BlockPos.Zero,
                            targetRotation, _random);
                        var hackBox = targetElement.GetBoundingBox(_structureTemplateManager, BlockPos.Zero, targetRotation);
                        var expandTo = ComputeExpandTo(doExpansionHack, hackBox, targetJigsaws, poolAliasLookup);
                        foreach (var targetJigsaw in targetJigsaws)
                        {
                            if (!JigsawBlock.CanAttach(sourceJigsaw, targetJigsaw)) continue;

                            var targetJigsawLocalPos = targetJigsaw.Info.Pos;
                            var rawTargetBoxPos = targetJigsawPos.Offset(-targetJigsawLocalPos.X, -targetJigsawLocalPos.Y,
                                -targetJigsawLocalPos.Z);
                            var rawTargetBox = targetElement.GetBoundingBox(_structureTemplateManager, rawTargetBoxPos,
                                targetRotation);
                            var rawTargetY = rawTargetBox.MinY;
                            var targetRigid = targetElement.Projection == StructureTemplatePool.Projection.Rigid;
                            var targetJigsawLocalY = targetJigsawLocalPos.Y;
                            var deltaY = sourceJigsawLocalY - targetJigsawLocalY + sourceDirection.StepY;

                            int targetBoxY;
                            if (sourceRigid && targetRigid)
                            {
                                targetBoxY = sourceBoxY + deltaY;
                            }
                            else
                            {
                                //A non-rigid connection snaps to the surface; the surface height of the source jigsaw column is cached per source jigsaw position
                                if (sourceJigsawBaseHeight == int.MinValue)
                                {
                                    sourceJigsawBaseHeight = GetFirstFreeHeight(_chunkGenerator, sourceJigsawPos.X,
                                        sourceJigsawPos.Z, heightAccessor, _heightRandom);
                                }
                                targetBoxY = sourceJigsawBaseHeight - targetJigsawLocalY;
                            }

                            var yOffset = targetBoxY - rawTargetY;
                            var targetBox = Moved(rawTargetBox, yOffset);
                            var targetBoxPosition = rawTargetBoxPos.Offset(0, yOffset, 0);
                            if (expandTo > 0)
                            {
                                var newSize = Math.Max(expandTo + 1, targetBox.MaxY - targetBox.MinY);
                                targetBox = Encapsulate(targetBox, targetBox.MinX, targetBox.MinY + newSize, targetBox.MinZ);
                            }

                            //Deflating by a quarter block and intersecting an existing free shape means they overlap, so try the next candidate
                            if (Shapes.JoinIsNotEmpty(childrenFree.Value!, Shapes.Create(ToAabb(targetBox).Deflate(0.25)),
                                    BooleanOps.OnlySecond)) continue;
                            childrenFree.Value = Shapes.JoinUnoptimized(childrenFree.Value!,
                                Shapes.Create(ToAabb(targetBox)), BooleanOps.OnlyFirst);

                            var sourceGroundLevelDelta = sourcePiece.GroundLevelDelta;
                            var targetGroundLevelDelta = targetRigid
                                ? sourceGroundLevelDelta - deltaY
                                : targetElement.GroundLevelDelta;
                            var targetPiece = new PoolElementStructurePiece(_structureTemplateManager, targetElement,
                                targetBoxPosition, targetGroundLevelDelta, targetRotation, targetBox, liquidSettings);

                            int junctionY;
                            if (sourceRigid)
                            {
                                junctionY = sourceBoxY + sourceJigsawLocalY;
                            }
                            else if (targetRigid)
                            {
                                junctionY = targetBoxY + targetJigsawLocalY;
                            }
                            else
                            {
                                if (sourceJigsawBaseHeight == int.MinValue)
                                {
                                    sourceJigsawBaseHeight = GetFirstFreeHeight(_chunkGenerator, sourceJigsawPos.X,
                                        sourceJigsawPos.Z, heightAccessor, _heightRandom);
                                }
                                junctionY = sourceJigsawBaseHeight + deltaY / 2;
                            }

                            sourcePiece.AddJunction(new JigsawJunction(targetJigsawPos.X,
                                junctionY - sourceJigsawLocalY + sourceGroundLevelDelta,
                                targetJigsawPos.Z, deltaY, targetElement.Projection));
                            targetPiece.AddJunction(new JigsawJunction(sourceJigsawPos.X,
                                junctionY - targetJigsawLocalY + targetGroundLevelDelta,
                                sourceJigsawPos.Z, -deltaY, sourceElement.Projection));
                            _pieces.Add(targetPiece);

                            //At max depth it is not enqueued and this source jigsaw is skipped entirely, maps to vanilla continue block0
                            if (depth + 1 <= MaxDepth)
                                Placing.Add(new PieceState(targetPiece, childrenFree, depth + 1), placementPriority);
                            placed = true;
                            break;
                        }
                        if (placed) break;
                    }
                    if (placed) break;
                }
            }
        }

        //ComputeExpandTo computes how much child content must fit to be tall enough; only done when expansion is on and the target box is at most 16 blocks tall
        //Maps to vanilla taking the max of the child pool and child fallback pool max sizes for each target jigsaw
        private int ComputeExpandTo(bool doExpansionHack, BoundingBoxInt hackBox,
            List<StructureTemplate.JigsawBlockInfo> targetJigsaws, PoolAliasLookup poolAliasLookup)
        {
            if (!doExpansionHack || hackBox.LengthY > 16) return 0;
            var maxExpand = 0;
            foreach (var targetJigsaw in targetJigsaws)
            {
                var targetJigsawInfo = targetJigsaw.Info;
                if (!IsInside(hackBox, targetJigsawInfo.Pos.Relative(JigsawBlock.GetFrontFacing(targetJigsawInfo.State), 1)))
                    continue;
                var childPool = BuiltInRegistries.TEMPLATE_POOL.GetValue(poolAliasLookup.Lookup(targetJigsaw.Pool))
                    as StructureTemplatePool;
                var childPoolSize = childPool?.GetMaxSize(_structureTemplateManager) ?? 0;
                var childFallback = childPool?.Fallback;
                var childFallbackSize = childFallback is { } holder && holder.IsBound()
                    ? (holder.Value as StructureTemplatePool)?.GetMaxSize(_structureTemplateManager) ?? 0
                    : 0;
                maxExpand = Math.Max(maxExpand, Math.Max(childPoolSize, childFallbackSize));
            }
            return maxExpand;
        }

        //Encapsulate merges a point into the bounding box, maps to vanilla BoundingBox.encapsulate(BlockPos)
        private static BoundingBoxInt Encapsulate(BoundingBoxInt box, int x, int y, int z)
            => new(Math.Min(box.MinX, x), Math.Min(box.MinY, y), Math.Min(box.MinZ, z),
                Math.Max(box.MaxX, x), Math.Max(box.MaxY, y), Math.Max(box.MaxZ, z));
    }
}
