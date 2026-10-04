using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Storage;

//SimplePoiManager 兴趣点管理器具体实现对应原版 PoiManager 简化版
//持有区块内 PoiSection 字典按 ChunkPos.Pack 索引
//提供 Add/Get/Remove/Has 基础 API 供村庄/铁傀儡等机制使用
public sealed class SimplePoiManager : PoiManager
{
    //Empty 空 PoiManager 单例 PersistentServerLevel.LoadChunkAsync 反序列化用
    public static readonly SimplePoiManager Empty = new();

    private readonly Dictionary<long, PoiSection> _sections = new();

    //GetSection 获取指定 chunk 的 PoiSection 不存在返回 null
    public PoiSection? GetSection(ChunkPos pos)
        => _sections.TryGetValue(ChunkPos.Pack(pos.X, pos.Z), out var section) ? section : null;

    //GetOrCreateSection 获取或创建 PoiSection
    public PoiSection GetOrCreateSection(ChunkPos pos)
    {
        var key = ChunkPos.Pack(pos.X, pos.Z);
        if (!_sections.TryGetValue(key, out var section))
        {
            section = new PoiSection(pos);
            _sections[key] = section;
        }
        return section;
    }

    //Add 添加兴趣点到指定位置对应原版 PoiManager.add
    public void Add(BlockPos pos, PoiType type)
    {
        var chunkPos = new ChunkPos(pos.X >> 4, pos.Z >> 4);
        GetOrCreateSection(chunkPos).Add(pos, type);
    }

    //Remove 移除指定位置的兴趣点对应原版 PoiManager.remove
    public bool Remove(BlockPos pos)
    {
        var chunkPos = new ChunkPos(pos.X >> 4, pos.Z >> 4);
        return GetSection(chunkPos)?.Remove(pos) ?? false;
    }

    //GetType 获取指定位置的 PoiType 不存在返回 null
    public PoiType? GetType(BlockPos pos)
    {
        var chunkPos = new ChunkPos(pos.X >> 4, pos.Z >> 4);
        return GetSection(chunkPos)?.GetType(pos);
    }

    //Has 询问指定位置是否有 Poi
    public bool Has(BlockPos pos)
        => GetType(pos) is not null;

    //GetChunk PoiManager 抽象方法实现返回 PoiSection 占位
    public override object? GetChunk(ChunkPos pos)
        => GetSection(pos);

    //Clear 清空所有 PoiSection
    public void Clear() => _sections.Clear();
}

//PoiSection 区块内兴趣点集合对应原版 net.minecraft.world.entity.ai.village.poi.PoiSection
//持有单个区块内所有 PoiRecord 用 BlockPos.AsLong 索引
public sealed class PoiSection
{
    public ChunkPos ChunkPos { get; }
    private readonly Dictionary<long, PoiRecord> _records = new();

    public PoiSection(ChunkPos chunkPos)
    {
        ChunkPos = chunkPos;
    }

    //Add 添加 PoiRecord 用 BlockPos.AsLong 索引
    public void Add(BlockPos pos, PoiType type)
        => _records[pos.AsLong()] = new PoiRecord(pos, type);

    //Remove 移除指定位置的 PoiRecord
    public bool Remove(BlockPos pos)
        => _records.Remove(pos.AsLong());

    //GetType 获取指定位置的 PoiType 不存在返回 null
    public PoiType? GetType(BlockPos pos)
        => _records.TryGetValue(pos.AsLong(), out var record) ? record.Type : null;

    //GetAll 获取所有 PoiRecord 副本
    public IReadOnlyCollection<PoiRecord> GetAll() => _records.Values.ToList();

    //Count 区块内 Poi 数量
    public int Count => _records.Count;
}

//PoiRecord 单条兴趣点记录对应原版 net.minecraft.world.entity.ai.village.poi.PoiRecord
//持有位置与类型占用状态由 Occupied 字段表示
public sealed class PoiRecord
{
    public BlockPos Pos { get; }
    public PoiType Type { get; }
    public bool Occupied { get; set; }

    public PoiRecord(BlockPos pos, PoiType type)
    {
        Pos = pos;
        Type = type;
    }
}
