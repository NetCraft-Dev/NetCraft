using NetCraft.Registry;

namespace NetCraft.Storage.Paletted;

//Strategy abstract class, maps to vanilla net.minecraft.world.level.chunk.Strategy
//Decides entryCount and which Configuration based on bitsPerAxis and globalMap
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

    //globalPaletteBits is the network segmentation lower bound: block states 9, biomes 4
    //The global config bit width takes the larger of the registry width and the lower bound, ensuring BitsInMemory==BitsInStorage
    //The client segments by the network bits byte 0/1-4/5-8/9+; a global width below the lower bound is misread as a hash palette
    protected Strategy(IdMap<T> globalMap, int bitsPerAxis, int globalPaletteBits)
    {
        GlobalMap = globalMap;
        GlobalPalette = new GlobalPalette<T>(globalMap);
        GlobalPaletteBitsInMemory = Math.Max(MinimumBitsRequiredForDistinctValues(globalMap.Size), globalPaletteBits);
        BitsPerAxis = bitsPerAxis;
        EntryCount = 1 << (bitsPerAxis * 3);
    }

    //Return the Configuration for a bit count, implemented by subclasses
    protected internal abstract Configuration GetConfigurationForBitCount(int entryBits);

    //Strategy for BlockState, bitsPerAxis=4, supporting 8 palettes
    public static Strategy<T> CreateForBlockStates(IdMap<T> registry)
        => new BlockStatesStrategy<T>(registry);

    //Strategy for Biome, bitsPerAxis=2, supporting 4 palettes
    public static Strategy<T> CreateForBiomes(IdMap<T> registry)
        => new BiomesStrategy<T>(registry);

    //Compute the storage index from xyz, maps to vanilla getIndex
    public int GetIndex(int x, int y, int z) => (((y << BitsPerAxis) | z) << BitsPerAxis) | x;

    //Derive the needed bits from palette size and return the matching Configuration
    public Configuration GetConfigurationForPaletteSize(int paletteSize)
        => GetConfigurationForBitCount(MinimumBitsRequiredForDistinctValues(paletteSize));

    //Compute the minimum bits needed to store count distinct values, maps to vanilla minimumBitsRequiredForDistinctValues
    private static int MinimumBitsRequiredForDistinctValues(int count)
    {
        if (count <= 1) return 0;
        return (int)Math.Ceiling(Math.Log2(count));
    }
}

//BlockState Strategy, bitsPerAxis=4, entryCount=4096
//Supports 0/1-4 (all 4 bits linear)/5-8 (hashmap)/9+ (global)
//Vanilla maps 1/2/3/4 all to FOUR_BITS_LINEAR; the client reads packets with the same mapping
//Sending the actual 1-3 bits would mismatch the client's 4-bit storage expectation and disconnect on an out-of-bounds read
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

//Biome Strategy, bitsPerAxis=2, entryCount=64
//Supports 0/1-3 (linear)/4+ (global)
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
