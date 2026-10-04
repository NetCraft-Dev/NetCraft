namespace NetCraft.Storage.Paletted;

//Configuration接口对应原版net.minecraft.world.level.chunk.Configuration
//描述palette配置bitsInMemory存储位宽bitsInStorage序列化位宽
public interface Configuration
{
    //是否需要重新打包对应原版alwaysRepack
    bool AlwaysRepack { get; }

    //内存中每元素bit数对应原版bitsInMemory
    int BitsInMemory { get; }

    //序列化到存档的每元素bit数对应原版bitsInStorage
    int BitsInStorage { get; }

    //根据strategy与初始条目创建palette对应原版createPalette
    Palette<T> CreatePalette<T>(Strategy<T> strategy, IReadOnlyList<T> paletteEntries);
}

//Palette工厂接口对应原版Palette.Factory
//C#委托不支持泛型方法用接口表达
public interface IPaletteFactory
{
    Palette<T> Create<T>(int bits, IReadOnlyList<T> paletteEntries);
}

//简单配置对应原版Configuration.Simple
//bitsInMemory与bitsInStorage相同factory指定palette类型
public sealed record SimpleConfiguration(IPaletteFactory Factory, int Bits) : Configuration
{
    public bool AlwaysRepack => false;

    public int BitsInMemory => Bits;

    public int BitsInStorage => Bits;

    public Palette<T> CreatePalette<T>(Strategy<T> strategy, IReadOnlyList<T> paletteEntries)
        => Factory.Create<T>(Bits, paletteEntries);
}

//Global配置对应原版Configuration.Global
//bitsInMemory与bitsInStorage不同alwaysRepack=true
public sealed record GlobalConfiguration(int BitsInMemory, int BitsInStorage) : Configuration
{
    public bool AlwaysRepack => true;

    public Palette<T> CreatePalette<T>(Strategy<T> strategy, IReadOnlyList<T> paletteEntries)
        => strategy.GlobalPalette;
}
