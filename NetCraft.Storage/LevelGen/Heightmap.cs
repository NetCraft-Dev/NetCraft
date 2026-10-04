using NetCraft.Registry;
using NetCraft.Registry.State;
using HeightmapRegistry = NetCraft.Registry.Heightmap;

namespace NetCraft.Storage.LevelGen;

//Heightmap 高度图实例对应原版 net.minecraft.world.level.levelgen.Heightmap
//每列存一个高度值用 BitStorage 紧凑存储
//Types 枚举引用 NetCraft.Registry.Heightmap.Types 避免重复定义
public sealed class Heightmap
{
    //每列最小高度对应原版 -1 表示空列
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
        //每列存储 height+1 个可能值(0..height)用 ceil(log2(height+1)) 位
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

    //GetFirstAvailable 取该列第一个可用高度即最高参与方块的上方一格 对应原版 getFirstAvailable
    //原版返回的是"再往上就腾空"的位置 高度图定位装饰与出生点列都按它算 少这一格会把位置落在方块内部
    //空列返回 MinValue 表示该列没有任何参与方块
    public int GetFirstAvailable(int x, int z)
    {
        var raw = _storage.Get(GetIndex(x, z));
        return raw == 0 ? MinValue : _minY + raw;
    }

    //更新指定列高度若新高度高于已存值则写入对应原版 update
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

    //SetHeight 直接设置该列第一个可用高度 对应原版 setHeight
    //参数是"再往上就腾空"的位置(最高方块 y + 1) 低于 minY 视为空列
    public void SetHeight(int x, int z, int y)
    {
        var raw = y < _minY ? 0 : y - _minY;
        _storage.Set(GetIndex(x, z), raw);
    }

    //IsOpaqueFor 该类型是否把该方块算作遮挡 对应原版 Heightmap.Types.isOpaque
    //原版判据是 isAir/blocksMotion/流体/树叶标签 本作尚无碰撞形状与流体体系
    //改用减光等级近似: 0 是空气 1-14 是水与树叶这类透光方块 15 是实心方块
    //OCEAN_FLOOR 只算阻挡移动的实心方块故要 15 其余含流体的用大于 0
    public static bool IsOpaqueFor(HeightmapRegistry.Types type, BlockState state) => type switch
    {
        HeightmapRegistry.Types.OceanFloor or HeightmapRegistry.Types.OceanFloorWg
            => state.GetLightDampening() >= 15,
        _ => state.GetLightDampening() > 0,
    };

    //PrimeHeightmaps 从区块内容整体补算指定类型高度图 对应原版 Heightmap.primeHeightmaps
    //每列从最高区段顶向下扫 遇到第一个参与该高度图的方块就取其高度 该列该类型即定
    //算完写回 chunk.Heightmaps 供序列化 整列无遮挡的列保持 0 即空列
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
                    //空气不参与任何高度图 整层直接跳过
                    if (state.GetLightDampening() == 0) continue;
                    for (var i = 0; i < maps.Length; i++)
                    {
                        if (primed[i] || !IsOpaqueFor(types[i], state)) continue;
                        //存的是第一个可用高度即方块上方一格
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

    //原始 long 数组对应原版 getData
    public long[] GetData() => _storage.GetRaw();

    //从原始数据重建高度图对应原版 setData
    public static Heightmap FromData(HeightmapRegistry.Types type, int minY, int height, long[] data)
    {
        var bits = Math.Max(1, BitCount(height + 1));
        var storage = new SimpleBitStorage(bits, 16 * 16, data);
        return new Heightmap(type, minY, height, storage);
    }

    private static int GetIndex(int x, int z) => (z & 15) * 16 + (x & 15);

    //计算最小位数对应原版 ceil(log2(value))
    private static int BitCount(int value)
    {
        var bits = 0;
        var v = value;
        while (v > 0) { bits++; v >>= 1; }
        return bits == 0 ? 1 : bits;
    }
}

//HeightmapFunction 高度图判定函数对应原版 Heightmap.Function
//判断方块状态是否参与该高度图计算
public delegate bool HeightmapFunction(BlockState state);
