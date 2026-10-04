namespace NetCraft.Registry.Flag;

//特性开关对应原版FeatureFlag
//掩码是位图里的一位，只在同一个宇宙内有意义
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
