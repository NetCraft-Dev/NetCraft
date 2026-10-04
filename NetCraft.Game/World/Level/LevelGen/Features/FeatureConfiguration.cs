using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.Features;

//FeatureConfiguration 特征配置基类对应原版 FeatureConfiguration
//子类各自持自己的参数 内嵌的子特征引用靠 SubFeatures 暴露 供装饰统计与权重选择链使用
public abstract class FeatureConfiguration
{
    //SubFeatures 本配置内嵌的子特征引用对应原版 getSubFeatures
    public virtual IEnumerable<Holder<NetCraft.Registry.ConfiguredFeature>> SubFeatures
        => Array.Empty<Holder<NetCraft.Registry.ConfiguredFeature>>();
}

//NoneFeatureConfiguration 无参配置对应原版 NoneFeatureConfiguration
//JSON 里写成空对象 解出来的是共享单例
public sealed class NoneFeatureConfiguration : FeatureConfiguration
{
    public static readonly NoneFeatureConfiguration Instance = new();

    public static readonly Codec<NoneFeatureConfiguration> Codec = new UnitCodec();

    private NoneFeatureConfiguration() { }
}

//UnitCodec 无参配置 codec 忽略输入直接给单例 对应原版 MapCodec.unit
internal sealed class UnitCodec : ScalarCodec<NoneFeatureConfiguration>
{
    public override DataResult<NoneFeatureConfiguration> Parse<U>(DynamicOps<U> ops, U input)
        => DataResult<NoneFeatureConfiguration>.Success(NoneFeatureConfiguration.Instance);

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, NoneFeatureConfiguration value)
        => DataResult<U>.Success(ops.Empty());
}
