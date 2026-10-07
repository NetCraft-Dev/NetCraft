using NetCraft.Primitives;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//PoolAliasLookup pool alias lookup table, maps to vanilla net.minecraft.world.level.levelgen.structure.pools.alias.PoolAliasLookup
//Resolves a fixed alias mapping once per generation; every pool name lookup afterwards goes through it
public sealed class PoolAliasLookup
{
    //Empty lookup table that performs no substitution, maps to vanilla EMPTY
    public static readonly PoolAliasLookup Empty = new(new Dictionary<Identifier, Identifier>());

    private readonly IReadOnlyDictionary<Identifier, Identifier> _mappings;

    private PoolAliasLookup(IReadOnlyDictionary<Identifier, Identifier> mappings) => _mappings = mappings;

    //Create resolves an alias mapping from the seed and generation position, maps to vanilla create
    //The random source is forked from the seed and positioned at the generation point, so the same point resolves the same mapping
    public static PoolAliasLookup Create(IReadOnlyList<PoolAliasBinding> bindings, BlockPos pos, long seed)
    {
        if (bindings.Count == 0) return Empty;
        var random = RandomSource.Create(seed).ForkPositional().At(pos.X, pos.Y, pos.Z);
        var mappings = new Dictionary<Identifier, Identifier>();
        foreach (var binding in bindings)
            binding.ForEachResolved(random, (alias, target) => mappings[alias] = target);
        return new PoolAliasLookup(mappings);
    }

    //Lookup returns the pool name for an alias, falling back to the original pool name when absent
    public Identifier Lookup(Identifier poolId)
        => _mappings.TryGetValue(poolId, out var target) ? target : poolId;
}
