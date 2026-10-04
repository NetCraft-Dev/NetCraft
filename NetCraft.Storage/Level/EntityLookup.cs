using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraftEntity = NetCraft.Registry.Entity;

namespace NetCraft.Storage;

//EntityLookup 实体分区索引对应原版 net.minecraft.world.level.entity.EntityLookup
//按 SectionPos 分桶索引实体支持按 AABB 范围查询与按 chunk 查询
//替代 List<Entity> 的线性扫描提升大世界实体查询性能
public sealed class EntityLookup
{
    //按 SectionPos.AsLong 分桶每个桶持有该区段内实体列表
    private readonly Dictionary<long, List<NetCraftEntity>> _bySection = new();
    //实体到 section 反向索引便于 Remove 快速定位桶避免遍历
    private readonly Dictionary<NetCraftEntity, long> _entityToSection = new(ReferenceEqualityComparer.Instance);

    public int Count => _entityToSection.Count;

    //Add 添加实体到对应 section 桶
    //entity.Pos 变化时需先 Remove 再 Add 重新分桶
    public void Add(NetCraftEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (_entityToSection.ContainsKey(entity)) return;
        var sectionKey = SectionPosOf(entity.Pos);
        if (!_bySection.TryGetValue(sectionKey, out var list))
        {
            list = new List<NetCraftEntity>();
            _bySection[sectionKey] = list;
        }
        list.Add(entity);
        _entityToSection[entity] = sectionKey;
    }

    //Remove 移除实体返回是否成功
    public bool Remove(NetCraftEntity entity)
    {
        if (!_entityToSection.TryGetValue(entity, out var sectionKey)) return false;
        if (_bySection.TryGetValue(sectionKey, out var list))
        {
            list.Remove(entity);
            if (list.Count == 0) _bySection.Remove(sectionKey);
        }
        _entityToSection.Remove(entity);
        return true;
    }

    //GetInRange 返回 AABB 范围内所有实体对应原版 get(AABB)
    //min/max 为 AABB 两角顶点遍历覆盖的 section 桶收集实体
    public IEnumerable<NetCraftEntity> GetInRange(Vec3 min, Vec3 max)
    {
        var minX = SectionPos.BlockToSectionCoord(min.X);
        var minY = SectionPos.BlockToSectionCoord(min.Y);
        var minZ = SectionPos.BlockToSectionCoord(min.Z);
        var maxX = SectionPos.BlockToSectionCoord(max.X);
        var maxY = SectionPos.BlockToSectionCoord(max.Y);
        var maxZ = SectionPos.BlockToSectionCoord(max.Z);
        for (var x = minX; x <= maxX; x++)
            for (var y = minY; y <= maxY; y++)
                for (var z = minZ; z <= maxZ; z++)
                {
                    var key = SectionPos.AsLong(x, y, z);
                    if (!_bySection.TryGetValue(key, out var list)) continue;
                    foreach (var entity in list)
                        yield return entity;
                }
    }

    //GetInChunk 返回指定 chunk 内所有实体
    public IEnumerable<NetCraftEntity> GetInChunk(ChunkPos pos)
    {
        foreach (var (key, list) in _bySection)
        {
            var sx = SectionPos.GetX(key);
            var sz = SectionPos.GetZ(key);
            if (sx == pos.X && sz == pos.Z)
                foreach (var entity in list)
                    yield return entity;
        }
    }

    //GetAll 返回所有实体用于 Tick 遍历
    public IEnumerable<NetCraftEntity> GetAll() => _entityToSection.Keys;

    //Clear 清空所有实体
    public void Clear()
    {
        _bySection.Clear();
        _entityToSection.Clear();
    }

    //SectionPosOf 由 Vec3 计算所属 section 的 packed long
    private static long SectionPosOf(Vec3 pos)
        => SectionPos.AsLong(
            SectionPos.BlockToSectionCoord(pos.X),
            SectionPos.BlockToSectionCoord(pos.Y),
            SectionPos.BlockToSectionCoord(pos.Z));
}
