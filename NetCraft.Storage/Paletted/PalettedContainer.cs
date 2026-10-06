using NetCraft.Codec;

namespace NetCraft.Storage.Paletted;

//PalettedContainer, maps to vanilla net.minecraft.world.level.chunk.PalettedContainer
//Uses a palette and bit storage to pack repetitive data such as block states and biomes
public sealed class PalettedContainer<T> : PaletteResize<T>
{
    private volatile Data _data;
    private readonly Strategy<T> _strategy;

    public PalettedContainer(T initialValue, Strategy<T> strategy)
    {
        _strategy = strategy;
        _data = CreateOrReuseData(null, 0);
        _data.Palette.IdFor(initialValue, this);
    }

    private PalettedContainer(Strategy<T> strategy, Configuration configuration, BitStorage storage, Palette<T> palette)
    {
        _strategy = strategy;
        _data = new Data(configuration, storage, palette);
    }

    private PalettedContainer(PalettedContainer<T> source)
    {
        _strategy = source._strategy;
        _data = source._data.Copy();
    }

    //Maps to vanilla onResize; when the palette runs out of space it creates a new Data and migrates the contents
    public int OnResize(int bits, T lastAddedValue)
    {
        var oldData = _data;
        var newData = CreateOrReuseData(oldData, bits);
        newData.CopyFrom(oldData.Palette, oldData.Storage);
        _data = newData;
        return newData.Palette.IdFor(lastAddedValue, PaletteResize<T>.NoResizeExpected());
    }

    //Read/write access, maps to vanilla acquire/release, currently empty implementations
    public void Acquire() { }
    public void Release() { }

    public T GetAndSet(int x, int y, int z, T value)
    {
        Acquire();
        try
        {
            var result = GetAndSet(_strategy.GetIndex(x, y, z), value);
            Release();
            return result;
        }
        catch
        {
            Release();
            throw;
        }
    }

    public T GetAndSetUnchecked(int x, int y, int z, T value)
        => GetAndSet(_strategy.GetIndex(x, y, z), value);

    private T GetAndSet(int index, T value)
    {
        var id = _data.Palette.IdFor(value, this);
        var oldId = _data.Storage.GetAndSet(index, id);
        return _data.Palette.ValueFor(oldId);
    }

    public void Set(int x, int y, int z, T value)
    {
        Acquire();
        try
        {
            Set(_strategy.GetIndex(x, y, z), value);
            Release();
        }
        catch
        {
            Release();
            throw;
        }
    }

    private void Set(int index, T value)
    {
        var id = _data.Palette.IdFor(value, this);
        _data.Storage.Set(index, id);
    }

    public T Get(int x, int y, int z) => Get(_strategy.GetIndex(x, y, z));

    private T Get(int index)
    {
        var data = _data;
        return data.Palette.ValueFor(data.Storage.Get(index));
    }

    public void GetAll(Action<T> consumer)
    {
        var data = _data;
        var palette = data.Palette;
        var visited = new HashSet<int>();
        data.Storage.GetAll(id =>
        {
            if (visited.Add(id)) consumer(palette.ValueFor(id));
        });
    }

    public bool MaybeHas(Predicate<T> predicate) => _data.Palette.MaybeHas(predicate);

    public void ForEachInPalette(Action<T> consumer)
    {
        var data = _data;
        for (var i = 0; i < data.Palette.Size; i++)
            consumer(data.Palette.ValueFor(i));
    }

    public int BitsPerEntry => _data.Storage.Bits;

    public PalettedContainer<T> Copy() => new(this);

    //Rebuild as a container holding only the default value, maps to vanilla recreate
    public PalettedContainer<T> Recreate()
        => new(_data.Palette.ValueFor(0), _strategy);

    //Count occurrences of each value, maps to vanilla count
    //Allocate count slots by palette index rather than dictionary lookups per cell, saving a hash and the dictionary allocation itself
    public void Count(Action<T, int> output)
    {
        var data = _data;
        var palette = data.Palette;
        var size = palette.Size;
        if (size == 1)
        {
            output(palette.ValueFor(0), data.Storage.Size);
            return;
        }
        var counts = new int[size];
        data.Storage.GetAll(id =>
        {
            if ((uint)id < (uint)size) counts[id]++;
        });
        for (var id = 0; id < size; id++)
        {
            if (counts[id] != 0) output(palette.ValueFor(id), counts[id]);
        }
    }

    //Serialization packing, maps to vanilla pack; uses a HashMapPalette to renumber ids into a compact sequence
    public PackedData<T> Pack(Strategy<T> strategy)
    {
        Acquire();
        try
        {
            var currentStorage = _data.Storage;
            var currentPalette = _data.Palette;
            var newPalette = new HashMapPalette<T>(currentStorage.Bits);
            var entryCount = strategy.EntryCount;
            var newContents = ReencodeContents(currentStorage, currentPalette, newPalette);
            var storedConfiguration = strategy.GetConfigurationForPaletteSize(newPalette.Size);
            var bitsOnDisc = storedConfiguration.BitsInStorage;
            Optional<long[]> values;
            if (bitsOnDisc != 0)
            {
                var storage = new SimpleBitStorage(bitsOnDisc, entryCount, newContents);
                values = Optional<long[]>.Of(storage.GetRaw());
            }
            else
            {
                values = Optional<long[]>.Empty();
            }
            Release();
            return new PackedData<T>(newPalette.GetEntries(), values, bitsOnDisc);
        }
        catch
        {
            Release();
            throw;
        }
    }

    //GetPackedData packs with its own strategy, exposed for disk serialization
    //Network serialization must not use this method: pack renumbers ids compactly, which conflicts with the client reading global ids under a global config
    public PackedData<T> GetPackedData() => Pack(_strategy);

    //GetNetworkData exposes the runtime palette and raw storage for network serialization
    //Maps to vanilla PalettedContainer.write, writing runtime data directly without renumbering
    //With a global palette, entries is empty and storage is the global id
    public NetworkData<T> GetNetworkData()
    {
        Acquire();
        try
        {
            var data = _data;
            var palette = data.Palette;
            var bits = data.Configuration.BitsInStorage;
            IReadOnlyList<T> entries;
            if (palette is GlobalPalette<T>)
            {
                entries = Array.Empty<T>();
            }
            else
            {
                //Takes the flattened cache rather than building an array each time; every section sent to the client goes through here
                entries = data.FlatPalette;
            }
            var raw = data.Storage.GetRaw();
            Release();
            return new NetworkData<T>(bits, entries, raw);
        }
        catch
        {
            Release();
            throw;
        }
    }

    private Data CreateOrReuseData(Data? oldData, int targetBits)
    {
        var configuration = _strategy.GetConfigurationForBitCount(targetBits);
        if (oldData is not null && configuration.Equals(oldData.Configuration)) return oldData;
        BitStorage? storage = configuration.BitsInMemory == 0
            ? new ZeroBitStorage(_strategy.EntryCount)
            : new SimpleBitStorage(configuration.BitsInMemory, _strategy.EntryCount);
        var palette = configuration.CreatePalette(_strategy, Array.Empty<T>());
        return new Data(configuration, storage, palette);
    }

    //Re-encode the storage under the old palette into the new palette, maps to vanilla reencodeContents
    private static int[] ReencodeContents(BitStorage storage, Palette<T> oldPalette, Palette<T> newPalette)
    {
        var buffer = new int[storage.Size];
        storage.Unpack(buffer);
        var dummyResizer = PaletteResize<T>.NoResizeExpected();
        var lastReadId = -1;
        var lastWrittenId = -1;
        for (var index = 0; index < buffer.Length; index++)
        {
            var id = buffer[index];
            if (id != lastReadId)
            {
                lastReadId = id;
                lastWrittenId = newPalette.IdFor(oldPalette.ValueFor(id), dummyResizer);
            }
            buffer[index] = lastWrittenId;
        }
        return buffer;
    }

    //Deserialize PackedData into a PalettedContainer, maps to vanilla unpack
    //BitsPerEntry>=0 is the network path, building the config from the declared bit width; with a global palette entries is empty so it cannot be inferred from entry count
    //BitsPerEntry<0 is the disk path, inferring the config from the palette entry count
    public static DataResult<PalettedContainer<T>> Unpack(Strategy<T> strategy, PackedData<T> discData)
    {
        var paletteEntries = discData.PaletteEntries;
        var entryCount = strategy.EntryCount;
        var storedConfiguration = discData.BitsPerEntry >= 0
            ? strategy.GetConfigurationForBitCount(discData.BitsPerEntry)
            : strategy.GetConfigurationForPaletteSize(paletteEntries.Count);
        var bitsOnDisc = storedConfiguration.BitsInStorage;
        if (discData.BitsPerEntry != -1 && bitsOnDisc != discData.BitsPerEntry)
        {
            return DataResult<PalettedContainer<T>>.Error(
                () => $"Invalid bit count, calculated {bitsOnDisc}, but container declared {discData.BitsPerEntry}");
        }

        Palette<T> palette;
        BitStorage storage;
        if (storedConfiguration.BitsInMemory == 0)
        {
            palette = storedConfiguration.CreatePalette(strategy, paletteEntries);
            storage = new ZeroBitStorage(entryCount);
        }
        else
        {
            if (!discData.Storage.IsPresent)
            {
                return DataResult<PalettedContainer<T>>.Error(() => "Missing values for non-zero storage");
            }
            var data = discData.Storage.Get();
            try
            {
                if (discData.BitsPerEntry >= 0 && storedConfiguration is GlobalConfiguration)
                {
                    //Network global palette storage holds global ids directly without renumbering
                    palette = storedConfiguration.CreatePalette(strategy, paletteEntries);
                    storage = new SimpleBitStorage(storedConfiguration.BitsInMemory, entryCount, data);
                }
                else if (storedConfiguration.AlwaysRepack || storedConfiguration.BitsInMemory != bitsOnDisc)
                {
                    var oldPalette = new HashMapPalette<T>(bitsOnDisc, paletteEntries);
                    var oldStorage = new SimpleBitStorage(bitsOnDisc, entryCount, data);
                    var newPalette = storedConfiguration.CreatePalette(strategy, paletteEntries);
                    var newContents = ReencodeContents(oldStorage, oldPalette, newPalette);
                    palette = newPalette;
                    storage = new SimpleBitStorage(storedConfiguration.BitsInMemory, entryCount, newContents);
                }
                else
                {
                    palette = storedConfiguration.CreatePalette(strategy, paletteEntries);
                    storage = new SimpleBitStorage(storedConfiguration.BitsInMemory, entryCount, data);
                }
            }
            catch (Exception exception)
            {
                return DataResult<PalettedContainer<T>>.Error(() => $"Failed to read PalettedContainer: {exception.Message}");
            }
        }
        return DataResult<PalettedContainer<T>>.Success(new PalettedContainer<T>(strategy, storedConfiguration, storage, palette));
    }

    //Create the codec for PalettedContainer, maps to vanilla PalettedContainer.codecRW
    //Serializes PackedData with RecordCodecBuilder then converts via comapFlatMap
    public static Codec<PalettedContainer<T>> CreateCodec(Codec<T> elementCodec, Strategy<T> strategy, T defaultValue)
    {
        var packedCodec = RecordCodecBuilder.Of2<PackedData<T>, IReadOnlyList<T>, Optional<long[]>>(
            elementCodec.MapResult(defaultValue).ListOf().FieldOf("palette")
                .ForGetter((PackedData<T> d) => d.PaletteEntries),
            CodecExtras.LongArray.LenientOptionalFieldOf("data")
                .ForGetter((PackedData<T> d) => d.Storage),
            (palette, storage) => new PackedData<T>(palette, storage));
        return packedCodec.ComapFlatMap(
            discData => Unpack(strategy, discData),
            container => container.Pack(strategy));
    }

    //Internal data holding configuration, storage and palette, maps to vanilla Data
    private sealed class Data
    {
        //_flatPalette, flattened palette entries, built lazily once
        //Fetching an entry from a hash palette is a dictionary lookup; sending sections and saving fetch by id item by item, so flattening leaves only array indexing
        private T[]? _flatPalette;

        public Configuration Configuration { get; }
        public BitStorage Storage { get; }
        public Palette<T> Palette { get; }

        public Data(Configuration configuration, BitStorage storage, Palette<T> palette)
        {
            Configuration = configuration;
            Storage = storage;
            Palette = palette;
        }

        //FlatPalette, flattened palette entries with a length matching the palette entry count
        public T[] FlatPalette
        {
            get
            {
                var cached = _flatPalette;
                if (cached is not null && cached.Length == Palette.Size) return cached;
                var values = new T[Palette.Size];
                for (var i = 0; i < values.Length; i++) values[i] = Palette.ValueFor(i);
                _flatPalette = values;
                return values;
            }
        }

        public void CopyFrom(Palette<T> oldPalette, BitStorage oldStorage)
        {
            var dummyResizer = PaletteResize<T>.NoResizeExpected();
            for (var i = 0; i < oldStorage.Size; i++)
            {
                var value = oldPalette.ValueFor(oldStorage.Get(i));
                Storage.Set(i, Palette.IdFor(value, dummyResizer));
            }
            //The code above may have added entries to this palette, invalidating the flatten cache
            _flatPalette = null;
        }

        public Data Copy() => new(Configuration, Storage.Copy(), Palette.Copy());
    }
}

//PackedData, maps to vanilla PalettedContainerRO.PackedData
//NBT serialization intermediate form: paletteEntries value list, storage packed long array, bitsPerEntry bit width
public sealed record PackedData<T>(IReadOnlyList<T> PaletteEntries, Optional<long[]> Storage, int BitsPerEntry)
{
    public const int UnknownBitsPerEntry = -1;

    public PackedData(IReadOnlyList<T> paletteEntries, Optional<long[]> storage) : this(paletteEntries, storage, UnknownBitsPerEntry) { }
}

//NetworkData, a network serialization view of runtime palette entries and raw storage
//With a global palette, PaletteEntries is empty and Storage is the global id
public sealed record NetworkData<T>(int Bits, IReadOnlyList<T> PaletteEntries, long[] RawStorage);
