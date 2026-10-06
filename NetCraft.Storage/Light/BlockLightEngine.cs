using NetCraft.Primitives;
using NetCraft.Registry.State;

namespace NetCraft.Storage.Light;

//BlockLightEngine, block light engine, maps to vanilla net.minecraft.world.level.lighting.BlockLightEngine
//Block light comes only from emitting blocks; it has no "whole column lit from above" semantics like sky light
public sealed class BlockLightEngine
    : LightEngine<BlockLightSectionStorage.BlockDataLayerStorageMap, BlockLightSectionStorage>
{
    public BlockLightEngine(LightChunkGetter chunkSource)
        : this(chunkSource, new BlockLightSectionStorage(chunkSource))
    {
    }

    public BlockLightEngine(LightChunkGetter chunkSource, BlockLightSectionStorage storage)
        : base(chunkSource, storage)
    {
    }

    protected override void CheckNode(long blockNode)
    {
        var sectionNode = SectionPos.BlockToSection(blockNode);
        if (!Storage.StoringLightForSection(sectionNode)) return;

        var state = GetState(BlockPos.GetX(blockNode), BlockPos.GetY(blockNode), BlockPos.GetZ(blockNode));
        var lightEmission = GetEmission(blockNode, state);
        var oldLevel = Storage.GetStoredLevel(blockNode);
        if (lightEmission < oldLevel)
        {
            Storage.SetStoredLevel(blockNode, 0);
            EnqueueDecrease(blockNode, QueueEntry.DecreaseAllDirections(oldLevel));
        }
        else
        {
            EnqueueDecrease(blockNode, PullLightInEntry);
        }

        if (lightEmission > 0)
            EnqueueIncrease(blockNode, QueueEntry.IncreaseLightFromEmission(lightEmission, IsEmptyShape(state)));
    }

    protected override void PropagateIncrease(long fromNode, long increaseData, int fromLevel)
    {
        BlockState? fromState = null;
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
        }
    }

    protected override void PropagateDecrease(long fromNode, long decreaseData)
    {
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
                var toState = GetState(BlockPos.GetX(toNode), BlockPos.GetY(toNode), BlockPos.GetZ(toNode));
                var toEmission = GetEmission(toNode, toState);
                Storage.SetStoredLevel(toNode, 0);
                if (toEmission < toLevel)
                    EnqueueDecrease(toNode, QueueEntry.DecreaseSkipOneDirection(toLevel, propagationDirection.Opposite));
                if (toEmission > 0)
                    EnqueueIncrease(toNode, QueueEntry.IncreaseLightFromEmission(toEmission, IsEmptyShape(toState)));
            }
            else
            {
                EnqueueIncrease(toNode, QueueEntry.IncreaseOnlyOneDirection(toLevel, false, propagationDirection.Opposite));
            }
        }
    }

    //getEmission, the block's light emission; treated as 0 when the matching layer has lighting disabled
    private int GetEmission(long blockNode, BlockState? state)
    {
        var emission = state?.GetLightEmission() ?? 0;
        if (emission > 0 && Storage.LightOnInSection(SectionPos.BlockToSection(blockNode))) return emission;
        return 0;
    }

    public override void PropagateLightSources(ChunkPos pos)
    {
        SetLightEnabled(pos, true);
        var chunk = ChunkSource.GetChunkForLighting(pos.X, pos.Z);
        chunk?.FindBlockLightSources((lightPos, state) => EnqueueIncrease(
            lightPos.AsLong(),
            QueueEntry.IncreaseLightFromEmission(state.GetLightEmission(), IsEmptyShape(state))));
    }
}
