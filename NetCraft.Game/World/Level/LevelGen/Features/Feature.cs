using NetCraft.Codec;
using NetCraft.Game.World.Level.Block;
using NetCraft.Registry;
using NetCraft.Registry.State;

namespace NetCraft.Game.World.Level.LevelGen.Features;

//Feature 特征抽象基类 非泛型化对应原版 Feature<FC>
//原版按配置类型泛型 NetCraft 无协变泛型 用泛型中间层 Feature<FC> 承载具体配置
//FEATURE 注册表与 ConfiguredFeature 的 dispatch 只依赖这一层
public abstract class Feature : NetCraft.Registry.Feature
{
    //Air 空气状态 放置前的可替换判断用
    protected static readonly BlockState Air = Blocks.AIR.DefaultBlockState;

    //Id 注册名 与 FEATURE 注册表里的键一致
    public Identifier Id { get; }

    protected Feature(Identifier id) => Id = id;

    //ConfigCodec 解本特征配置的 codec 供 ConfiguredFeature 的 dispatch 使用
    public abstract Codec<FeatureConfiguration> ConfigCodec { get; }

    //Place 按配置放置特征对应原版 place(FC, WorldGenLevel, ChunkGenerator, RandomSource, BlockPos)
    public abstract bool Place(FeatureConfiguration config, FeaturePlaceContext context);

    //Register 注册进 FEATURE 注册表并返回自身 便于静态字段直接赋值
    protected static T Register<T>(Identifier id, T feature) where T : Feature
    {
        Registry<NetCraft.Registry.Feature>.Register(BuiltInRegistries.FEATURE, id, feature);
        return feature;
    }
}

//Feature<FC> 带具体配置类型的泛型中间层 子类只需实现 Place(FC, context)
public abstract class Feature<FC> : Feature where FC : FeatureConfiguration
{
    private readonly Codec<FC> _codec;
    private UpcastCodec<FC>? _upcast;

    protected Feature(Identifier id, Codec<FC> codec) : base(id) => _codec = codec;

    protected abstract bool Place(FC config, FeaturePlaceContext context);

    public sealed override bool Place(FeatureConfiguration config, FeaturePlaceContext context)
        => Place((FC)config, context);

    public sealed override Codec<FeatureConfiguration> ConfigCodec => _upcast ??= new UpcastCodec<FC>(_codec);
}

//UpcastCodec 把具体配置 codec 适配成基类 codec 供非泛型的 dispatch 使用
//解码出的对象本身就是 FC 实例 这里只做类型上转
internal sealed class UpcastCodec<FC> : ScalarCodec<FeatureConfiguration> where FC : FeatureConfiguration
{
    private readonly Codec<FC> _inner;

    public UpcastCodec(Codec<FC> inner) => _inner = inner;

    public override DataResult<FeatureConfiguration> Parse<U>(DynamicOps<U> ops, U input)
        => _inner.Parse(ops, input).Map(value => (FeatureConfiguration)value);

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, FeatureConfiguration value)
        => value is FC typed
            ? _inner.EncodeStart(ops, typed)
            : DataResult<U>.Error(() => $"配置类型不匹配 期望 {typeof(FC).Name} 实际 {value.GetType().Name}");
}
