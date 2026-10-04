using NetCraft.Primitives;
using NetCraft.Registry.State;

namespace NetCraft.Storage.Light;

//LightChunk 光照用区块视图对应原版 net.minecraft.world.level.chunk.LightChunk
//继承 BlockGetter 只暴露光照引擎需要的读方块与光源枚举能力
public interface LightChunk : BlockGetter
{
    //getSkyLightSources 取该区块的天光光源列高度图
    ChunkSkyLightSources GetSkyLightSources();

    //findBlockLightSources 枚举区块内所有发光方块
    void FindBlockLightSources(Action<BlockPos, BlockState> consumer);
}
