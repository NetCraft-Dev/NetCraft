using NetCraft.Logging;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Storage.Chunk;
using NetCraft.Util;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//JigsawPlacement 拼图装配 对应原版 net.minecraft.world.level.levelgen.structure.pools.JigsawPlacement
//从起始池的一个元素出发 按拼图方块逐层接出目标池的元素 直到深度耗尽
//高度采样原版走 RandomState 的 WORLD_SURFACE_WG 缓存 这里用独立随机源调生成器求表面高度
public static class JigsawPlacement
{
    //EmptyPoolId 内置空池名 对应原版 Pools.EMPTY 空池合法且不告警
    private static readonly Identifier EmptyPoolId = Identifier.WithDefaultNamespace("empty");

    //AddPieces 装配入口 返回生成点 无可用起始元素或越界时返回 null 对应原版 addPieces
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

        //锚点相对结构原点的偏移 把元素整体挪回去让锚点落在原点
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
        //原版 box 与片段的包围盒是同一个可变对象 平移后一并生效 这里改用平移后的盒
        return new GenerationStub(new BlockPos(centerX, centerY, centerZ),
            builder => PlaceChildren(context, structureTemplateManager, chunkGenerator, heightAccessor, random,
                centerPiece, maxDepth, doExpansionHack, centerX, centerY, centerZ, centerPiece.BoundingBox,
                maxDistanceFromCenter, dimensionPadding, poolAliasLookup, liquidSettings, builder));
    }

    //IsStartTooCloseToWorldHeightLimits 中心片段是否贴到维度上下界的留白里 对应原版同名方法
    private static bool IsStartTooCloseToWorldHeightLimits(LevelHeightAccessor heightAccessor,
        DimensionPadding dimensionPadding, BoundingBoxInt centerPieceBox)
    {
        if (dimensionPadding == DimensionPadding.Zero) return false;
        var minYWithPadding = heightAccessor.MinBuildHeight + dimensionPadding.Bottom;
        //原版 getMaxY 是最高可放方块 项目的 MaxBuildHeight 是开区间上界 这里减一
        var maxYWithPadding = heightAccessor.MaxBuildHeight - 1 - dimensionPadding.Top;
        return centerPieceBox.MinY < minYWithPadding || centerPieceBox.MaxY > maxYWithPadding;
    }

    //GetRandomNamedJigsaw 取洗牌后首个指定名的拼图方块坐标 对应原版 getRandomNamedJigsaw
    private static BlockPos? GetRandomNamedJigsaw(StructurePoolElement element, Identifier targetJigsawId,
        BlockPos position, Rotation rotation, StructureTemplateManager structureTemplateManager, RandomSource random)
    {
        foreach (var jigsaw in element.GetShuffledJigsawBlocks(structureTemplateManager, position, rotation, random))
        {
            if (jigsaw.Name == targetJigsawId) return jigsaw.Info.Pos;
        }
        return null;
    }

    //PlaceChildren 装配中心片段之外的其余片段 对应原版 addPieces 里延迟执行的那一段
    private static void PlaceChildren(GenerationContext context, StructureTemplateManager structureTemplateManager,
        ChunkGenerator chunkGenerator, LevelHeightAccessor heightAccessor, RandomSource random,
        PoolElementStructurePiece centerPiece, int maxDepth, bool doExpansionHack, int centerX, int centerY,
        int centerZ, BoundingBoxInt box, JigsawStructure.MaxDistance maxDistanceFromCenter,
        DimensionPadding dimensionPadding, PoolAliasLookup poolAliasLookup, LiquidSettings liquidSettings,
        StructurePiecesBuilder builder)
    {
        var pieces = new List<PoolElementStructurePiece> { centerPiece };
        //深度为零时原版直接返回且一个片段都不交出去
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

    //ResolvePool 起始池按别名改写后重新查注册表 对应原版 addPieces 开头的 flatMap
    private static StructureTemplatePool ResolvePool(Holder<NetCraft.Registry.StructureTemplatePool> startPool,
        PoolAliasLookup poolAliasLookup)
    {
        if (startPool.UnwrapKey() is { } key)
        {
            var aliased = BuiltInRegistries.TEMPLATE_POOL.GetValue(poolAliasLookup.Lookup(key.Identifier));
            if (aliased is StructureTemplatePool aliasedPool) return aliasedPool;
        }
        return startPool.Value as StructureTemplatePool
            ?? throw new InvalidOperationException("起始池不是 Game 层的模板池实现");
    }

    //GetFirstFreeHeight 取地表第一空高度 对应原版 ChunkGenerator.getFirstFreeHeight
    //项目生成器没有该方法 用 GetBaseHeight 加一作等价实现 原版传的随机状态换成本地随机源
    private static int GetFirstFreeHeight(ChunkGenerator generator, int x, int z, LevelHeightAccessor heightAccessor,
        RandomSource random)
        => generator.GetBaseHeight(x, z, (int)NetCraft.Registry.Heightmap.Types.WorldSurfaceWg, heightAccessor, random) + 1;

    //ToAabb 整数包围盒转浮点盒 对应原版 AABB.of 上界取闭区间右下角再加一
    private static AABB ToAabb(BoundingBoxInt box)
        => new(box.MinX, box.MinY, box.MinZ, box.MaxX + 1, box.MaxY + 1, box.MaxZ + 1);

    //IsInside 闭区间包含判定 对应原版 BoundingBox.isInside
    private static bool IsInside(BoundingBoxInt box, BlockPos pos)
        => pos.X >= box.MinX && pos.X <= box.MaxX
            && pos.Y >= box.MinY && pos.Y <= box.MaxY
            && pos.Z >= box.MinZ && pos.Z <= box.MaxZ;

    //Moved 包围盒沿 Y 平移 对应原版 BoundingBox.move
    private static BoundingBoxInt Moved(BoundingBoxInt box, int dy)
        => box with { MinY = box.MinY + dy, MaxY = box.MaxY + dy };

    //MutableShape 可变的形状引用 对应原版 MutableObject<VoxelShape>
    private sealed class MutableShape
    {
        public MutableShape(VoxelShape? value) => Value = value;

        public VoxelShape? Value { get; set; }
    }

    //PieceState 待展开的片段与它下游可用的自由形状 对应原版 record PieceState
    private sealed record PieceState(PoolElementStructurePiece Piece, MutableShape Free, int Depth);

    //Placer 装配器 按源片段的拼图方块逐层接出目标片段 对应原版 JigsawPlacement.Placer
    private sealed class Placer
    {
        private readonly ChunkGenerator _chunkGenerator;
        private readonly StructureTemplateManager _structureTemplateManager;
        private readonly List<PoolElementStructurePiece> _pieces;
        private readonly RandomSource _random;
        //高度采样专用随机源 原版用 RandomState 与洗牌随机源分开 这里单独派生避免扰动洗牌序列
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

        //MaxDepth 最大装配深度
        public int MaxDepth { get; }

        //Placing 待展开片段队列 按放置优先级出队 对应原版 SequencedPriorityIterator
        public SequencedPriorityIterator<PieceState> Placing { get; } = new();

        //TryPlacingChildren 从源片段的拼图方块接出目标片段 对应原版 tryPlacingChildren
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
                //引用还没绑上时按原版「空且不是内置空池」处理 保守跳过而不是让它抛
                var fallbackPool = fallbackHolder.IsBound() ? fallbackHolder.Value as StructureTemplatePool : null;
                var fallbackName = fallbackHolder.UnwrapKey()?.Identifier;
                if (fallbackPool is null || (fallbackPool.Size() == 0 && fallbackName != EmptyPoolId))
                {
                    Log.Warning($"Empty or missing fallback pool {fallbackName?.ToString() ?? "<unbound>"}");
                    continue;
                }

                //目标拼图落在源包围盒里就用源片段自己的自由形状 否则用父级传下来的
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
                    //抽到空元素就不再往下看这个源拼图
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
                                //非刚性连接要贴地表 源拼图列的表面高度按源拼图坐标缓存
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

                            //收缩四分之一格后与已有自由形状相交就说明挤在一起 换下一个候选
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

                            //到顶深度就不再入队 该源拼图整块跳过 对应原版 continue block0
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

        //ComputeExpandTo 计算要塞进多少子内容才够高 只在开扩展且目标盒不高于 16 格时才做
        //对应原版对每个目标拼图取子池与子兜底池最大尺寸的最大值
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

        //Encapsulate 把点并入包围盒 对应原版 BoundingBox.encapsulate(BlockPos)
        private static BoundingBoxInt Encapsulate(BoundingBoxInt box, int x, int y, int z)
            => new(Math.Min(box.MinX, x), Math.Min(box.MinY, y), Math.Min(box.MinZ, z),
                Math.Max(box.MaxX, x), Math.Max(box.MaxY, y), Math.Max(box.MaxZ, z));
    }
}
