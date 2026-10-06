using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Storage;

//SimplePoiManager, point-of-interest manager concrete implementation, a simplified version of vanilla PoiManager
//Holds a dictionary of per-chunk PoiSection indexed by ChunkPos.Pack
//Provides the basic Add/Get/Remove/Has APIs for villages, iron golems and similar
public sealed class SimplePoiManager : PoiManager
{
    //Empty, an empty PoiManager singleton, used by PersistentServerLevel.LoadChunkAsync during deserialization
    public static readonly SimplePoiManager Empty = new();

    private readonly Dictionary<long, PoiSection> _sections = new();

    //GetSection gets the PoiSection of the given chunk, or null when absent
    public PoiSection? GetSection(ChunkPos pos)
        => _sections.TryGetValue(ChunkPos.Pack(pos.X, pos.Z), out var section) ? section : null;

    //GetOrCreateSection gets or creates a PoiSection
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

    //Add adds a point of interest at the given pos, maps to vanilla PoiManager.add
    public void Add(BlockPos pos, PoiType type)
    {
        var chunkPos = new ChunkPos(pos.X >> 4, pos.Z >> 4);
        GetOrCreateSection(chunkPos).Add(pos, type);
    }

    //Remove removes the point of interest at the given pos, maps to vanilla PoiManager.remove
    public bool Remove(BlockPos pos)
    {
        var chunkPos = new ChunkPos(pos.X >> 4, pos.Z >> 4);
        return GetSection(chunkPos)?.Remove(pos) ?? false;
    }

    //GetType gets the PoiType at the given pos, or null when absent
    public PoiType? GetType(BlockPos pos)
    {
        var chunkPos = new ChunkPos(pos.X >> 4, pos.Z >> 4);
        return GetSection(chunkPos)?.GetType(pos);
    }

    //Has reports whether the given pos has a Poi
    public bool Has(BlockPos pos)
        => GetType(pos) is not null;

    //GetChunk, the PoiManager abstract method implementation returning the PoiSection placeholder
    public override object? GetChunk(ChunkPos pos)
        => GetSection(pos);

    //Clear clears all PoiSections
    public void Clear() => _sections.Clear();
}

//PoiSection, point-of-interest collection within a chunk, maps to vanilla net.minecraft.world.entity.ai.village.poi.PoiSection
//Holds all PoiRecords in a single chunk, indexed by BlockPos.AsLong
public sealed class PoiSection
{
    public ChunkPos ChunkPos { get; }
    private readonly Dictionary<long, PoiRecord> _records = new();

    public PoiSection(ChunkPos chunkPos)
    {
        ChunkPos = chunkPos;
    }

    //Add adds a PoiRecord indexed by BlockPos.AsLong
    public void Add(BlockPos pos, PoiType type)
        => _records[pos.AsLong()] = new PoiRecord(pos, type);

    //Remove removes the PoiRecord at the given pos
    public bool Remove(BlockPos pos)
        => _records.Remove(pos.AsLong());

    //GetType gets the PoiType at the given pos, or null when absent
    public PoiType? GetType(BlockPos pos)
        => _records.TryGetValue(pos.AsLong(), out var record) ? record.Type : null;

    //GetAll gets a copy of all PoiRecords
    public IReadOnlyCollection<PoiRecord> GetAll() => _records.Values.ToList();

    //Count, the number of Poi in the chunk
    public int Count => _records.Count;
}

//PoiRecord, a single point-of-interest record, maps to vanilla net.minecraft.world.entity.ai.village.poi.PoiRecord
//Holds the pos and type; occupancy is represented by the Occupied field
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
