using NetCraft.Codec;
using NetCraft.Registry;
using NetCraft.Registry.Codec;

namespace NetCraft.Game.World.Level.LevelGen.Dimension;

//LevelStem 关卡定义 对应原版 net.minecraft.world.level.dimension.LevelStem
//一个维度 = 维度类型引用 + 区块生成器 数据包里的 world_preset 就是一组 LevelStem
//TypeId 是 dimension_type 注册表里的键 装配时按它取真实的 DimensionType
public sealed record LevelStem(Identifier TypeId, ChunkGenerator Generator) : NetCraft.Registry.LevelStem
{
    //Codec 元素 codec 对应原版 LevelStem.CODEC
    public static readonly Codec<LevelStem> Codec = new LevelStemCodec();
}

//LevelStemCodec 关卡定义 codec 对应原版 LevelStem.CODEC
//generator 的 type 在数据包里目前只有 minecraft:noise 一种 直接内联分支不绕注册表派发
internal sealed class LevelStemCodec : AbstractMapCodec<LevelStem>
{
    public override DataResult<LevelStem> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var typeText = ReadString(ops, input, "type");
        if (typeText is null)
            return DataResult<LevelStem>.Error(() => "关卡定义缺 type 字段");
        var typeId = Identifier.TryParse(typeText);
        if (typeId is null)
            return DataResult<LevelStem>.Error(() => $"维度类型标识非法 {typeText}");
        var generatorTag = input.Get("generator");
        if (!generatorTag.IsPresent)
            return DataResult<LevelStem>.Error(() => "关卡定义缺 generator 字段");
        var generator = DecodeGenerator(ops, generatorTag.Get());
        if (!generator.Result().IsPresent)
            return DataResult<LevelStem>.Error(() => "generator 解析失败");
        return DataResult<LevelStem>.Success(new LevelStem(typeId.Value, generator.GetOrThrow()));
    }

    //EncodeTo 本阶段没有回写关卡定义的需求 level.dat 的 dimensions 写空 map
    //需要回写时再补 噪声设置的注册名可以从 NOISE_SETTINGS 反查
    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, LevelStem value, RecordBuilder<U> builder)
        => throw new NotSupportedException("关卡定义暂不支持编码");

    private static DataResult<ChunkGenerator> DecodeGenerator<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodeGeneratorMap(ops, map));

    private static DataResult<ChunkGenerator> DecodeGeneratorMap<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var typeText = ReadString(ops, input, "type");
        if (typeText is null)
            return DataResult<ChunkGenerator>.Error(() => "区块生成器缺 type 字段");
        var typeId = Identifier.TryParse(typeText);
        if (typeId is null)
            return DataResult<ChunkGenerator>.Error(() => $"区块生成器类型标识非法 {typeText}");
        if (typeId.Value.Path != "noise")
            return DataResult<ChunkGenerator>.Error(() => $"暂不支持该区块生成器类型 {typeId}");

        var biomeTag = input.Get("biome_source");
        if (!biomeTag.IsPresent)
            return DataResult<ChunkGenerator>.Error(() => "噪声生成器缺 biome_source 字段");
        var biomeSource = BiomeSourceCodecs.Decode(ops, biomeTag.Get());
        if (!biomeSource.Result().IsPresent)
            return DataResult<ChunkGenerator>.Error(() => "biome_source 解析失败");

        var settingsText = ReadString(ops, input, "settings");
        if (settingsText is null)
            return DataResult<ChunkGenerator>.Error(() => "噪声生成器缺 settings 字段");
        var settingsId = Identifier.TryParse(settingsText);
        if (settingsId is null)
            return DataResult<ChunkGenerator>.Error(() => $"噪声设置标识非法 {settingsText}");
        //噪声设置由第 8 步数据驱动装载 未装载说明数据包缺 worldgen/noise_settings
        var settingsRegistry = (ops as RegistryOps<U>)?.GetRegistry(Registries.NOISE_SETTINGS)
            ?? BuiltInRegistries.NOISE_SETTINGS;
        if (settingsRegistry.GetValue(settingsId.Value) is not NoiseGeneratorSettings settings)
            return DataResult<ChunkGenerator>.Error(() => $"未找到噪声设置 {settingsId}");
        return DataResult<ChunkGenerator>.Success(new NoiseBasedChunkGenerator(biomeSource.GetOrThrow(), settings));
    }

    //ReadString 读字符串字段缺失或非字符串返回 null
    private static string? ReadString<U>(DynamicOps<U> ops, MapLike<U> input, string name)
    {
        var tag = input.Get(name);
        if (!tag.IsPresent) return null;
        var text = ops.GetStringValue(tag.Get());
        return text.Result().IsPresent ? text.GetOrThrow() : null;
    }
}
