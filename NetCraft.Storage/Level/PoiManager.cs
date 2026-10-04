using NetCraft.Primitives;

namespace NetCraft.Storage;

//PoiManager 兴趣点管理器抽象类对应原版 net.minecraft.world.entity.ai.village.poi.PoiManager
//持有 chunk 内兴趣点注册表用于村庄/铁傀儡等机制
//完整实现依赖 PoiSection/PoiType 子系统
public abstract class PoiManager
{
    //GetChunk 获取指定 chunk 的兴趣点数据占位
    public abstract object? GetChunk(ChunkPos pos);
}
