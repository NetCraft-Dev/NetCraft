using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Storage.Light;

//LightChunkGetter 光照取区块接口对应原版 net.minecraft.world.level.chunk.LightChunkGetter
//光照引擎通过它按区块坐标取到 LightChunk 视图
public interface LightChunkGetter
{
    //getChunkForLighting 取光照用区块视图未加载返回 null
    LightChunk? GetChunkForLighting(int chunkX, int chunkZ);

    //getLevel 取世界读取入口 提供高度范围与按坐标读方块
    BlockGetter GetLevel();

    //onLightUpdate 光照更新完成回调 原版为接口默认方法
    void OnLightUpdate(LightLayer layer, SectionPos pos) { }
}
