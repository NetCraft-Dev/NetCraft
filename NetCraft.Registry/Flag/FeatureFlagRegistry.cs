using NetCraft.Codec;
using NetCraft.Logging;
using NetCraft.Registry.Codec;

namespace NetCraft.Registry.Flag;

//特性开关注册表对应原版FeatureFlagRegistry
//把Identifier映射到开关，并给出全量集合
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

    //名字认不出来的按警告放过
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

    //名字列表与集合互转，名字认不出来算解码失败
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

    //注册表构造器对应原版FeatureFlagRegistry.Builder
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
