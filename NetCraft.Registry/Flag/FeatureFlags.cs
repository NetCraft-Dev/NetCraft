using NetCraft.Codec;

namespace NetCraft.Registry.Flag;

//Built-in feature flags, maps to vanilla FeatureFlags
public static class FeatureFlags
{
    public static readonly FeatureFlag VANILLA;

    public static readonly FeatureFlag TRADE_REBALANCE;

    public static readonly FeatureFlag REDSTONE_EXPERIMENTS;

    public static readonly FeatureFlag MINECART_IMPROVEMENTS;

    public static readonly FeatureFlagRegistry REGISTRY;

    public static readonly Codec<FeatureFlagSet> CODEC;

    public static readonly FeatureFlagSet VANILLA_SET;

    public static readonly FeatureFlagSet DEFAULT_FLAGS;

    static FeatureFlags()
    {
        var builder = new FeatureFlagRegistry.Builder("main");
        VANILLA = builder.CreateVanilla("vanilla");
        TRADE_REBALANCE = builder.CreateVanilla("trade_rebalance");
        REDSTONE_EXPERIMENTS = builder.CreateVanilla("redstone_experiments");
        MINECART_IMPROVEMENTS = builder.CreateVanilla("minecart_improvements");
        REGISTRY = builder.Build();
        CODEC = REGISTRY.Codec();
        VANILLA_SET = FeatureFlagSet.Of(VANILLA);
        DEFAULT_FLAGS = VANILLA_SET;
    }

    //Names present in the requested set but not the allowed set, comma-separated
    public static string PrintMissingFlags(FeatureFlagSet allowedFlags, FeatureFlagSet requestedFlags)
        => PrintMissingFlags(REGISTRY, allowedFlags, requestedFlags);

    public static string PrintMissingFlags(FeatureFlagRegistry registry, FeatureFlagSet allowedFlags, FeatureFlagSet requestedFlags)
    {
        var requested = registry.ToNames(requestedFlags);
        var allowed = registry.ToNames(allowedFlags);
        return string.Join(", ", requested.Where(id => !allowed.Contains(id)).Select(id => id.ToString()));
    }

    //Anything beyond the vanilla flag set is experimental
    public static bool IsExperimental(FeatureFlagSet features) => !features.IsSubsetOf(VANILLA_SET);
}
