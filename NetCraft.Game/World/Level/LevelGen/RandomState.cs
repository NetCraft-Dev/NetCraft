using System.Collections.Concurrent;
using NetCraft.Game.World.Level.LevelGen.Synth;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen;

//RandomState random state bridging the seed and NoiseRouter, maps to vanilla net.minecraft.world.level.levelgen.RandomState
//At construction it mapAll's the density tree, replacing every NoiseHolder with an instantiated NormalNoise and injecting BlendedNoise into the terrain random source
//Exposes router/sampler/aquiferRandom/oreRandom/getOrCreateNoise for NoiseChunk/Aquifer
public sealed class RandomState
{
    private readonly PositionalRandomFactory _random;
    private readonly Registry<NoiseParameters> _noises;
    //Seed the raw world seed; the surface stage derives biome blend distance from it, matching the seed vanilla stores
    public long Seed { get; }
    public NoiseRouter Router { get; }
    public Climate.Sampler Sampler { get; }
    //SurfaceSystem world-level singleton, built at construction like vanilla and used directly by the surface stage
    public SurfaceSystem SurfaceSystem { get; }
    private readonly PositionalRandomFactory _aquiferRandom;
    private readonly PositionalRandomFactory _oreRandom;
    //Noise instance and random factory caches, maps to vanilla ConcurrentHashMap
    //Chunk generation is spread over several threads, so the same key is accessed concurrently
    private readonly ConcurrentDictionary<ResourceKey<NoiseParameters>, NormalNoise> _noiseInstances = new();
    private readonly ConcurrentDictionary<Identifier, PositionalRandomFactory> _positionalRandoms = new();

    //Create factory, maps to vanilla create
    public static RandomState Create(NoiseGeneratorSettings settings, Registry<NoiseParameters> noises, long seed)
        => new(settings, noises, seed);

    private RandomState(NoiseGeneratorSettings settings, Registry<NoiseParameters> noises, long seed)
    {
        Seed = seed;
        _random = RandomSource.Create(seed).ForkPositional();
        _noises = noises;
        _aquiferRandom = _random.FromHashOf("aquifer").ForkPositional();
        _oreRandom = _random.FromHashOf("ore").ForkPositional();
        SurfaceSystem = new SurfaceSystem(this, settings.DefaultBlock, settings.SeaLevel, _random);

        var wiringHelper = new NoiseWiringHelper(this, settings.UseLegacyRandomSource, seed);
        Router = settings.NoiseRouter.MapAll(wiringHelper);

        //noiseFlattener expands the remaining HolderHolder and Marker nodes for Climate.Sampler
        var flattener = new NoiseFlattener();
        Sampler = new Climate.NoiseRouterSampler(Router);
        _ = flattener;
    }

    //GetOrCreateNoise looks up or builds a NormalNoise by key, maps to vanilla getOrCreateNoise
    //The cache avoids re-instantiation and guarantees the same key returns the same NormalNoise instance
    //Vanilla's noise cache is a ConcurrentHashMap + computeIfAbsent; chunk generation runs on several threads so this must be thread-safe
    public NormalNoise GetOrCreateNoise(ResourceKey<NoiseParameters> key)
        => _noiseInstances.GetOrAdd(key, k => Noises.Instantiate(_noises, _random, k));

    //GetOrCreateRandomFactory looks up or derives a PositionalRandomFactory by name, maps to vanilla getOrCreateRandomFactory
    public PositionalRandomFactory GetOrCreateRandomFactory(Identifier name)
        => _positionalRandoms.GetOrAdd(name, n => _random.FromHashOf(n.ToString()).ForkPositional());

    public PositionalRandomFactory AquiferRandom => _aquiferRandom;
    public PositionalRandomFactory OreRandom => _oreRandom;
}

//NoiseWiringHelper NoiseHolder/BlendedNoise wiring visitor, maps to the anonymous Visitor inside vanilla RandomState
//visitNoise replaces NoiseHolder with an instantiated NormalNoise (TEMPERATURE_NETHER/VEGETATION_NETHER go through the legacy path)
//apply injects a new random source into BlendedNoise and replaces EndIslandDensityFunction with a new seed-carrying instance
internal sealed class NoiseWiringHelper : Visitor
{
    private readonly RandomState _owner;
    private readonly bool _useLegacyInit;
    private readonly long _seed;
    private readonly Dictionary<DensityFunction, DensityFunction> _wrapped = new();

    public NoiseWiringHelper(RandomState owner, bool useLegacyInit, long seed)
    {
        _owner = owner;
        _useLegacyInit = useLegacyInit;
        _seed = seed;
    }

    //NewLegacyInstance derives a LegacyRandomSource from seedOffset, maps to vanilla newLegacyInstance
    private RandomSource NewLegacyInstance(long seedOffset) => new LegacyRandomSource(_seed + seedOffset);

    public NoiseHolder VisitNoise(NoiseHolder noise)
    {
        var noiseData = noise.NoiseData;
        if (noiseData is null) return noise;
        //NetCraft has no ResourceKey comparison for HolderData to detect the NETHER path and relies on NormalNoise.CreateLegacyNetherBiome for the Nether
        //Vanilla's is(Noises.TEMPERATURE_NETHER) takes the LegacyNetherBiome path; here everything goes through standard instantiation
        var instantiated = _owner.GetOrCreateNoise(GetKeyForData(noiseData));
        return new NoiseHolder(noiseData, instantiated);
    }

    //GetKeyForData looks up the ResourceKey from NoiseParameters, maps to vanilla noiseData.unwrapKey().orElseThrow
    //NetCraft's BuiltInRegistries.NOISE provides GetKey for the reverse lookup
    private ResourceKey<NoiseParameters> GetKeyForData(NoiseParameters data)
    {
        foreach (var key in BuiltInRegistries.NOISE.RegistryKeySet)
        {
            if (BuiltInRegistries.NOISE.GetValue(key) == data)
                return key;
        }
        throw new InvalidOperationException("NoiseParameters not registered");
    }

    public DensityFunction Apply(DensityFunction input)
    {
        if (_wrapped.TryGetValue(input, out var cached)) return cached;
        var result = WrapNew(input);
        _wrapped[input] = result;
        return result;
    }

    //WrapNew node replacement logic, maps to vanilla wrapNew
    private DensityFunction WrapNew(DensityFunction function)
    {
        if (function is BlendedNoise blended)
        {
            var terrainRandom = _useLegacyInit
                ? NewLegacyInstance(0L)
                : _owner.GetOrCreateRandomFactory(Identifier.WithDefaultNamespace("terrain")).FromSeed(0L);
            return blended.WithNewRandom(terrainRandom);
        }
        if (function is EndIslandDensityFunction)
        {
            return new EndIslandDensityFunction(_seed);
        }
        return function;
    }
}

//NoiseFlattener HolderHolder/Marker expander, maps to the second anonymous Visitor in vanilla RandomState
//Expands HolderHolder into the inner function and Marker into the inner wrapped, saving call depth in Climate.Sampler
internal sealed class NoiseFlattener : Visitor
{
    private readonly Dictionary<DensityFunction, DensityFunction> _wrapped = new();

    public NoiseHolder VisitNoise(NoiseHolder noise) => noise;

    public DensityFunction Apply(DensityFunction input)
    {
        if (_wrapped.TryGetValue(input, out var cached)) return cached;
        var result = WrapNew(input);
        _wrapped[input] = result;
        return result;
    }

    private static DensityFunction WrapNew(DensityFunction function)
    {
        if (function is HolderHolder holder)
            return holder.Function;
        if (function is MarkerNode marker)
            return marker.Wrapped;
        return function;
    }
}
