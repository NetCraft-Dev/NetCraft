namespace NetCraft.Storage.Paletted;

//Configuration interface, maps to vanilla net.minecraft.world.level.chunk.Configuration
//Describes the palette config: bitsInMemory is the in-memory bits per entry, bitsInStorage the serialized bits per entry
public interface Configuration
{
    //Whether repacking is needed, maps to vanilla alwaysRepack
    bool AlwaysRepack { get; }

    //Bits per element in memory, maps to vanilla bitsInMemory
    int BitsInMemory { get; }

    //Bits per element serialized to disk, maps to vanilla bitsInStorage
    int BitsInStorage { get; }

    //Create a palette from the strategy and initial entries, maps to vanilla createPalette
    Palette<T> CreatePalette<T>(Strategy<T> strategy, IReadOnlyList<T> paletteEntries);
}

//Palette factory interface, maps to vanilla Palette.Factory
//C# delegates cannot have generic methods, so an interface expresses it
public interface IPaletteFactory
{
    Palette<T> Create<T>(int bits, IReadOnlyList<T> paletteEntries);
}

//Simple configuration, maps to vanilla Configuration.Simple
//bitsInMemory equals bitsInStorage; factory picks the palette type
public sealed record SimpleConfiguration(IPaletteFactory Factory, int Bits) : Configuration
{
    public bool AlwaysRepack => false;

    public int BitsInMemory => Bits;

    public int BitsInStorage => Bits;

    public Palette<T> CreatePalette<T>(Strategy<T> strategy, IReadOnlyList<T> paletteEntries)
        => Factory.Create<T>(Bits, paletteEntries);
}

//Global configuration, maps to vanilla Configuration.Global
//bitsInMemory differs from bitsInStorage and alwaysRepack=true
public sealed record GlobalConfiguration(int BitsInMemory, int BitsInStorage) : Configuration
{
    public bool AlwaysRepack => true;

    public Palette<T> CreatePalette<T>(Strategy<T> strategy, IReadOnlyList<T> paletteEntries)
        => strategy.GlobalPalette;
}
