namespace NetCraft.Registry.Flag;

//Feature flag, maps to vanilla FeatureFlag
//The mask is one bit in a bitmap and is only meaningful within the same universe
public sealed class FeatureFlag
{
    internal readonly FeatureFlagUniverse Universe;

    internal readonly long Mask;

    internal FeatureFlag(FeatureFlagUniverse universe, int bit)
    {
        Universe = universe;
        Mask = 1L << bit;
    }
}
