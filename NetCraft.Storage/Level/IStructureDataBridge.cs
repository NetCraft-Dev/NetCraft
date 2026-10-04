using NetCraft.Nbt;
using NetCraft.Primitives;

namespace NetCraft.Storage;

//IStructureDataBridge 结构数据与区块存储之间的桥 由 Game 层实现
//结构片段的具体类型只有 Game 层认识 Storage 层只搬运 structures 段的 NBT
//对应原版 LevelChunk 自己持有 structureStarts/structureReferences 的职责
public interface IStructureDataBridge
{
    //Pack 取该区块的装配结果与跨区块引用打包成 structures 段
    //直接写盘的是这份 NBT 结构内容不含任何游戏对象
    CompoundTag Pack(ChunkPos pos);

    //Restore 把读档得到的 structures 段还原进结构表 未知结构名由实现跳过
    void Restore(ChunkPos pos, CompoundTag tag);
}
