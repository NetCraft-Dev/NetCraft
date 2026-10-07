namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Nether;

//NetherBootstrap nether and end feature registration entry
//Touching each static Instance triggers static registration; called by FeatureBootstrap
public static class NetherBootstrap
{
    public static void RegisterAll()
    {
        _ = HugeFungusFeature.Instance;
        _ = NetherForestVegetationFeature.Instance;
        _ = TwistingVinesFeature.Instance;
        _ = WeepingVinesFeature.Instance;
        _ = BasaltColumnsFeature.Instance;
        _ = BasaltPillarFeature.Instance;
        _ = NetherrackReplaceBlobsFeature.Instance;
        _ = DeltaFeature.Instance;
        _ = EndIslandFeature.Instance;
        _ = EndSpikeFeature.Instance;
        _ = EndPlatformFeature.Instance;
        _ = EndGatewayFeature.Instance;
        _ = ChorusPlantFeature.Instance;
        _ = SpikeFeature.Instance;
        _ = VoidStartPlatformFeature.Instance;
    }
}
