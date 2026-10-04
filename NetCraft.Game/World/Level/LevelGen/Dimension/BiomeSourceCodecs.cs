using NetCraft.Codec;
using NetCraft.Registry;
using NetCraft.Registry.Codec;

namespace NetCraft.Game.World.Level.LevelGen.Dimension;

//BiomeSourceCodecs 生物群系源编解码 对应原版 BiomeSources 的注册与 dispatch
//原版走 MapCodec 注册表派发 本作类型集合封闭 直接按 type 字段手写分支
//multi_noise 支持 preset 形态 the_end 从注册表取末地五群系
public static class BiomeSourceCodecs
{
    //Decode 按 type 字段派生生物群系源
    public static DataResult<BiomeSource> Decode<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodeMap(ops, map));

    private static DataResult<BiomeSource> DecodeMap<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var typeText = ReadString(ops, input, "type");
        if (typeText is null)
            return DataResult<BiomeSource>.Error(() => "生物群系源缺 type 字段");
        var typeId = Identifier.TryParse(typeText);
        if (typeId is null)
            return DataResult<BiomeSource>.Error(() => $"生物群系源类型标识非法 {typeText}");

        switch (typeId.Value.Path)
        {
            case "multi_noise":
                return DecodeMultiNoise(ops, input);
            case "the_end":
                return DecodeTheEnd(ops);
            default:
                return DataResult<BiomeSource>.Error(() => $"暂不支持的生物群系源类型 {typeId}");
        }
    }

    //DecodeTheEnd 末地群系源无参数 群系按原版固定五件套从注册表取
    private static DataResult<BiomeSource> DecodeTheEnd<U>(DynamicOps<U> ops)
    {
        var source = TheEndBiomeSource.FromRegistry(BiomeRegistry(ops));
        return source is null
            ? DataResult<BiomeSource>.Error(() => "末地群系源缺少必需群系(the_end/end_highlands/end_midlands/small_end_islands/end_barrens)")
            : DataResult<BiomeSource>.Success(source);
    }

    //DecodeMultiNoise 多噪声群系源 目前只支持 preset 形态
    //内联 biomes 列表形态数据包里没用到 等有需求再补
    private static DataResult<BiomeSource> DecodeMultiNoise<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var presetText = ReadString(ops, input, "preset");
        if (presetText is null)
            return DataResult<BiomeSource>.Error(() => "multi_noise 目前只支持 preset 形态");
        var presetId = Identifier.TryParse(presetText);
        if (presetId is null)
            return DataResult<BiomeSource>.Error(() => $"参数表预设标识非法 {presetText}");
        //参数表必须已被数据驱动装载 未装载说明数据包缺 worldgen/multi_noise_biome_source_parameter_list
        if (ParameterListRegistry(ops)?.GetValue(presetId.Value) is not MultiNoiseBiomeSourceParameterList parameterList)
            return DataResult<BiomeSource>.Error(() => $"未找到参数表预设 {presetId}");
        return DataResult<BiomeSource>.Success(new MultiNoiseBiomeSource(parameterList));
    }

    //ParameterListRegistry 参数表注册表优先取 RegistryOps 携带的那份 非注册表 ops 退回内置
    private static Registry<object>? ParameterListRegistry<U>(DynamicOps<U> ops)
        => (ops as RegistryOps<U>)?.GetRegistry(Registries.MULTI_NOISE_BIOME_SOURCE_PARAMETER_LIST)
            ?? BuiltInRegistries.MULTI_NOISE_BIOME_SOURCE_PARAMETER_LIST;

    //BiomeRegistry 群系注册表同上 测试可以喂独立注册表而不必动全局
    private static Registry<Biome>? BiomeRegistry<U>(DynamicOps<U> ops)
        => (ops as RegistryOps<U>)?.GetRegistry(Registries.BIOME) ?? BuiltInRegistries.BIOME;

    //ReadString 读字符串字段缺失或非字符串返回 null
    private static string? ReadString<U>(DynamicOps<U> ops, MapLike<U> input, string name)
    {
        var tag = input.Get(name);
        if (!tag.IsPresent) return null;
        var text = ops.GetStringValue(tag.Get());
        return text.Result().IsPresent ? text.GetOrThrow() : null;
    }
}
