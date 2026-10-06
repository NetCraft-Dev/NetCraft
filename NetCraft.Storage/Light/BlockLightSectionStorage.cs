using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage.Chunk;

namespace NetCraft.Storage.Light;

//BlockLightSectionStorage, block light section storage, maps to vanilla net.minecraft.world.level.lighting.BlockLightSectionStorage
//Block light has no "fully lit from above" semantics; a missing layer reads as 0
public class BlockLightSectionStorage : LayerLightSectionStorage<BlockLightSectionStorage.BlockDataLayerStorageMap>
{
    public BlockLightSectionStorage(LightChunkGetter chunkSource)
        : base(LightLayer.Block, chunkSource, new BlockDataLayerStorageMap())
    {
    }

    protected internal override int GetLightValue(long blockNode)
    {
        var sectionNode = SectionPos.BlockToSection(blockNode);
        var layer = GetDataLayer(sectionNode, false);
        if (layer is null) return 0;
        return layer.Get(
            SectionPos.SectionRelative(BlockPos.GetX(blockNode)),
            SectionPos.SectionRelative(BlockPos.GetY(blockNode)),
            SectionPos.SectionRelative(BlockPos.GetZ(blockNode)));
    }

    //BlockDataLayerStorageMap, block light layer map, maps to the vanilla nested class of the same name
    //Vanilla is a protected nested class; C# needs it public to construct across assemblies
    public sealed class BlockDataLayerStorageMap : DataLayerStorageMap<BlockDataLayerStorageMap>
    {
        public BlockDataLayerStorageMap() { }

        private BlockDataLayerStorageMap(Dictionary<long, DataLayer> map) : base(map) { }

        public override BlockDataLayerStorageMap Copy() => new(CopyEntries());
    }
}
