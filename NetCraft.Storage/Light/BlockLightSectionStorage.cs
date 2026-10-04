using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage.Chunk;

namespace NetCraft.Storage.Light;

//BlockLightSectionStorage 方块光区段存储对应原版 net.minecraft.world.level.lighting.BlockLightSectionStorage
//方块光没有任何"上方全亮"的语义 读不到层数据即 0
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

    //BlockDataLayerStorageMap 方块光层映射对应原版同名嵌套类
    //原版为 protected 嵌套类 C# 跨程序集构造需公开
    public sealed class BlockDataLayerStorageMap : DataLayerStorageMap<BlockDataLayerStorageMap>
    {
        public BlockDataLayerStorageMap() { }

        private BlockDataLayerStorageMap(Dictionary<long, DataLayer> map) : base(map) { }

        public override BlockDataLayerStorageMap Copy() => new(CopyEntries());
    }
}
