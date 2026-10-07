using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen;

//MultiNoiseBiomeSource multi-noise biome source, maps to vanilla net.minecraft.world.level.biome.MultiNoiseBiomeSource
//Phase E wires in Climate.Sampler + MultiNoiseBiomeSourceParameterList for real derivation
//GetBiome samples 6-dimensional parameters by coordinate and finds the biome closest in parameter space
public sealed class MultiNoiseBiomeSource : BiomeSource
{
    public MultiNoiseBiomeSourceParameterList? ParameterList { get; }
    public Climate.Sampler? Sampler { get; }

    public MultiNoiseBiomeSource(MultiNoiseBiomeSourceParameterList parameterList, Climate.Sampler sampler)
    {
        ParameterList = parameterList;
        Sampler = sampler;
    }

    //Single-parameter constructor reserved for data-driven assembly; the sampler is injected by NoiseBasedChunkGenerator from the noise router
    public MultiNoiseBiomeSource(MultiNoiseBiomeSourceParameterList parameterList)
    {
        ParameterList = parameterList;
    }

    //LegacyConstructor placeholder with no parameter list and no sampler, returns the plains biome
    //Used before Bootstrap or in test scenarios
    public MultiNoiseBiomeSource() { }

    //PossibleBiomes distinct biomes seen in the parameter table; only the plains placeholder without a table
    //Unbound entries are skipped, they point at biomes not loaded yet
    public IReadOnlyList<Biome> PossibleBiomes
    {
        get
        {
            if (ParameterList is null) return new[] { Biome.Plains };
            var seen = new HashSet<Biome>(ReferenceEqualityComparer.Instance);
            var result = new List<Biome>();
            foreach (var (_, holder) in ParameterList.Entries)
            {
                if (!holder.IsBound()) continue;
                if (seen.Add(holder.Value)) result.Add(holder.Value);
            }
            return result.Count == 0 ? new[] { Biome.Plains } : result;
        }
    }

    //GetBiome samples 6-dimensional parameters by coordinate and finds the biome closest in parameter space
    //Without a ParameterList/Sampler it returns the Biome.Plains singleton as a placeholder to avoid palette blow-up
    public Biome GetBiome(int x, int y, int z)
    {
        if (ParameterList is null || Sampler is null)
            return Biome.Plains;

        var target = new Climate.ParameterPoint(
            Sampler.Temperature(x, y, z),
            Sampler.Humidity(x, y, z),
            Sampler.Continentalness(x, y, z),
            Sampler.Erosion(x, y, z),
            Sampler.Depth(x, y, z),
            Sampler.Weirdness(x, y, z),
            0L);
        return ParameterList.FindClosest(target).Value;
    }
}
