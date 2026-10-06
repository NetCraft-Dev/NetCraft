using NetCraft.Primitives;
using NetCraft.Registry.State;

namespace NetCraft.Storage.Light;

//SkyLightEngine, sky light engine, maps to vanilla net.minecraft.world.level.lighting.SkyLightEngine
//Besides ordinary propagation it maintains each column's sky light source height, distinguishing "fully lit column" from "attenuating downward after occlusion"
public sealed class SkyLightEngine
    : LightEngine<SkyLightSectionStorage.SkyDataLayerStorageMap, SkyLightSectionStorage>
{
    private static readonly long RemoveTopSkySourceEntry = QueueEntry.DecreaseAllDirections(MaxLevel);
    private static readonly long RemoveSkySourceEntry = QueueEntry.DecreaseSkipOneDirection(MaxLevel, Direction.Up);
    private static readonly long AddSkySourceEntry = QueueEntry.IncreaseSkipOneDirection(MaxLevel, false, Direction.Up);

    //Used as a fallback when the chunk is unloaded, meaning the column has no sky light source
    private readonly ChunkSkyLightSources _emptyChunkSources;

    public SkyLightEngine(LightChunkGetter chunkSource)
        : this(chunkSource, new SkyLightSectionStorage(chunkSource))
    {
    }

    public SkyLightEngine(LightChunkGetter chunkSource, SkyLightSectionStorage storage)
        : base(chunkSource, storage)
    {
        _emptyChunkSources = new ChunkSkyLightSources(chunkSource.GetLevel());
    }

    private static bool IsSourceLevel(int value) => value == MaxLevel;

    private int GetLowestSourceY(int x, int z, int defaultValue)
    {
        var sources = GetChunkSources(SectionPos.BlockToSectionCoord(x), SectionPos.BlockToSectionCoord(z));
        if (sources is null) return defaultValue;
        return sources.GetLowestSourceY(SectionPos.SectionRelative(x), SectionPos.SectionRelative(z));
    }

    private ChunkSkyLightSources? GetChunkSources(int chunkX, int chunkZ)
        => ChunkSource.GetChunkForLighting(chunkX, chunkZ)?.GetSkyLightSources();

    protected override void CheckNode(long blockNode)
    {
        var x = BlockPos.GetX(blockNode);
        var y = BlockPos.GetY(blockNode);
        var z = BlockPos.GetZ(blockNode);
        var sectionNode = SectionPos.BlockToSection(blockNode);
        var lowestSourceY = Storage.LightOnInSection(sectionNode)
            ? GetLowestSourceY(x, z, int.MaxValue)
            : int.MaxValue;

        if (lowestSourceY != int.MaxValue) UpdateSourcesInColumn(x, z, lowestSourceY);
        if (!Storage.StoringLightForSection(sectionNode)) return;

        if (y >= lowestSourceY)
        {
            EnqueueDecrease(blockNode, RemoveSkySourceEntry);
            EnqueueIncrease(blockNode, AddSkySourceEntry);
            return;
        }

        var oldLevel = Storage.GetStoredLevel(blockNode);
        if (oldLevel > 0)
        {
            Storage.SetStoredLevel(blockNode, 0);
            EnqueueDecrease(blockNode, QueueEntry.DecreaseAllDirections(oldLevel));
        }
        else
        {
            EnqueueDecrease(blockNode, PullLightInEntry);
        }
    }

    private void UpdateSourcesInColumn(int x, int z, int lowestSourceY)
    {
        var worldBottomY = SectionPos.SectionToBlockCoord(Storage.GetBottomSectionY());
        RemoveSourcesBelow(x, z, lowestSourceY, worldBottomY);
        AddSourcesAbove(x, z, lowestSourceY, worldBottomY);
    }

    //removeSourcesBelow retracts sky light stored at full value below the lowest source
    private void RemoveSourcesBelow(int x, int z, int lowestSourceY, int worldBottomY)
    {
        if (lowestSourceY <= worldBottomY) return;

        var sectionX = SectionPos.BlockToSectionCoord(x);
        var sectionZ = SectionPos.BlockToSectionCoord(z);
        var startY = lowestSourceY - 1;
        for (var sectionY = SectionPos.BlockToSectionCoord(startY); Storage.HasLightDataAtOrBelow(sectionY); sectionY--)
        {
            if (!Storage.StoringLightForSection(SectionPos.AsLong(sectionX, sectionY, sectionZ))) continue;

            var sectionBottomY = SectionPos.SectionToBlockCoord(sectionY);
            var sectionTopY = sectionBottomY + 15;
            var y = Math.Min(sectionTopY, startY);
            while (y >= sectionBottomY)
            {
                var blockNode = BlockPos.AsLong(x, y, z);
                if (!IsSourceLevel(Storage.GetStoredLevel(blockNode))) return;
                Storage.SetStoredLevel(blockNode, 0);
                EnqueueDecrease(blockNode, y == lowestSourceY - 1 ? RemoveTopSkySourceEntry : RemoveSkySourceEntry);
                y--;
            }
        }
    }

    //addSourcesAbove fills the column above the lowest source to full sky light
    private void AddSourcesAbove(int x, int z, int lowestSourceY, int worldBottomY)
    {
        var sectionX = SectionPos.BlockToSectionCoord(x);
        var sectionZ = SectionPos.BlockToSectionCoord(z);
        var neighborLowestSourceY = Math.Max(
            Math.Max(GetLowestSourceY(x - 1, z, ChunkSkyLightSources.NegativeInfinity),
                GetLowestSourceY(x + 1, z, ChunkSkyLightSources.NegativeInfinity)),
            Math.Max(GetLowestSourceY(x, z - 1, ChunkSkyLightSources.NegativeInfinity),
                GetLowestSourceY(x, z + 1, ChunkSkyLightSources.NegativeInfinity)));

        var startY = Math.Max(lowestSourceY, worldBottomY);
        var sectionNode = SectionPos.AsLong(sectionX, SectionPos.BlockToSectionCoord(startY), sectionZ);
        while (true)
        {
            if (Storage.IsAboveData(sectionNode)) return;

            if (Storage.StoringLightForSection(sectionNode))
            {
                var sectionBottomY = SectionPos.SectionToBlockCoord(SectionPos.GetY(sectionNode));
                var sectionTopY = sectionBottomY + 15;
                for (var y = Math.Max(sectionBottomY, startY); y <= sectionTopY; y++)
                {
                    var blockNode = BlockPos.AsLong(x, y, z);
                    if (IsSourceLevel(Storage.GetStoredLevel(blockNode))) return;
                    Storage.SetStoredLevel(blockNode, MaxLevel);
                    if (y < neighborLowestSourceY || y == lowestSourceY)
                        EnqueueIncrease(blockNode, AddSkySourceEntry);
                }
            }

            sectionNode = SectionPos.Offset(sectionNode, Direction.Up);
        }
    }

    protected override void PropagateIncrease(long fromNode, long increaseData, int fromLevel)
    {
        BlockState? fromState = null;
        var emptySectionsBelow = CountEmptySectionsBelowIfAtBorder(fromNode);
        foreach (var propagationDirection in PropagationDirections)
        {
            if (!QueueEntry.ShouldPropagateInDirection(increaseData, propagationDirection)) continue;

            var toNode = BlockPos.Offset(fromNode, propagationDirection);
            if (!Storage.StoringLightForSection(SectionPos.BlockToSection(toNode))) continue;

            var toLevel = Storage.GetStoredLevel(toNode);
            var maxPossibleNewToLevel = fromLevel - 1;
            if (maxPossibleNewToLevel <= toLevel) continue;

            var toState = GetState(BlockPos.GetX(toNode), BlockPos.GetY(toNode), BlockPos.GetZ(toNode));
            var newToLevel = fromLevel - GetOpacity(toState);
            if (newToLevel <= toLevel) continue;

            if (fromState is null)
                fromState = QueueEntry.IsFromEmptyShape(increaseData)
                    ? default(BlockState)
                    : GetState(BlockPos.GetX(fromNode), BlockPos.GetY(fromNode), BlockPos.GetZ(fromNode));

            if (ShapeOccludes(fromState, toState, propagationDirection)) continue;

            Storage.SetStoredLevel(toNode, newToLevel);
            if (newToLevel > 1)
                EnqueueIncrease(toNode, QueueEntry.IncreaseSkipOneDirection(newToLevel, IsEmptyShape(toState),
                    propagationDirection.Opposite));
            PropagateFromEmptySections(toNode, propagationDirection, newToLevel, true, emptySectionsBelow);
        }
    }

    protected override void PropagateDecrease(long fromNode, long decreaseData)
    {
        var emptySectionsBelow = CountEmptySectionsBelowIfAtBorder(fromNode);
        var oldFromLevel = QueueEntry.GetFromLevel(decreaseData);
        foreach (var propagationDirection in PropagationDirections)
        {
            if (!QueueEntry.ShouldPropagateInDirection(decreaseData, propagationDirection)) continue;

            var toNode = BlockPos.Offset(fromNode, propagationDirection);
            if (!Storage.StoringLightForSection(SectionPos.BlockToSection(toNode))) continue;

            var toLevel = Storage.GetStoredLevel(toNode);
            if (toLevel == 0) continue;

            if (toLevel <= oldFromLevel - 1)
            {
                Storage.SetStoredLevel(toNode, 0);
                EnqueueDecrease(toNode, QueueEntry.DecreaseSkipOneDirection(toLevel, propagationDirection.Opposite));
                PropagateFromEmptySections(toNode, propagationDirection, toLevel, false, emptySectionsBelow);
            }
            else
            {
                EnqueueIncrease(toNode, QueueEntry.IncreaseOnlyOneDirection(toLevel, false, propagationDirection.Opposite));
            }
        }
    }

    //countEmptySectionsBelowIfAtBorder counts contiguous empty sections below when on the section's bottom face and at a horizontal border
    private int CountEmptySectionsBelowIfAtBorder(long blockNode)
    {
        var y = BlockPos.GetY(blockNode);
        if (SectionPos.SectionRelative(y) != 0) return 0;

        var x = BlockPos.GetX(blockNode);
        var z = BlockPos.GetZ(blockNode);
        var localX = SectionPos.SectionRelative(x);
        var localZ = SectionPos.SectionRelative(z);
        if (localX != 0 && localX != 15 && localZ != 0 && localZ != 15) return 0;

        var sectionX = SectionPos.BlockToSectionCoord(x);
        var sectionY = SectionPos.BlockToSectionCoord(y);
        var sectionZ = SectionPos.BlockToSectionCoord(z);
        var emptySectionsBelow = 0;
        while (!Storage.StoringLightForSection(SectionPos.AsLong(sectionX, sectionY - emptySectionsBelow - 1, sectionZ))
               && Storage.HasLightDataAtOrBelow(sectionY - emptySectionsBelow - 1))
        {
            emptySectionsBelow++;
        }
        return emptySectionsBelow;
    }

    //propagateFromEmptySections: when light passes vertically through empty sections, the whole empty stretch must be filled at once
    private void PropagateFromEmptySections(long toNode, Direction propagationDirection, int toLevel, bool increase,
        int emptySectionsBelow)
    {
        if (emptySectionsBelow == 0) return;

        var x = BlockPos.GetX(toNode);
        var z = BlockPos.GetZ(toNode);
        if (!CrossedSectionEdge(propagationDirection, SectionPos.SectionRelative(x), SectionPos.SectionRelative(z))) return;

        var y = BlockPos.GetY(toNode);
        var sectionX = SectionPos.BlockToSectionCoord(x);
        var sectionZ = SectionPos.BlockToSectionCoord(z);
        var sectionY = SectionPos.BlockToSectionCoord(y) - 1;
        var bottomSectionY = sectionY - emptySectionsBelow + 1;
        while (sectionY >= bottomSectionY)
        {
            if (Storage.StoringLightForSection(SectionPos.AsLong(sectionX, sectionY, sectionZ)))
            {
                var sectionMinY = SectionPos.SectionToBlockCoord(sectionY);
                for (var localY = 15; localY >= 0; localY--)
                {
                    var blockNode = BlockPos.AsLong(x, sectionMinY + localY, z);
                    if (increase)
                    {
                        Storage.SetStoredLevel(blockNode, toLevel);
                        if (toLevel > 1)
                            EnqueueIncrease(blockNode, QueueEntry.IncreaseSkipOneDirection(toLevel, true,
                                propagationDirection.Opposite));
                    }
                    else
                    {
                        Storage.SetStoredLevel(blockNode, 0);
                        EnqueueDecrease(blockNode, QueueEntry.DecreaseSkipOneDirection(toLevel, propagationDirection.Opposite));
                    }
                }
            }
            sectionY--;
        }
    }

    private static bool CrossedSectionEdge(Direction propagationDirection, int x, int z)
    {
        if (propagationDirection == Direction.North) return z == 15;
        if (propagationDirection == Direction.South) return z == 0;
        if (propagationDirection == Direction.West) return x == 15;
        if (propagationDirection == Direction.East) return x == 0;
        return false;
    }

    public override void SetLightEnabled(ChunkPos pos, bool enable)
    {
        base.SetLightEnabled(pos, enable);
        if (!enable) return;

        var sources = GetChunkSources(pos.X, pos.Z) ?? _emptyChunkSources;
        var highestNonSourceY = sources.GetHighestLowestSourceY() - 1;
        var lowestFullySourceSectionY = SectionPos.BlockToSectionCoord(highestNonSourceY) + 1;
        var zeroNode = SectionPos.GetZeroNode(pos.X, pos.Z);
        var topSectionY = Storage.GetTopSectionY(zeroNode);
        var bottomSectionY = Math.Max(Storage.GetBottomSectionY(), lowestFullySourceSectionY);

        //Sections with a fully unoccluded column are filled directly, skipping per-cell propagation
        for (var sectionY = topSectionY - 1; sectionY >= bottomSectionY; sectionY--)
        {
            var dataLayer = Storage.GetDataLayerToWrite(SectionPos.AsLong(pos.X, sectionY, pos.Z));
            if (dataLayer is not null && dataLayer.IsEmpty) dataLayer.Fill(MaxLevel);
        }
    }

    public override void PropagateLightSources(ChunkPos pos)
    {
        var zeroNode = SectionPos.GetZeroNode(pos.X, pos.Z);
        Storage.SetLightEnabled(zeroNode, true);

        var sources = GetChunkSources(pos.X, pos.Z) ?? _emptyChunkSources;
        var northSources = GetChunkSources(pos.X, pos.Z - 1) ?? _emptyChunkSources;
        var southSources = GetChunkSources(pos.X, pos.Z + 1) ?? _emptyChunkSources;
        var westSources = GetChunkSources(pos.X - 1, pos.Z) ?? _emptyChunkSources;
        var eastSources = GetChunkSources(pos.X + 1, pos.Z) ?? _emptyChunkSources;

        var topSectionY = Storage.GetTopSectionY(zeroNode);
        var bottomSectionY = Storage.GetBottomSectionY();
        var sectionMinX = SectionPos.SectionToBlockCoord(pos.X);
        var sectionMinZ = SectionPos.SectionToBlockCoord(pos.Z);

        for (var sectionY = topSectionY - 1; sectionY >= bottomSectionY; sectionY--)
        {
            var sectionNode = SectionPos.AsLong(pos.X, sectionY, pos.Z);
            var dataLayer = Storage.GetDataLayerToWrite(sectionNode);
            if (dataLayer is null) continue;

            var sectionMinY = SectionPos.SectionToBlockCoord(sectionY);
            var sectionMaxY = sectionMinY + 15;
            var sourcesBelow = false;
            for (var z = 0; z < 16; z++)
            for (var x = 0; x < 16; x++)
            {
                var lowestSourceY = sources.GetLowestSourceY(x, z);
                if (lowestSourceY > sectionMaxY) continue;

                var northLowestSourceY = z == 0 ? northSources.GetLowestSourceY(x, 15) : sources.GetLowestSourceY(x, z - 1);
                var southLowestSourceY = z == 15 ? southSources.GetLowestSourceY(x, 0) : sources.GetLowestSourceY(x, z + 1);
                var westLowestSourceY = x == 0 ? westSources.GetLowestSourceY(15, z) : sources.GetLowestSourceY(x - 1, z);
                var eastLowestSourceY = x == 15 ? eastSources.GetLowestSourceY(0, z) : sources.GetLowestSourceY(x + 1, z);
                var neighborLowestSourceY = Math.Max(Math.Max(northLowestSourceY, southLowestSourceY),
                    Math.Max(westLowestSourceY, eastLowestSourceY));

                var y = sectionMaxY;
                while (y >= Math.Max(sectionMinY, lowestSourceY))
                {
                    dataLayer.Set(x, SectionPos.SectionRelative(y), z, MaxLevel);
                    if (y == lowestSourceY || y < neighborLowestSourceY)
                    {
                        var blockNode = BlockPos.AsLong(sectionMinX + x, y, sectionMinZ + z);
                        EnqueueIncrease(blockNode, QueueEntry.IncreaseSkySourceInDirections(
                            y == lowestSourceY,
                            y < northLowestSourceY,
                            y < southLowestSourceY,
                            y < westLowestSourceY,
                            y < eastLowestSourceY));
                    }
                    y--;
                }

                if (lowestSourceY < sectionMinY) sourcesBelow = true;
            }

            if (!sourcesBelow) return;
        }
    }
}
