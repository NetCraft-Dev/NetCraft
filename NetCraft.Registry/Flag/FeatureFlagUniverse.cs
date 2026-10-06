namespace NetCraft.Registry.Flag;

//Feature flag universe, maps to vanilla FeatureFlagUniverse
//Masks from different universes must not be mixed; one universe holds at most 64 flags
public sealed class FeatureFlagUniverse
{
    private readonly string _id;

    public FeatureFlagUniverse(string id) => _id = id;

    public override string ToString() => _id;
}
