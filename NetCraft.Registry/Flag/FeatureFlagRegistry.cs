using NetCraft.Codec;
using NetCraft.Logging;
using NetCraft.Registry.Codec;

namespace NetCraft.Registry.Flag;

//Feature flag registry, maps to vanilla FeatureFlagRegistry
//Maps an Identifier to a flag and provides the full set
public sealed class FeatureFlagRegistry
{
    private readonly FeatureFlagUniverse _universe;

    private readonly IReadOnlyDictionary<Identifier, FeatureFlag> _names;

    private readonly FeatureFlagSet _allFlags;

    private FeatureFlagRegistry(FeatureFlagUniverse universe, FeatureFlagSet allFlags, IReadOnlyDictionary<Identifier, FeatureFlag> names)
    {
        _universe = universe;
        _names = names;
        _allFlags = allFlags;
    }

    public bool IsSubset(FeatureFlagSet set) => set.IsSubsetOf(_allFlags);

    public FeatureFlagSet AllFlags() => _allFlags;

    //Unknown names are let through with a warning
    public FeatureFlagSet FromNames(IEnumerable<Identifier> flagIds)
        => FromNames(flagIds, flagId => Log.Warning($"Unknown feature flag: {flagId}"));

    public FeatureFlagSet Subset(params FeatureFlag[] flags) => FeatureFlagSet.Create(_universe, flags);

    public FeatureFlagSet FromNames(IEnumerable<Identifier> flagIds, Action<Identifier> unknownFlags)
    {
        var flags = new List<FeatureFlag>();
        var seen = new HashSet<FeatureFlag>();
        foreach (var flagId in flagIds)
        {
            if (!_names.TryGetValue(flagId, out var flag))
            {
                unknownFlags(flagId);
                continue;
            }
            if (seen.Add(flag)) flags.Add(flag);
        }
        return FeatureFlagSet.Create(_universe, flags);
    }

    public IReadOnlySet<Identifier> ToNames(FeatureFlagSet set)
    {
        var result = new HashSet<Identifier>();
        foreach (var (id, flag) in _names)
            if (set.Contains(flag))
                result.Add(id);
        return result;
    }

    //Converts between a name list and a set; unknown names count as a decode failure
    public Codec<FeatureFlagSet> Codec()
        => IdentifierCodec.Instance.ListOf().ComapFlatMap(
            ids =>
            {
                var unknown = new List<Identifier>();
                var result = FromNames(ids, unknown.Add);
                return unknown.Count > 0
                    ? DataResult<FeatureFlagSet>.Error(() => $"Unknown feature ids: {string.Join(", ", unknown)}", result)
                    : DataResult<FeatureFlagSet>.Success(result);
            },
            set => [.. ToNames(set)]);

    //Registry builder, maps to vanilla FeatureFlagRegistry.Builder
    public sealed class Builder
    {
        private readonly FeatureFlagUniverse _universe;

        private readonly Dictionary<Identifier, FeatureFlag> _flags = new();

        private int _id;

        public Builder(string universeId) => _universe = new FeatureFlagUniverse(universeId);

        public FeatureFlag CreateVanilla(string name) => Create(Identifier.WithDefaultNamespace(name));

        public FeatureFlag Create(Identifier name)
        {
            if (_id >= FeatureFlagSet.MaxContainerSize)
                throw new InvalidOperationException("Too many feature flags");
            var flag = new FeatureFlag(_universe, _id++);
            if (_flags.ContainsKey(name))
                throw new InvalidOperationException($"Duplicate feature flag {name}");
            _flags[name] = flag;
            return flag;
        }

        public FeatureFlagRegistry Build()
            => new(_universe, FeatureFlagSet.Create(_universe, _flags.Values.ToList()), new Dictionary<Identifier, FeatureFlag>(_flags));
    }
}
