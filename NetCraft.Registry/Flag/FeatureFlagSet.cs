namespace NetCraft.Registry.Flag;

//特性开关集合对应原版FeatureFlagSet
//一个long当位图，空集合的宇宙为null
public sealed class FeatureFlagSet
{
    private static readonly FeatureFlagSet Empty = new(null, 0L);

    //掩码只有64位，一个宇宙最多放64个开关
    public const int MaxContainerSize = 64;

    private readonly FeatureFlagUniverse? _universe;

    private readonly long _mask;

    private FeatureFlagSet(FeatureFlagUniverse? universe, long mask)
    {
        _universe = universe;
        _mask = mask;
    }

    //按开关列表建集合，列表为空给空集合
    internal static FeatureFlagSet Create(FeatureFlagUniverse universe, IReadOnlyCollection<FeatureFlag> flags)
        => flags.Count == 0 ? Empty : new FeatureFlagSet(universe, ComputeMask(universe, 0L, flags));

    public static FeatureFlagSet Of() => Empty;

    public static FeatureFlagSet Of(FeatureFlag flag) => new(flag.Universe, flag.Mask);

    public static FeatureFlagSet Of(FeatureFlag flag, params FeatureFlag[] flags)
    {
        var mask = flags.Length == 0 ? flag.Mask : ComputeMask(flag.Universe, flag.Mask, flags);
        return new FeatureFlagSet(flag.Universe, mask);
    }

    //把同一个宇宙的开关掩码或到一起，混了别的宇宙直接报错
    private static long ComputeMask(FeatureFlagUniverse universe, long mask, IEnumerable<FeatureFlag> flags)
    {
        foreach (var flag in flags)
        {
            if (!ReferenceEquals(universe, flag.Universe))
                throw new InvalidOperationException($"Mismatched feature universe, expected '{universe}', but got '{flag.Universe}'");
            mask |= flag.Mask;
        }
        return mask;
    }

    public bool Contains(FeatureFlag flag)
        => ReferenceEquals(_universe, flag.Universe) && (_mask & flag.Mask) != 0L;

    public bool IsEmpty() => Equals(Empty);

    //本集合里的位对方全都有
    public bool IsSubsetOf(FeatureFlagSet set)
    {
        if (_universe is null) return true;
        if (!ReferenceEquals(_universe, set._universe)) return false;
        return (_mask & ~set._mask) == 0L;
    }

    public bool Intersects(FeatureFlagSet set)
    {
        if (_universe is null || set._universe is null || !ReferenceEquals(_universe, set._universe)) return false;
        return (_mask & set._mask) != 0L;
    }

    public FeatureFlagSet Join(FeatureFlagSet other)
    {
        if (_universe is null) return other;
        if (other._universe is null) return this;
        if (!ReferenceEquals(_universe, other._universe))
            throw new ArgumentException($"Mismatched set elements: '{_universe}' != '{other._universe}'");
        return new FeatureFlagSet(_universe, _mask | other._mask);
    }

    public FeatureFlagSet Subtract(FeatureFlagSet other)
    {
        if (_universe is null || other._universe is null) return this;
        if (!ReferenceEquals(_universe, other._universe))
            throw new ArgumentException($"Mismatched set elements: '{_universe}' != '{other._universe}'");
        var mask = _mask & ~other._mask;
        return mask == 0L ? Empty : new FeatureFlagSet(_universe, mask);
    }

    public override bool Equals(object? obj)
        => obj is FeatureFlagSet other && ReferenceEquals(_universe, other._universe) && _mask == other._mask;

    public override int GetHashCode() => _mask.GetHashCode();
}
