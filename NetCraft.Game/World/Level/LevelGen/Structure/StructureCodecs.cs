using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructureCodecs 结构 codec 入口 对应原版 Structure.DIRECT_CODEC
//公共设置字段由 ReadSettings 提供 具体结构类型在自己的 codec 里拼上类型特有字段
public static class StructureCodecs
{
    //ElementCodec 结构注册表元素 codec 按 type 派发到 STRUCTURE_TYPE 里注册的结构类型
    public static readonly Codec<NetCraft.Registry.Structure> ElementCodec = new StructureDispatchCodec();

    //ReadSettings 解析结构公共设置字段 biomes / step / terrain_adaptation 各类型 codec 复用
    //spawn_overrides 本作不实现刷怪覆盖 解析时忽略
    internal static DataResult<StructureGenerationSettings> ReadSettings<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var biomesTag = input.Get("biomes");
        if (!biomesTag.IsPresent) return DataResult<StructureGenerationSettings>.Error(() => "结构缺少必填字段 biomes");
        var biomes = HolderSetCodecs.BiomeSet.Parse(ops, biomesTag.Get());
        if (!biomes.Result().IsPresent) return DataResult<StructureGenerationSettings>.Error(() => "结构的 biomes 解析失败");

        var step = Features.GenerationStep.Decoration.SurfaceStructures;
        var stepTag = input.Get("step");
        if (stepTag.IsPresent)
        {
            var text = ops.GetStringValue(stepTag.Get());
            if (!text.Result().IsPresent) return DataResult<StructureGenerationSettings>.Error(() => "step 必须是字符串");
            var parsed = Features.GenerationStep.TryParse(text.GetOrThrow());
            if (parsed is null) return DataResult<StructureGenerationSettings>.Error(() => $"未知的装饰步骤: {text.GetOrThrow()}");
            step = parsed.Value;
        }

        var adaptation = TerrainAdjustment.None;
        var adaptationTag = input.Get("terrain_adaptation");
        if (adaptationTag.IsPresent)
        {
            var text = ops.GetStringValue(adaptationTag.Get());
            if (!text.Result().IsPresent) return DataResult<StructureGenerationSettings>.Error(() => "terrain_adaptation 必须是字符串");
            var parsed = TerrainAdjustments.TryParse(text.GetOrThrow());
            if (parsed is null) return DataResult<StructureGenerationSettings>.Error(() => $"未知的地形适配方式: {text.GetOrThrow()}");
            adaptation = parsed.Value;
        }

        return DataResult<StructureGenerationSettings>.Success(new StructureGenerationSettings(biomes.GetOrThrow(), step, adaptation));
    }
}

//StructureDispatchCodec 按 type 字段查 STRUCTURE_TYPE 再交给该类型的 codec 对应原版 dispatch codec
internal sealed class StructureDispatchCodec : ScalarCodec<NetCraft.Registry.Structure>
{
    public override DataResult<NetCraft.Registry.Structure> Parse<U>(DynamicOps<U> ops, U input)
    {
        var mapResult = ops.GetMap(input);
        if (!mapResult.Result().IsPresent) return DataResult<NetCraft.Registry.Structure>.Error(() => "结构必须是对象");
        var map = mapResult.GetOrThrow();
        var typeTag = map.Get("type");
        if (!typeTag.IsPresent) return DataResult<NetCraft.Registry.Structure>.Error(() => "结构缺少 type 字段");
        var typeText = ops.GetStringValue(typeTag.Get());
        if (!typeText.Result().IsPresent) return DataResult<NetCraft.Registry.Structure>.Error(() => "结构 type 必须是字符串");
        var typeId = Identifier.TryParse(typeText.GetOrThrow());
        if (typeId is null) return DataResult<NetCraft.Registry.Structure>.Error(() => $"非法的结构类型: {typeText.GetOrThrow()}");
        var type = BuiltInRegistries.STRUCTURE_TYPE.GetValue(typeId.Value) as StructureType;
        if (type is null) return DataResult<NetCraft.Registry.Structure>.Error(() => $"未注册的结构类型: {typeId}");
        if (type.ElementCodec is null) return DataResult<NetCraft.Registry.Structure>.Error(() => $"结构类型 {typeId} 不支持数据驱动装载");
        return type.ElementCodec.Parse(ops, input);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, NetCraft.Registry.Structure value)
        => value is Structure structure && structure.Type.ElementCodec is { } codec
            ? codec.EncodeStart(ops, value)
            : DataResult<U>.Error(() => "结构没有可用的元素 codec 无法编码");
}
