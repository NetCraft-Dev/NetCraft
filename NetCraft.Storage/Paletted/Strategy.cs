using NetCraft.Registry;

namespace NetCraft.Storage.Paletted;

//Strategy抽象类对应原版net.minecraft.world.level.chunk.Strategy
//根据bitsPerAxis与globalMap决定entryCount和何种Configuration
public abstract class Strategy<T>
{
    private static readonly IPaletteFactory SingleValueFactory = new SingleValuePaletteFactory();
    private static readonly IPaletteFactory LinearFactory = new LinearPaletteFactory();
    private static readonly IPaletteFactory HashMapFactory = new HashMapPaletteFactory();

    internal static readonly SimpleConfiguration ZeroBits = new(SingleValueFactory, 0);
    internal static readonly SimpleConfiguration OneBitLinear = new(LinearFactory, 1);
    internal static readonly SimpleConfiguration TwoBitsLinear = new(LinearFactory, 2);
    internal static readonly SimpleConfiguration ThreeBitsLinear = new(LinearFactory, 3);
    internal static readonly SimpleConfiguration FourBitsLinear = new(LinearFactory, 4);
    internal static readonly SimpleConfiguration FiveBitsHashMap = new(HashMapFactory, 5);
    internal static readonly SimpleConfiguration SixBitsHashMap = new(HashMapFactory, 6);
    internal static readonly SimpleConfiguration SevenBitsHashMap = new(HashMapFactory, 7);
    internal static readonly SimpleConfiguration EightBitsHashMap = new(HashMapFactory, 8);

    public IdMap<T> GlobalMap { get; }
    public GlobalPalette<T> GlobalPalette { get; }
    protected int GlobalPaletteBitsInMemory { get; }
    public int BitsPerAxis { get; }
    public int EntryCount { get; }

    //globalPaletteBits 为网络分段下限 block states 9 biomes 4
    //global 配置位宽取 registry 位宽与下限的较大值保证 BitsInMemory==BitsInStorage
    //客户端按网络 bits byte 分段 0/1-4/5-8/9+ 全局位宽低于下限会被误读为 hash palette
    protected Strategy(IdMap<T> globalMap, int bitsPerAxis, int globalPaletteBits)
    {
        GlobalMap = globalMap;
        GlobalPalette = new GlobalPalette<T>(globalMap);
        GlobalPaletteBitsInMemory = Math.Max(MinimumBitsRequiredForDistinctValues(globalMap.Size), globalPaletteBits);
        BitsPerAxis = bitsPerAxis;
        EntryCount = 1 << (bitsPerAxis * 3);
    }

    //根据bit数返回对应Configuration由子类实现
    protected internal abstract Configuration GetConfigurationForBitCount(int entryBits);

    //BlockState专用Strategy bitsPerAxis=4支持8种palette
    public static Strategy<T> CreateForBlockStates(IdMap<T> registry)
        => new BlockStatesStrategy<T>(registry);

    //Biome专用Strategy bitsPerAxis=2支持4种palette
    public static Strategy<T> CreateForBiomes(IdMap<T> registry)
        => new BiomesStrategy<T>(registry);

    //由xyz计算storage index对应原版getIndex
    public int GetIndex(int x, int y, int z) => (((y << BitsPerAxis) | z) << BitsPerAxis) | x;

    //根据palette size推算所需bits返回对应Configuration
    public Configuration GetConfigurationForPaletteSize(int paletteSize)
        => GetConfigurationForBitCount(MinimumBitsRequiredForDistinctValues(paletteSize));

    //计算存储count个不同值所需的最小bit数对应原版minimumBitsRequiredForDistinctValues
    private static int MinimumBitsRequiredForDistinctValues(int count)
    {
        if (count <= 1) return 0;
        return (int)Math.Ceiling(Math.Log2(count));
    }
}

//BlockState Strategy bitsPerAxis=4 entryCount=4096
//支持0/1-4(全部4 bits linear)/5-8(hashmap)/9+(global)
//原版 1/2/3/4 一律映射 FOUR_BITS_LINEAR 客户端按同款映射读网络包
//若按实际位宽发 1-3 bits 客户端 storage 期望 4 bits 长度不符直接越界断连
internal sealed class BlockStatesStrategy<T> : Strategy<T>
{
    public BlockStatesStrategy(IdMap<T> globalMap) : base(globalMap, 4, 9) { }

    protected internal override Configuration GetConfigurationForBitCount(int entryBits)
    {
        return entryBits switch
        {
            0 => Strategy<object>.ZeroBits,
            1 or 2 or 3 or 4 => Strategy<object>.FourBitsLinear,
            5 => Strategy<object>.FiveBitsHashMap,
            6 => Strategy<object>.SixBitsHashMap,
            7 => Strategy<object>.SevenBitsHashMap,
            8 => Strategy<object>.EightBitsHashMap,
            _ => new GlobalConfiguration(GlobalPaletteBitsInMemory, GlobalPaletteBitsInMemory)
        };
    }
}

//Biome Strategy bitsPerAxis=2 entryCount=64
//支持0/1-3(linear)/4+(global)
internal sealed class BiomesStrategy<T> : Strategy<T>
{
    public BiomesStrategy(IdMap<T> globalMap) : base(globalMap, 2, 4) { }

    protected internal override Configuration GetConfigurationForBitCount(int entryBits)
    {
        return entryBits switch
        {
            0 => Strategy<object>.ZeroBits,
            1 => Strategy<object>.OneBitLinear,
            2 => Strategy<object>.TwoBitsLinear,
            3 => Strategy<object>.ThreeBitsLinear,
            _ => new GlobalConfiguration(GlobalPaletteBitsInMemory, GlobalPaletteBitsInMemory)
        };
    }
}

internal sealed class SingleValuePaletteFactory : IPaletteFactory
{
    public Palette<T> Create<T>(int bits, IReadOnlyList<T> paletteEntries)
        => new SingleValuePalette<T>(paletteEntries);
}

internal sealed class LinearPaletteFactory : IPaletteFactory
{
    public Palette<T> Create<T>(int bits, IReadOnlyList<T> paletteEntries)
        => new LinearPalette<T>(bits, paletteEntries);
}

internal sealed class HashMapPaletteFactory : IPaletteFactory
{
    public Palette<T> Create<T>(int bits, IReadOnlyList<T> paletteEntries)
        => new HashMapPalette<T>(bits, paletteEntries);
}
