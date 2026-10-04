using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//CountPlacement 数量放置对应原版 CountPlacement
//按整数提供者采样次数在同一位置重复放置
public sealed class CountPlacement : RepeatingPlacement
{
    public static readonly Codec<CountPlacement> Codec =
        new SingleFieldPlacementCodec<CountPlacement, IntProvider>(
            IntProviders.NonNegativeCodec.FieldOf("count"),
            count => new CountPlacement(count),
            placement => placement.CountProvider);

    //CountProvider 数量提供者 属性名避开基类 Count 方法
    public IntProvider CountProvider { get; }

    private CountPlacement(IntProvider count) => CountProvider = count;

    //Of 构造入口对应原版 of
    public static CountPlacement Of(IntProvider count) => new(count);

    public static CountPlacement Of(int count) => new(ConstantInt.Of(count));

    protected override int Count(RandomSource random, BlockPos origin) => CountProvider.Sample(random);

    public override PlacementModifierType Type => CountPlacementType.Instance;
}

//CountPlacementType 对应原版 PlacementModifierType.COUNT
public sealed class CountPlacementType : PlacementModifierType<CountPlacement>
{
    public static readonly CountPlacementType Instance = Register(
        Identifier.WithDefaultNamespace("count"), new CountPlacementType());

    private CountPlacementType()
        : base(Identifier.WithDefaultNamespace("count"), CountPlacement.Codec) { }
}
