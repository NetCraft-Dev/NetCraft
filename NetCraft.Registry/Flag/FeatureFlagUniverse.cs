namespace NetCraft.Registry.Flag;

//特性开关宇宙对应原版FeatureFlagUniverse
//不同宇宙的掩码不能混用，同一个宇宙最多64个开关
public sealed class FeatureFlagUniverse
{
    private readonly string _id;

    public FeatureFlagUniverse(string id) => _id = id;

    public override string ToString() => _id;
}
