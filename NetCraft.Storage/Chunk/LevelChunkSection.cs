using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage.Paletted;

namespace NetCraft.Storage.Chunk;

//Chunk section, maps to vanilla net.minecraft.world.level.chunk.LevelChunkSection
//Holds the states block state palette and the biomes biome palette
//The four counts are computed in a single scan, maps to vanilla recalcBlockCounts and Paper's countEntries
public sealed class LevelChunkSection
{
    public const int BiomeContainerBits = 2;

    private short _nonEmptyBlockCount;
    private short _fluidCount;
    private short _tickingBlockCount;
    //Ticking fluid count; always 0 until the fluid tick subsystem is wired in, maps to the same-named vanilla field
    private short _tickingFluidCount;

    public PalettedContainer<BlockState> States { get; }
    public PalettedContainer<Holder<Biome>> Biomes { get; private set; }

    public LevelChunkSection(PalettedContainer<BlockState> states, PalettedContainer<Holder<Biome>> biomes)
    {
        States = states;
        Biomes = biomes;
        RecountStates();
    }

    public LevelChunkSection(Func<PalettedContainer<BlockState>> statesFactory, Func<PalettedContainer<Holder<Biome>>> biomesFactory)
    {
        States = statesFactory();
        Biomes = biomesFactory();
    }

    //RecountStates recomputes the section counts from the container's actual content
    //When restoring a section from a save or copying it, counts do not come with the container; without a recount HasOnlyAir would misjudge a section with blocks as all air
    //The consequence is that the section is written as null on disk, losing block data, and lighting treats it as an empty section
    //Air is decided by Block.IsAir, not by some coordinate's state as a baseline; on load that coordinate might be stone
    private void RecountStates()
    {
        _nonEmptyBlockCount = 0;
        _fluidCount = 0;
        _tickingBlockCount = 0;
        _tickingFluidCount = 0;
        States.Count((state, count) =>
        {
            if (!state.Owner.IsAir) _nonEmptyBlockCount += (short)count;
            if (!state.FluidState.IsEmpty) _fluidCount += (short)count;
            if (state.Owner.RandomTicks) _tickingBlockCount += (short)count;
        });
    }

    public BlockState GetBlockState(int sectionX, int sectionY, int sectionZ)
        => States.Get(sectionX, sectionY, sectionZ);

    //SetBlockState writes a block and maintains the section counts, maps to vanilla setBlockState
    //Each count changes only when old and new states differ on the matching check; equivalent to the recount pass
    public BlockState SetBlockState(int sectionX, int sectionY, int sectionZ, BlockState state)
    {
        var old = States.GetAndSet(sectionX, sectionY, sectionZ, state);
        //Each of the three checks is read once, avoiding evaluating the same property access twice in the condition and the delta
        var oldAir = old.Owner.IsAir;
        var newAir = state.Owner.IsAir;
        if (oldAir != newAir)
            _nonEmptyBlockCount = (short)Math.Max(0, _nonEmptyBlockCount + (newAir ? -1 : 1));

        var oldEmpty = old.FluidState.IsEmpty;
        var newEmpty = state.FluidState.IsEmpty;
        if (oldEmpty != newEmpty)
            _fluidCount = (short)Math.Max(0, _fluidCount + (newEmpty ? -1 : 1));

        var oldTicks = old.Owner.RandomTicks;
        var newTicks = state.Owner.RandomTicks;
        if (oldTicks != newTicks)
            _tickingBlockCount = (short)Math.Max(0, _tickingBlockCount + (newTicks ? 1 : -1));

        return old;
    }

    //NonEmptyBlockCount, non-empty block count, written by network serialization
    //The vanilla client's LevelChunk.getBlockState checks the section's hasOnlyAir first; writing 0 would make the whole section count as air
    //The result is that the client thinks the world is all air: the player falls and the section does not render
    public short NonEmptyBlockCount => _nonEmptyBlockCount;

    //FluidCount, fluid count, written by network serialization
    public short FluidCount => _fluidCount;

    public bool HasOnlyAir() => _nonEmptyBlockCount == 0;

    public bool HasFluid() => _fluidCount > 0;

    public bool IsRandomlyTicking() => _tickingBlockCount > 0 || _tickingFluidCount > 0;

    public PalettedContainer<Holder<Biome>> GetBiomes() => Biomes;

    public LevelChunkSection Copy()
        => new(States.Copy(), (PalettedContainer<Holder<Biome>>)Biomes.Copy());

    public bool MaybeHas(Predicate<BlockState> predicate) => States.MaybeHas(predicate);

    public Holder<Biome> GetNoiseBiome(int quartX, int quartY, int quartZ)
        => Biomes.Get(quartX, quartY, quartZ);

    //SetBiome writes a biome by quart coords within the section, maps to vanilla setBiome
    //quart coords range 0-3; every 4 blocks share one biome
    public void SetBiome(int quartX, int quartY, int quartZ, Holder<Biome> biome)
        => Biomes.Set(quartX, quartY, quartZ, biome);
}
