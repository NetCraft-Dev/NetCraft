using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructurePlacementType 结构放置类型基类 对应原版 StructurePlacementType
//持注册名与 decode 入口 装载时按 placement 的 type 字段派发到具体类型
public abstract class StructurePlacementType : NetCraft.Registry.StructurePlacementType<object>
{
    public Identifier Id { get; }

    protected StructurePlacementType(Identifier id) => Id = id;

    //Decode 从 map 解出一个放置实例 type 字段已由外层消费
    public abstract DataResult<StructurePlacement> Decode<U>(DynamicOps<U> ops, MapLike<U> input);

    //Register 注册进 STRUCTURE_PLACEMENT 并返回自身 便于静态字段直接赋值
    protected static T Register<T>(Identifier id, T type) where T : StructurePlacementType
    {
        Registry<NetCraft.Registry.StructurePlacementType<object>>.Register(
            BuiltInRegistries.STRUCTURE_PLACEMENT, id, type);
        return type;
    }

    public override string ToString() => $"StructurePlacementType[{Id}]";
}

//RandomSpreadStructurePlacementType 随机散布放置类型 对应原版 StructurePlacementType.RANDOM_SPREAD
public sealed class RandomSpreadStructurePlacementType : StructurePlacementType
{
    public static readonly RandomSpreadStructurePlacementType Instance =
        Register(Identifier.WithDefaultNamespace("random_spread"), new RandomSpreadStructurePlacementType());

    private RandomSpreadStructurePlacementType()
        : base(Identifier.WithDefaultNamespace("random_spread")) { }

    //Decode 解析公共字段与网格参数 对应原版 RandomSpreadStructurePlacement.CODEC
    public override DataResult<StructurePlacement> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => StructurePlacementCodecs.ReadCommon(ops, input).FlatMap(parts =>
        {
            var spacing = StructurePlacementCodecs.ReadIntField(ops, input, "spacing");
            if (!spacing.Result().IsPresent) return DataResult<StructurePlacement>.Error(() => "random_spread 缺少 spacing");
            var separation = StructurePlacementCodecs.ReadIntField(ops, input, "separation");
            if (!separation.Result().IsPresent) return DataResult<StructurePlacement>.Error(() => "random_spread 缺少 separation");
            var spreadType = RandomSpreadType.Linear;
            var spreadTag = input.Get("spread_type");
            if (spreadTag.IsPresent)
            {
                var text = ops.GetStringValue(spreadTag.Get());
                if (!text.Result().IsPresent) return DataResult<StructurePlacement>.Error(() => "spread_type 必须是字符串");
                var parsed = RandomSpreadTypes.TryParse(text.GetOrThrow());
                if (parsed is null) return DataResult<StructurePlacement>.Error(() => $"未知的 spread_type: {text.GetOrThrow()}");
                spreadType = parsed.Value;
            }
            return DataResult<StructurePlacement>.Success(new RandomSpreadStructurePlacement(
                parts.LocateOffset, parts.ReductionMethod, parts.Frequency, parts.Salt, parts.Exclusion,
                spacing.GetOrThrow(), separation.GetOrThrow(), spreadType));
        });
}

//StructurePlacementCodecs 结构放置 codec 入口 对应原版 StructurePlacement.CODEC
public static class StructurePlacementCodecs
{
    //ElementCodec 注册表元素 codec 按 type 派发到 STRUCTURE_PLACEMENT 里注册的放置类型
    public static readonly Codec<StructurePlacement> ElementCodec = new PlacementDispatchCodec();

    //ReadCommon 解析放置公共字段 各放置类型复用
    //locate_offset 与 frequency_reduction_method 可省 frequency 默认 1.0 salt 必填
    internal static DataResult<PlacementParts> ReadCommon<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var locateOffset = Vec3i.Zero;
        var offsetTag = input.Get("locate_offset");
        if (offsetTag.IsPresent)
        {
            var offset = ReadVec3i(ops, offsetTag.Get());
            if (!offset.Result().IsPresent) return DataResult<PlacementParts>.Error(() => "locate_offset 必须是有符号整数三元组");
            locateOffset = offset.GetOrThrow();
        }

        var method = FrequencyReductionMethod.Default;
        var methodTag = input.Get("frequency_reduction_method");
        if (methodTag.IsPresent)
        {
            var text = ops.GetStringValue(methodTag.Get());
            if (!text.Result().IsPresent) return DataResult<PlacementParts>.Error(() => "frequency_reduction_method 必须是字符串");
            var parsed = FrequencyReductionMethods.TryParse(text.GetOrThrow());
            if (parsed is null) return DataResult<PlacementParts>.Error(() => $"未知的 frequency_reduction_method: {text.GetOrThrow()}");
            method = parsed.Value;
        }

        var frequency = 1.0f;
        var frequencyTag = input.Get("frequency");
        if (frequencyTag.IsPresent)
        {
            var value = ops.GetNumberValue(frequencyTag.Get());
            if (!value.Result().IsPresent) return DataResult<PlacementParts>.Error(() => "frequency 必须是数值");
            frequency = (float)value.GetOrThrow();
        }

        var saltTag = input.Get("salt");
        if (!saltTag.IsPresent) return DataResult<PlacementParts>.Error(() => "placement 缺少必填字段 salt");
        var saltValue = ops.GetNumberValue(saltTag.Get());
        if (!saltValue.Result().IsPresent) return DataResult<PlacementParts>.Error(() => "salt 必须是整数");
        var salt = (int)saltValue.GetOrThrow();

        StructurePlacement.ExclusionZone? exclusion = null;
        var exclusionTag = input.Get("exclusion_zone");
        if (exclusionTag.IsPresent)
        {
            var mapResult = ops.GetMap(exclusionTag.Get());
            if (!mapResult.Result().IsPresent) return DataResult<PlacementParts>.Error(() => "exclusion_zone 必须是对象");
            var map = mapResult.GetOrThrow();
            var otherSetTag = map.Get("other_set");
            if (!otherSetTag.IsPresent) return DataResult<PlacementParts>.Error(() => "exclusion_zone 缺少 other_set");
            var otherSet = HolderSetCodecs.StructureSetRef.Parse(ops, otherSetTag.Get());
            if (!otherSet.Result().IsPresent) return DataResult<PlacementParts>.Error(() => "exclusion_zone.other_set 解析失败");
            var chunkCount = ReadIntField(ops, map, "chunk_count");
            if (!chunkCount.Result().IsPresent) return DataResult<PlacementParts>.Error(() => "exclusion_zone.chunk_count 必须是整数");
            exclusion = new StructurePlacement.ExclusionZone(otherSet.GetOrThrow(), chunkCount.GetOrThrow());
        }

        return DataResult<PlacementParts>.Success(new PlacementParts(locateOffset, method, frequency, salt, exclusion));
    }

    //ReadIntField 读一个整数标量字段 缺失或非数值都报错
    internal static DataResult<int> ReadIntField<U>(DynamicOps<U> ops, MapLike<U> map, string key)
    {
        var tag = map.Get(key);
        if (!tag.IsPresent) return DataResult<int>.Error(() => $"缺少字段 {key}");
        var value = ops.GetNumberValue(tag.Get());
        if (!value.Result().IsPresent) return DataResult<int>.Error(() => $"{key} 必须是数值");
        return DataResult<int>.Success((int)value.GetOrThrow());
    }

    //ReadVec3i 读三元素整数数组 对应原版 Vec3i.CODEC 的 [x, y, z] 形式
    internal static DataResult<Vec3i> ReadVec3i<U>(DynamicOps<U> ops, U input)
    {
        var streamResult = ops.GetStream(input);
        if (!streamResult.Result().IsPresent)
            return DataResult<Vec3i>.Error(() => "坐标必须是有符号整数三元组");
        var values = new List<int>();
        foreach (var element in streamResult.GetOrThrow())
        {
            var number = ops.GetNumberValue(element);
            if (!number.Result().IsPresent)
                return DataResult<Vec3i>.Error(() => "坐标必须是有符号整数三元组");
            var value = number.GetOrThrow();
            if (value != Math.Floor(value))
                return DataResult<Vec3i>.Error(() => "坐标必须是有符号整数三元组");
            values.Add((int)value);
        }
        if (values.Count != 3)
            return DataResult<Vec3i>.Error(() => $"坐标必须是有符号整数三元组 实际 {values.Count} 个");
        return DataResult<Vec3i>.Success(new Vec3i(values[0], values[1], values[2]));
    }
}

//PlacementParts 放置公共字段的解析结果 供各放置类型组装自己的实例
internal sealed record PlacementParts(Vec3i LocateOffset, FrequencyReductionMethod ReductionMethod,
    float Frequency, int Salt, StructurePlacement.ExclusionZone? Exclusion);

//PlacementDispatchCodec 按 type 字段查 STRUCTURE_PLACEMENT 再交给该类型的 decode 对应原版 dispatch codec
internal sealed class PlacementDispatchCodec : ScalarCodec<StructurePlacement>
{
    public override DataResult<StructurePlacement> Parse<U>(DynamicOps<U> ops, U input)
    {
        var mapResult = ops.GetMap(input);
        if (!mapResult.Result().IsPresent) return DataResult<StructurePlacement>.Error(() => "placement 必须是对象");
        var map = mapResult.GetOrThrow();
        var typeTag = map.Get("type");
        if (!typeTag.IsPresent) return DataResult<StructurePlacement>.Error(() => "placement 缺少 type 字段");
        var typeText = ops.GetStringValue(typeTag.Get());
        if (!typeText.Result().IsPresent) return DataResult<StructurePlacement>.Error(() => "placement.type 必须是字符串");
        var typeId = Identifier.TryParse(typeText.GetOrThrow());
        if (typeId is null) return DataResult<StructurePlacement>.Error(() => $"非法的 placement 类型: {typeText.GetOrThrow()}");
        var type = BuiltInRegistries.STRUCTURE_PLACEMENT.GetValue(typeId.Value) as StructurePlacementType;
        if (type is null) return DataResult<StructurePlacement>.Error(() => $"未注册的 placement 类型: {typeId}");
        return type.Decode(ops, map);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, StructurePlacement value)
        => DataResult<U>.Error(() => "结构放置编码暂未实现");
}
