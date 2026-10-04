using NetCraft.Primitives;

namespace NetCraft.Storage.Light;

//LightEventListener 光照事件监听入口对应原版 net.minecraft.world.level.lighting.LightEventListener
//光照引擎与光照存储层都实现此接口供区块系统通知光照变化
public interface LightEventListener
{
    //checkBlock 方块变化后登记待重算
    void CheckBlock(BlockPos pos);

    //hasLightWork 是否还有待处理的光照更新
    bool HasLightWork();

    //runLightUpdates 执行一轮光照更新返回处理的节点数
    int RunLightUpdates();

    //updateSectionStatus 区段是否为空变化时通知
    void UpdateSectionStatus(SectionPos pos, bool sectionEmpty);

    //setLightEnabled 区块光照开关
    void SetLightEnabled(ChunkPos pos, bool enable);

    //propagateLightSources 传播区块内所有光源
    void PropagateLightSources(ChunkPos pos);

    //updateSectionStatus 方块坐标重载 原版为接口默认方法
    void UpdateSectionStatus(BlockPos pos, bool sectionEmpty)
        => UpdateSectionStatus(SectionPos.Of(pos), sectionEmpty);
}
