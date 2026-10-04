using NetCraft.Game.World.Level.Block;
using NetCraft.Logging;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Storage;

namespace NetCraft.Game.Server;

//ServerBlockEntityBridge 方块实体桥的 Game 层实现
//把方块实体集合与方块实体类型注册表接给 Storage 层的区块存储
public sealed class ServerBlockEntityBridge(ServerLevel level, BlockEntityManager blockEntities)
    : IBlockEntityBridge
{
    //Collect 采集该区块的方块实体完整 NBT
    //SaveWithFullMetadata 带 id 与坐标 读档时靠它反查类型并落回原位
    public List<CompoundTag> Collect(ChunkPos pos)
    {
        var tags = new List<CompoundTag>();
        foreach (var entity in blockEntities.InChunk(pos))
            tags.Add(entity.SaveWithFullMetadata());
        return tags;
    }

    //Restore 按 id 反查类型还原方块实体 未知 id 跳过并记警告
    //原版对未知方块实体同样是丢弃 报错会让整张存档读不进来
    public void Restore(ChunkPos pos, List<CompoundTag> tags)
    {
        foreach (var tag in tags)
        {
            var entity = BlockEntityTypes.Load(tag);
            if (entity is null)
            {
                Log.Warning($"Block entity type in chunk {pos} is not registered, skipped id={tag.GetStringValue("id")}");
                continue;
            }
            blockEntities.Add(level, entity);
        }
    }

    //Unload 区块卸载时清掉该区块的方块实体
    public void Unload(ChunkPos pos)
    {
        var removed = blockEntities.RemoveInChunk(pos);
        if (removed > 0) Log.Debug($"Chunk unload cleaned up block entities at {pos}, {removed} in total");
    }
}
