using NetCraft.Registry;
using NetCraft.Registry.State;
using HeightmapRegistry = NetCraft.Registry.Heightmap;

namespace NetCraft.Storage.LevelGen;

//Heightmap instance, maps to vanilla net.minecraft.world.level.levelgen.Heightmap
//Stores one height value per column in packed form using BitStorage
//The Types enum references NetCraft.Registry.Heightmap.Types to avoid duplicating the definition
public sealed class Heightmap
{
    //Minimum height per column; matches vanilla -1 for an empty column
    public const int MinValue = -1;

    private readonly HeightmapRegistry.Types _type;
    private readonly int _minY;
    private readonly int _height;
    private readonly BitStorage _storage;

    public Heightmap(HeightmapRegistry.Types type, int minY, int height)
    {
        _type = type;
        _minY = minY;
        _height = height;
        //Each column stores height+1 possible values (0..height) in ceil(log2(height+1)) bits
        var bits = Math.Max(1, BitCount(height + 1));
        _storage = new SimpleBitStorage(bits, 16 * 16);
    }

    private Heightmap(HeightmapRegistry.Types type, int minY, int height, BitStorage storage)
    {
        _type = type;
        _minY = minY;
        _height = height;
        _storage = storage;
    }

    public HeightmapRegistry.Types Type => _type;
    public int MinY => _minY;
    public int Height => _height;

    //GetFirstAvailable returns the column's first available height, i.e. one above the highest contributing block, maps to vanilla getFirstAvailable
    //Vanilla returns the position where "going one more up is open air"; heightmap-based decoration placement and spawn columns use it, and off-by-one would place things inside blocks
    //An empty column returns MinValue, meaning no contributing block in that column
    public int GetFirstAvailable(int x, int z)
    {
        var raw = _storage.Get(GetIndex(x, z));
        return raw == 0 ? MinValue : _minY + raw;
    }

    //Update the column height, writing when the new height is above the stored one, maps to vanilla update
    public bool Update(int x, int y, int z)
    {
        var index = GetIndex(x, z);
        var current = _storage.Get(index);
        var candidate = y - _minY + 1;
        if (candidate > current)
        {
            _storage.Set(index, candidate);
            return true;
        }
        return false;
    }

    //SetHeight sets the column's first available height directly, maps to vanilla setHeight
    //The argument is the position where "going one more up is open air" (highest block y + 1); below minY counts as an empty column
    public void SetHeight(int x, int z, int y)
    {
        var raw = y < _minY ? 0 : y - _minY;
        _storage.Set(GetIndex(x, z), raw);
    }

    //IsOpaqueFor, whether that type counts the block as occluding, maps to vanilla Heightmap.Types.isOpaque
    //The vanilla criterion is isAir/blocksMotion/fluid/leaves tags; this project has no collision shape or fluid system yet
    //Instead it approximates with the light dampening: 0 is air, 1-14 are translucent blocks like water and leaves, 15 is solid
    //OCEAN_FLOOR counts only solid motion-blocking blocks, so it needs 15; the rest, including fluids, use greater than 0
    public static bool IsOpaqueFor(HeightmapRegistry.Types type, BlockState state) => type switch
    {
        HeightmapRegistry.Types.OceanFloor or HeightmapRegistry.Types.OceanFloorWg
            => state.GetLightDampening() >= 15,
        _ => state.GetLightDampening() > 0,
    };

    //PrimeHeightmaps recomputes the heightmaps of the given types from chunk content, maps to vanilla Heightmap.primeHeightmaps
    //Each column scans down from the top of the highest section; the first block contributing to that heightmap fixes the column for that type
    //After computing, writes back to chunk.Heightmaps for serialization; a column with no occluder stays 0, an empty column
    public static void PrimeHeightmaps(ChunkAccess chunk, IReadOnlyList<HeightmapRegistry.Types> types)
    {
        if (types.Count == 0) return;
        var maps = new Heightmap[types.Count];
        var primed = new bool[types.Count];
        for (var i = 0; i < types.Count; i++)
            maps[i] = chunk.GetOrCreateHeightmapForType(types[i]);

        var minY = chunk.MinSectionY * 16;
        var topY = chunk.MaxSectionY * 16 + 15;
        var baseX = chunk.Pos.X << 4;
        var baseZ = chunk.Pos.Z << 4;
        for (var x = 0; x < 16; x++)
        {
            for (var z = 0; z < 16; z++)
            {
                var remaining = types.Count;
                Array.Clear(primed);
                for (var y = topY; y >= minY && remaining > 0; y--)
                {
                    var state = chunk.GetBlockState(baseX + x, y, baseZ + z);
                    //Air contributes to no heightmap, skip the whole layer
                    if (state.GetLightDampening() == 0) continue;
                    for (var i = 0; i < maps.Length; i++)
                    {
                        if (primed[i] || !IsOpaqueFor(types[i], state)) continue;
                        //Stores the first available height, i.e. one above the block
                        maps[i].SetHeight(x, z, y + 1);
                        primed[i] = true;
                        remaining--;
                    }
                }
            }
        }
        for (var i = 0; i < maps.Length; i++)
            chunk.Heightmaps[types[i]] = maps[i].GetData();
    }

    //Raw long array, maps to vanilla getData
    public long[] GetData() => _storage.GetRaw();

    //Rebuild the heightmap from raw data, maps to vanilla setData
    public static Heightmap FromData(HeightmapRegistry.Types type, int minY, int height, long[] data)
    {
        var bits = Math.Max(1, BitCount(height + 1));
        var storage = new SimpleBitStorage(bits, 16 * 16, data);
        return new Heightmap(type, minY, height, storage);
    }

    private static int GetIndex(int x, int z) => (z & 15) * 16 + (x & 15);

    //Compute the minimum bits, maps to vanilla ceil(log2(value))
    private static int BitCount(int value)
    {
        var bits = 0;
        var v = value;
        while (v > 0) { bits++; v >>= 1; }
        return bits == 0 ? 1 : bits;
    }
}

//HeightmapFunction, heightmap predicate function, maps to vanilla Heightmap.Function
//Decides whether a block state contributes to that heightmap
public delegate bool HeightmapFunction(BlockState state);
