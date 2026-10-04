using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.Features;

//NoOpFeature 空特征对应原版 NoOpFeature
//什么都不放置 注册它是为了让 Feature 链路有一个可端到端验证的最小实现
public sealed class NoOpFeature : Feature<NoneFeatureConfiguration>
{
    private const string FeatureId = "no_op";

    //Instance 单例 与 FEATURE 注册表里的 minecraft:no_op 对应
    public static readonly NoOpFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new NoOpFeature());

    private NoOpFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), NoneFeatureConfiguration.Codec) { }

    protected override bool Place(NoneFeatureConfiguration config, FeaturePlaceContext context) => false;
}

//FeatureBootstrap 特征注册入口
//注册表要先于数据加载填好 否则 configured_feature 的 type 字段整批解不出来
public static class FeatureBootstrap
{
    public static void RegisterAll()
    {
        _ = NoOpFeature.Instance;
        //必备特征与方块状态提供者由 Impl 下的批次统一登记 触碰一次即触发静态注册
        Impl.RequiredFeatures.RegisterAll();
        //树木体系 树干/树叶/树根放置器与装饰器都是独立注册表 缺一个 tree 的 JSON 就整批解不出来
        Impl.Trees.TreeBootstrap.RegisterAll();
        Impl.Vegetation.VegetationBootstrap.RegisterAll();
        Impl.Nether.NetherBootstrap.RegisterAll();
        Impl.Misc.MiscBootstrap.RegisterAll();
        //噪声状态提供者也是 BLOCKSTATE_PROVIDER_TYPE 的一部分 花朵这类按噪声取状态的特征靠它
        Impl.NoiseProviderBootstrap.RegisterAll();
    }
}
