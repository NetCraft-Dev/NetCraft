namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Nether;

//NetherBootstrap 下界与末地特征注册入口
//触碰各静态 Instance 使静态注册生效 由 FeatureBootstrap 调用
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
