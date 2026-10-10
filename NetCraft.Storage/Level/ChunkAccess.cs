using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage.Chunk;
using HeightmapRegistry = NetCraft.Registry.Heightmap;

namespace NetCraft.Storage;

//ChunkAccess, chunk access abstract base class, maps to vanilla net.minecraft.world.level.chunk.ChunkAccess
//Holds basic fields: chunk pos, height access, chunk status, heightmaps
//Subclasses LevelChunk/ProtoChunk extend with concrete fields as needed
public abstract class ChunkAccess : LevelHeightAccessor
{
    //Heightmap instance cache, maps to the vanilla heightmaps field
    //The abstract Heightmaps property holds long[] serialized data; this cache holds mutable instances so updates are not lost on rebuild
    private Dictionary<HeightmapRegistry.Types, LevelGen.Heightmap>? _heightmapCache;

    //Pos, the chunk position
    public abstract ChunkPos Pos { get; }

    //MinSectionY, the lowest section Y, maps to vanilla getMinSection
    public abstract int MinSectionY { get; }

    //SectionsCount, the section count, maps to vanilla getSectionsCount
    public abstract int SectionsCount { get; }

    //MaxSectionY derived from MinSectionY+SectionsCount-1, maps to vanilla getMaxSection
    public int MaxSectionY => MinSectionY + SectionsCount - 1;

    //ChunkStatus, the chunk status
    public abstract ChunkStatus ChunkStatus { get; }

    //Heightmaps, the heightmap collection, maps to vanilla getHeightmaps
    public abstract IDictionary<HeightmapRegistry.Types, long[]> Heightmaps { get; }

    //PostProcessingSections, positions needing post-processing grouped by section; each item is a section-local coord packed into a short
    //Maps to vanilla ChunkAccess.postProcessing; positions are registered after carving breaks through fluids
    public virtual List<short>?[] PostProcessingSections => Array.Empty<List<short>?>();

    //MarkPosForPostProcessing registers a block position needing post-processing, maps to vanilla markPosForPostProcessing
    public virtual void MarkPosForPostProcessing(int worldX, int worldY, int worldZ) { }

    //BlockTicks, this chunk's block scheduled tick container, maps to vanilla LevelChunk.blockTicks
    //Generation and runtime share one container; vanilla has a separate ProtoChunkTicks during generation that zeroes the delay, not distinguished here yet
    public Ticks.LevelChunkTicks<NetCraft.Registry.Block> BlockTicks { get; private set; } = new();

    //FluidTicks, this chunk's fluid scheduled tick container
    public Ticks.LevelChunkTicks<NetCraft.Registry.Fluid> FluidTicks { get; private set; } = new();

    //SetBlockTicks replaces the whole block scheduled tick container on load, maps to injecting blockTicks in the vanilla constructor
    public void SetBlockTicks(Ticks.LevelChunkTicks<NetCraft.Registry.Block> ticks) => BlockTicks = ticks;

    //SetFluidTicks replaces the whole fluid scheduled tick container on load
    public void SetFluidTicks(Ticks.LevelChunkTicks<NetCraft.Registry.Fluid> ticks) => FluidTicks = ticks;

    //SetBlockState writes a block by world coords, maps to vanilla setBlockState; out-of-range sections are dropped
    public virtual void SetBlockState(int worldX, int worldY, int worldZ, BlockState state)
    {
        var section = GetSection(worldY >> 4);
        section?.SetBlockState(worldX & 15, worldY & 15, worldZ & 15, state);
    }

    //GetSection gets the section data by section Y; out of range returns null, maps to vanilla getSection
    public abstract LevelChunkSection? GetSection(int sectionY);

    //GetOrCreateHeightmapForType creates or gets the heightmap instance by type, maps to vanilla getOrCreateHeightmap
    //On first call it rebuilds the instance from the Heightmaps long[] and caches it; later calls return the same instance
    public virtual LevelGen.Heightmap GetOrCreateHeightmapForType(HeightmapRegistry.Types type)
    {
        _heightmapCache ??= new Dictionary<HeightmapRegistry.Types, LevelGen.Heightmap>();
        if (_heightmapCache.TryGetValue(type, out var cached))
            return cached;
        var data = Heightmaps.TryGetValue(type, out var d) ? d : Array.Empty<long>();
        var instance = data.Length == 0
            ? new LevelGen.Heightmap(type, MinSectionY * 16, SectionsCount * 16)
            : LevelGen.Heightmap.FromData(type, MinSectionY * 16, SectionsCount * 16, data);
        _heightmapCache[type] = instance;
        return instance;
    }

    //GetHeight, maps to vanilla getHeight, takes the column height by type
    //When the type has not been computed, prime it wholesale first, maps to the vanilla getHeight branch that runs primeHeightmaps on a missing heightmap
    public int GetHeight(HeightmapRegistry.Types type, int x, int z)
    {
        if (!Heightmaps.ContainsKey(type))
            LevelGen.Heightmap.PrimeHeightmaps(this, new[] { type });
        return GetOrCreateHeightmapForType(type).GetFirstAvailable(x, z);
    }

    //GetBlockState reads a block state by world coords; out-of-range sections return air, maps to vanilla ChunkAccess.getBlockState
    public virtual BlockState GetBlockState(int worldX, int worldY, int worldZ)
        => GetSection(worldY >> 4)?.GetBlockState(worldX & 15, worldY & 15, worldZ & 15) ?? default;

    //UpdateHeightmaps incrementally maintains the heightmaps after a block change, maps to heightmap.update in vanilla LevelChunk.setBlockState
    //Only maintains types with an existing instance; types not yet computed are primed wholesale on the next GetHeight
    //When the new block does not contribute to that heightmap and the old one held the column's highest point, rescan downward for a new occluder
    public virtual void UpdateHeightmaps(int worldX, int worldY, int worldZ, BlockState state)
    {
        if (_heightmapCache is null || _heightmapCache.Count == 0) return;
        var localX = worldX & 15;
        var localZ = worldZ & 15;
        foreach (var (type, map) in _heightmapCache)
        {
            //GetFirstAvailable is one above the highest occluding block; the comparison here is against the block's own height
            var firstAvailable = map.GetFirstAvailable(localX, localZ);
            var highest = firstAvailable == LevelGen.Heightmap.MinValue ? int.MinValue : firstAvailable - 1;
            //Lower than the previous highest block means this column is unaffected
            if (worldY < highest) continue;
            if (LevelGen.Heightmap.IsOpaqueFor(type, state))
            {
                //Raise only when above the previous highest block
                if (worldY <= highest) continue;
                map.SetHeight(localX, localZ, worldY + 1);
            }
            else
            {
                //Only when the previous highest block is replaced with a non-occluding one, rescan downward for a new occluder
                if (highest != worldY) continue;
                var found = int.MinValue;
                for (var y = worldY - 1; y >= MinSectionY * 16; y--)
                {
                    if (!LevelGen.Heightmap.IsOpaqueFor(type, GetBlockState(worldX, y, worldZ))) continue;
                    found = y;
                    break;
                }
                map.SetHeight(localX, localZ, found == int.MinValue ? int.MinValue : found + 1);
            }
            Heightmaps[type] = map.GetData();
        }
    }

    //SetBiome writes a biome by world coords, maps to vanilla setBiome
    //Converts world coords to sectionY and quart local coords, delegating to section.SetBiome
    public virtual void SetBiome(int worldX, int worldY, int worldZ, Holder<Biome> biome)
    {
        var section = GetSection(worldY >> 4);
        if (section is null) return;
        section.SetBiome((worldX >> 2) & 3, (worldY >> 2) & 3, (worldZ >> 2) & 3, biome);
    }

    //GetNoiseBiome queries the biome by quart world coords, maps to vanilla getNoiseBiome
    public virtual Holder<Biome> GetNoiseBiome(int quartX, int quartY, int quartZ)
    {
        //quartY >> 2 is already the absolute section index; adding MinSectionY on top shifted every lookup four
        //sections down and pushed the bottom of the world out of range, where the fallback answered plains
        var section = GetSection(quartY >> 2);
        return section is null
            ? EmptyBiomeHolder
            : section.GetNoiseBiome(quartX & 3, quartY & 3, quartZ & 3);
    }

    //EmptyBiome, the default biome placeholder returned for out-of-range sections, avoiding null
    private static readonly Biome EmptyBiome = new EmptyBiomeImpl();

    //The placeholder's holder is built once; the out-of-range branch used to allocate a fresh Direct<Biome> per query
    private static readonly Holder<Biome> EmptyBiomeHolder = Holder<Biome>.Direct(EmptyBiome);
    private sealed class EmptyBiomeImpl : Biome
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("plains");
    }
}
