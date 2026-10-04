using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//PlacementContext 放置上下文对应原版 PlacementContext
//在生成范围之外还带上世界与生成器 修饰器靠它查高度图/方块/群系
public sealed class PlacementContext : WorldGenerationContext
{
    public WorldGenRegion Level { get; }
    public ChunkGenerator Generator { get; }

    //TopFeature 当前正在放置的已放置特征 群系过滤要靠它反查所属群系
    public PlacedFeature? TopFeature { get; }

    public PlacementContext(WorldGenRegion level, ChunkGenerator generator, PlacedFeature? topFeature)
        : base(generator, level)
    {
        Level = level;
        Generator = generator;
        TopFeature = topFeature;
    }
}

//PlacementModifierType 放置修饰器类型单例对应原版 PlacementModifierType
//持类型 id 与「map → 实例」的解码入口 注册进 PLACEMENT_MODIFIER_TYPE 注册表
public abstract class PlacementModifierType : NetCraft.Registry.PlacementModifierType
{
    public Identifier Id { get; }

    protected PlacementModifierType(Identifier id) => Id = id;

    //Decode 从 map 解出一个带参数的修饰器实例 type 字段已由外层消费
    public abstract DataResult<PlacementModifier> Decode<U>(DynamicOps<U> ops, MapLike<U> input);

    //EncodeFields 把实例参数累积进 builder type 字段由外层补
    public abstract void EncodeFields<U>(DynamicOps<U> ops, PlacementModifier value, RecordBuilder<U> builder);

    //Register 注册进 PLACEMENT_MODIFIER_TYPE 并返回自身 便于静态字段直接赋值
    protected static T Register<T>(Identifier id, T type) where T : PlacementModifierType
    {
        Registry<NetCraft.Registry.PlacementModifierType>.Register(
            BuiltInRegistries.PLACEMENT_MODIFIER_TYPE, id, type);
        return type;
    }
}

//PlacementModifierType<P> 具体修饰器类型的泛型中间层 子类只需给出一个 MapCodec<P>
public abstract class PlacementModifierType<P> : PlacementModifierType where P : PlacementModifier
{
    private readonly MapCodec<P> _codec;

    protected PlacementModifierType(Identifier id, MapCodec<P> codec) : base(id) => _codec = codec;

    public sealed override DataResult<PlacementModifier> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => _codec.Decode(ops, input).Map(value => (PlacementModifier)value);

    public sealed override void EncodeFields<U>(DynamicOps<U> ops, PlacementModifier value, RecordBuilder<U> builder)
    {
        if (value is P typed) _codec.EncodeTo(ops, typed, builder);
    }
}

//PlacementModifier 放置修饰器实例基类对应原版 PlacementModifier
//从一批候选位置推出下一批 多个修饰器串起来构成放置链
public abstract class PlacementModifier
{
    //Type 所属类型单例 编码与日志要靠它拿 id
    public abstract PlacementModifierType Type { get; }

    //GetPositions 从起点推出一批位置对应原版 getPositions
    public abstract IEnumerable<BlockPos> GetPositions(PlacementContext context, RandomSource random, BlockPos origin);
}

//PlacementModifierCodec 按 type 字段查 PLACEMENT_MODIFIER_TYPE 再委派给该类型解码
//对应原版 BuiltInRegistries.PLACEMENT_MODIFIER_TYPE.byNameCodec().dispatch(...)
//type 与参数平铺在同一层 与原版把 MapCodec 字段内联进 dispatch 结果的行为一致
internal sealed class PlacementModifierCodec : ScalarCodec<PlacementModifier>
{
    public static readonly PlacementModifierCodec Instance = new();

    public override DataResult<PlacementModifier> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodeModifier(ops, map));

    private static DataResult<PlacementModifier> DecodeModifier<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var typeTag = input.Get("type");
        if (!typeTag.IsPresent) return DataResult<PlacementModifier>.Error(() => "放置修饰器缺 type 字段");
        var typeText = ops.GetStringValue(typeTag.Get());
        if (!typeText.Result().IsPresent)
            return DataResult<PlacementModifier>.Error(() => "放置修饰器的 type 必须是字符串");
        var typeId = Identifier.TryParse(typeText.GetOrThrow());
        if (typeId is null)
            return DataResult<PlacementModifier>.Error(() => $"非法的修饰器类型: {typeText.GetOrThrow()}");
        if (BuiltInRegistries.PLACEMENT_MODIFIER_TYPE.GetValue(typeId.Value) is not PlacementModifierType type)
            return DataResult<PlacementModifier>.Error(() => $"未知的修饰器类型: {typeId}");
        return type.Decode(ops, input);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, PlacementModifier value)
    {
        var builder = ops.MapBuilder();
        builder.Add("type", ops.CreateString(value.Type.Id.ToString()));
        value.Type.EncodeFields(ops, value, builder);
        return builder.Build(ops.Empty());
    }
}
