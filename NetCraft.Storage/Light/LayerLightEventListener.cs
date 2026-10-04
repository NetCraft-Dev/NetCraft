using NetCraft.Primitives;
using NetCraft.Storage.Chunk;

namespace NetCraft.Storage.Light;

//LayerLightEventListener 单层光照监听对应原版 net.minecraft.world.level.lighting.LayerLightEventListener
//在 LightEventListener 之上增加按层读取光照数据的能力
public interface LayerLightEventListener : LightEventListener
{
    //getDataLayerData 取区段的层数据不存在返回 null
    DataLayer? GetDataLayerData(SectionPos pos);

    //getLightValue 取方块坐标上的光照等级
    int GetLightValue(BlockPos pos);
}

//DummyLightLayerEventListener 空实现对应原版 LayerLightEventListener.DummyLightLayerEventListener
//供光照未启用时占位避免调用方到处判空
public sealed class DummyLightLayerEventListener : LayerLightEventListener
{
    public static readonly DummyLightLayerEventListener Instance = new();

    private DummyLightLayerEventListener() { }

    public DataLayer? GetDataLayerData(SectionPos pos) => null;

    public int GetLightValue(BlockPos pos) => 0;

    public void CheckBlock(BlockPos pos) { }

    public bool HasLightWork() => false;

    public int RunLightUpdates() => 0;

    public void UpdateSectionStatus(SectionPos pos, bool sectionEmpty) { }

    public void SetLightEnabled(ChunkPos pos, bool enable) { }

    public void PropagateLightSources(ChunkPos pos) { }
}
