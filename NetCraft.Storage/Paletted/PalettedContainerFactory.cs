using NetCraft.Codec;
using NetCraft.Nbt;
using NetCraft.Registry;
using NetCraft.Registry.Codec;
using NetCraft.Registry.State;

namespace NetCraft.Storage.Paletted;

//PalettedContainerFactory abstract class, maps to vanilla net.minecraft.world.level.chunk.PalettedContainerFactory
//Provides creation and codecs for block state and biome containers
public abstract class PalettedContainerFactory
{
    public abstract PalettedContainer<BlockState> CreateForBlockStates();
    public abstract PalettedContainer<Holder<Biome>> CreateForBiomes();
    public abstract Codec<PalettedContainer<BlockState>> BlockStatesContainerCodec();
    public abstract Codec<PalettedContainer<Holder<Biome>>> BiomeContainerCodec();

    //Deserialize PackedData into a PalettedContainer, maps to vanilla read on the network serialization path
    //LevelChunkSerializer.Read uses this to rebuild section.States and section.Biomes
    public abstract PalettedContainer<BlockState> UnpackBlockStates(PackedData<BlockState> discData);
    public abstract PalettedContainer<Holder<Biome>> UnpackBiomes(PackedData<Holder<Biome>> discData);

    //Look up a BlockState by Identifier, used to rebuild palette entries during network deserialization
    //LevelChunkSerializer.ReadBlockState calls this first and falls back to BuiltInRegistries.BLOCK
    public abstract BlockState? LookupBlockState(Identifier id);
    public abstract Holder<Biome>? LookupBiome(Identifier id);

    //Default, the concrete instance, so callers can register blocks and biomes
    public static readonly DefaultPalettedContainerFactory Default = new();
}

//Default factory implementation using IdMapper as the GlobalMap registry to look up Block and Holder<Biome> by Identifier
public sealed class DefaultPalettedContainerFactory : PalettedContainerFactory
{
    //GlobalMap registers on demand, so tests can run without entering GlobalPalette
    private readonly IdMapper<BlockState> _blockStatesGlobalMap = new();
    private readonly IdMapper<Holder<Biome>> _biomesGlobalMap = new();
    private readonly Strategy<BlockState> _blockStatesStrategy;
    private readonly Strategy<Holder<Biome>> _biomesStrategy;
    private readonly Codec<BlockState> _blockStateCodec;
    private readonly Codec<Holder<Biome>> _biomeCodec;

    //Look up Block and Holder<Biome> by Identifier, used by codec deserialization
    private readonly Dictionary<Identifier, Block> _blocksById = new();
    private readonly Dictionary<Identifier, Holder<Biome>> _biomesById = new();

    //The default value is set by the most recent RegisterBlock/RegisterBiome and used as the initial value of an empty container
    private BlockState? _defaultBlockState;
    private Holder<Biome>? _defaultBiome;

    public DefaultPalettedContainerFactory()
    {
        _blockStatesStrategy = Strategy<BlockState>.CreateForBlockStates(_blockStatesGlobalMap);
        _biomesStrategy = Strategy<Holder<Biome>>.CreateForBiomes(_biomesGlobalMap);

        //The BlockState codec does full state encode/decode (Name + Properties)
        //First checks this factory's registered block table (test MockBlocks exist only here), then falls back to the built-in registry
        //The earlier simplified version writing only block names dropped properties like facing, so a chunk unloaded and reloaded reverted to the default state
        _blockStateCodec = new BlockStateCodec(id => _blocksById.GetValueOrDefault(id));

        //Biome codec encodes by Value.Id and decodes by looking up Holder<Biome> by Identifier
        _biomeCodec = IdentifierCodec.Instance.ComapFlatMap(
            id => _biomesById.TryGetValue(id, out var holder)
                ? DataResult<Holder<Biome>>.Success(holder)
                : DataResult<Holder<Biome>>.Error(() => $"Unknown biome: {id}"),
            holder => holder.Value.Id);
    }

    //RegisterBlock builds the codec reverse table by Identifier and adds all states to GlobalMap plus the default cache
    //The network palette global id must match the vanilla runtime id, so all BlockState.Id values are registered rather than only the default state
    //The default is set only on the first registration and later ones do not overwrite it, aligning with vanilla AIR as the default block
    public void RegisterBlock(Block block)
    {
        _blocksById[block.Id] = block;
        foreach (var state in block.AllStates)
            _blockStatesGlobalMap.Add(state, state.Id);
        _defaultBlockState ??= block.DefaultBlockState;
    }

    //RegisterBiome builds the codec reverse table by Identifier and adds to GlobalMap plus the default cache
    //The default is set only on the first registration and later ones do not overwrite it
    public void RegisterBiome(Holder<Biome> holder)
    {
        _biomesById[holder.Value.Id] = holder;
        _biomesGlobalMap.Add(holder);
        _defaultBiome ??= holder;
    }

    public override PalettedContainer<BlockState> CreateForBlockStates()
        => new(_defaultBlockState ?? default, _blockStatesStrategy);

    public override PalettedContainer<Holder<Biome>> CreateForBiomes()
        => new(_defaultBiome ?? default!, _biomesStrategy);

    public override Codec<PalettedContainer<BlockState>> BlockStatesContainerCodec()
        => PalettedContainer<BlockState>.CreateCodec(_blockStateCodec, _blockStatesStrategy, _defaultBlockState.Value);

    public override Codec<PalettedContainer<Holder<Biome>>> BiomeContainerCodec()
        => PalettedContainer<Holder<Biome>>.CreateCodec(_biomeCodec, _biomesStrategy, _defaultBiome!);

    //UnpackBlockStates deserializes PackedData into a container using the strategy
    //Falls back to an empty container on failure, avoiding an exception that would break network reads
    public override PalettedContainer<BlockState> UnpackBlockStates(PackedData<BlockState> discData)
        => PalettedContainer<BlockState>.Unpack(_blockStatesStrategy, discData).Result()
            .OrElse(CreateForBlockStates());

    public override PalettedContainer<Holder<Biome>> UnpackBiomes(PackedData<Holder<Biome>> discData)
        => PalettedContainer<Holder<Biome>>.Unpack(_biomesStrategy, discData).Result()
            .OrElse(CreateForBiomes());

    //LookupBlockState looks up _blocksById by Identifier and returns the matching DefaultBlockState
    //Returns null when absent; the caller falls back to BuiltInRegistries.BLOCK
    public override BlockState? LookupBlockState(Identifier id)
        => _blocksById.TryGetValue(id, out var block) ? block.DefaultBlockState : null;

    //LookupBiome looks up _biomesById by Identifier and returns the matching Holder
    //Returns null when absent; the caller falls back to the Biome.Plains placeholder
    public override Holder<Biome>? LookupBiome(Identifier id)
        => _biomesById.TryGetValue(id, out var holder) ? holder : null;
}
