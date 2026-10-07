using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.Features;

//NoOpFeature no-op feature, maps to vanilla NoOpFeature
//Places nothing; registered so the Feature chain has a minimal end-to-end implementation to validate
public sealed class NoOpFeature : Feature<NoneFeatureConfiguration>
{
    private const string FeatureId = "no_op";

    //Instance singleton, matching minecraft:no_op in the FEATURE registry
    public static readonly NoOpFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new NoOpFeature());

    private NoOpFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), NoneFeatureConfiguration.Codec) { }

    protected override bool Place(NoneFeatureConfiguration config, FeaturePlaceContext context) => false;
}

//FeatureBootstrap feature registration entry
//The registry must be populated before data loading, otherwise the type field of configured_feature cannot be decoded at all
public static class FeatureBootstrap
{
    public static void RegisterAll()
    {
        _ = NoOpFeature.Instance;
        //Required features and block state providers are registered together by the batch under Impl; a single touch triggers static registration
        Impl.RequiredFeatures.RegisterAll();
        //The tree system: trunk/foliage/root placers and decorators are separate registries; one missing tree JSON breaks the whole decode
        Impl.Trees.TreeBootstrap.RegisterAll();
        Impl.Vegetation.VegetationBootstrap.RegisterAll();
        Impl.Nether.NetherBootstrap.RegisterAll();
        Impl.Misc.MiscBootstrap.RegisterAll();
        //Noise state providers are also part of BLOCKSTATE_PROVIDER_TYPE; features like flowers that pick state by noise rely on it
        Impl.NoiseProviderBootstrap.RegisterAll();
    }
}
