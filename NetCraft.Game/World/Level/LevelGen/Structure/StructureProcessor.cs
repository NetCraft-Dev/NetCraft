using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry.State;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructureBlockInfo one block in a template, maps to vanilla StructureTemplate.StructureBlockInfo
//A non-null Nbt means the cell also carries block entity data; the block entity is created during placement
//A value type on purpose: a single piece rebuilds one of these per block on every placement, and the structural pass
//allocates tens of thousands of them, so keeping them inline in the lists removes the wrapper object per element
public readonly record struct StructureBlockInfo(BlockPos Pos, BlockState State, NetCraft.Nbt.CompoundTag? Nbt);

//StructureEntityInfo one entity in a template, maps to vanilla StructureTemplate.StructureEntityInfo
//Pos is the exact entity position, BlockPos is the block it sits in; the entry is dropped when Nbt is missing
public sealed record StructureEntityInfo(Vec3i Pos, BlockPos BlockPos, NetCraft.Nbt.CompoundTag Nbt);

//StructureTemplatePalette the block list of one palette, maps to vanilla StructureTemplate.Palette
//A multi-palette template picks one per placement from a coordinate-derived random
public sealed class StructureTemplatePalette
{
    public IReadOnlyList<StructureBlockInfo> Blocks { get; }

    public StructureTemplatePalette(IReadOnlyList<StructureBlockInfo> blocks) => Blocks = blocks;
}

//StructureProcessor structure processor, maps to the identically named vanilla interface
//Runs the placed blocks through in order; returning null drops the cell
//Inherits the registry-side marker interface so processor types can register into STRUCTURE_PROCESSOR
public interface StructureProcessor : NetCraft.Registry.StructureProcessor
{
    //ProcessBlock processes a single block; targetPosition is the final write position and templateRelativePos is the local position inside the template
    StructureBlockInfo? ProcessBlock(WorldGenRegion? level, BlockPos targetPosition, BlockPos referencePos,
        BlockPos templateRelativePos, StructureBlockInfo processedBlockInfo, StructurePlaceSettings settings)
        => processedBlockInfo;

    //FinalizeProcessing does the cleanup after all blocks are processed
    IReadOnlyList<StructureBlockInfo> FinalizeProcessing(WorldGenRegion? level, BlockPos position, BlockPos referencePos,
        IReadOnlyList<StructureBlockInfo> originalBlockInfoList,
        IReadOnlyList<StructureBlockInfo> processedBlockInfoList, StructurePlaceSettings settings)
        => processedBlockInfoList;

    //EvaluatesEntirePieceState whether the whole piece state is needed; when true no clipping to the current chunk happens
    bool EvaluatesEntirePieceState() => false;

    //ModifiesBlockEntityData whether ProcessBlock may write into the nbt it is handed instead of returning a fresh block info
    //ProcessBlockInfos reads it to decide whether a block needs its own copy of the template nbt before the chain runs, so
    //a processor that never writes into the tag leaves the default and lets placement share the template's own tag
    bool ModifiesBlockEntityData => false;

    //ElementCodec this processor's JSON codec for processor_list loading; returns null when parsing is not implemented
    MapCodec<StructureProcessor>? ElementCodec => null;
}
