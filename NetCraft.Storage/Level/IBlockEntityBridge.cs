using NetCraft.Nbt;
using NetCraft.Primitives;

namespace NetCraft.Storage;

//IBlockEntityBridge 方块实体与区块存储之间的桥 由 Game 层实现
//方块实体的具体类型只有 Game 层认识 Storage 层只搬运 NBT
//落盘采集/读档还原/区块卸载清理三件事都经这条 对应原版 LevelChunk 自己持有 blockEntities 的职责
public interface IBlockEntityBridge
{
    //Collect 取该区块内全部方块实体的完整 NBT(含 id 与坐标三元组)供落盘与区块包使用
    List<CompoundTag> Collect(ChunkPos pos);

    //Restore 把读档得到的方块实体 NBT 还原进关卡 未知 id 由实现跳过
    void Restore(ChunkPos pos, List<CompoundTag> tags);

    //Unload 区块卸载时清掉该区块的方块实体 对应原版区块卸载时的方块实体清理
    void Unload(ChunkPos pos);
}
