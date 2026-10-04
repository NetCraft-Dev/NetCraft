using NetCraft.Codec;
using NetCraft.Registry;
using NetCraft.Registry.Codec;

namespace NetCraft.Game.World.Level.LevelGen;

//MultiNoisePreset 多噪声参数表预设对应原版 MultiNoiseBiomeSourceParameterList.Preset
//只提供 preset 标识与参数表生成入口 参数表内容未移植
public sealed class MultiNoisePreset
{
    public Identifier Id { get; }

    private MultiNoisePreset(Identifier id) => Id = id;

    //Overworld/Nether 内置预设对应原版 OVERWORLD/NETHER
    public static readonly MultiNoisePreset Overworld = new(Identifier.WithDefaultNamespace("overworld"));
    public static readonly MultiNoisePreset Nether = new(Identifier.WithDefaultNamespace("nether"));

    private static readonly MultiNoisePreset[] All = { Overworld, Nether };

    //ById 按注册名查预设找不到返回 null
    public static MultiNoisePreset? ById(Identifier id)
    {
        foreach (var preset in All)
            if (preset.Id == id) return preset;
        return null;
    }

    //CreateEntries 生成参数表条目
    //lookup 把群系 ResourceKey 查成 Holder 对应原版把 HolderGetter 传进预设的 provider
    //Overworld 走 OverworldBiomeBuilder 现场展开 Nether 是硬编码 5 条
    public IReadOnlyList<(Climate.ParameterPoint Point, Holder<Biome> Biome)> CreateEntries(
        Func<ResourceKey<Biome>, Holder<Biome>> lookup)
    {
        var entries = new List<(Climate.ParameterPoint, Holder<Biome>)>();
        if (Id == Overworld.Id)
        {
            new OverworldBiomeBuilder().AddBiomes(e => entries.Add((e.Point, lookup(e.Biome))));
        }
        else if (Id == Nether.Id)
        {
            foreach (var (point, biome) in OverworldBiomeBuilder.NetherBiomes())
                entries.Add((point, lookup(biome)));
        }
        return entries;
    }

    //Codec 按注册名编解码对应原版 Preset.CODEC
    public static readonly Codec<MultiNoisePreset> Codec = IdentifierCodec.Instance.ComapFlatMap(
        id => ById(id) is { } preset
            ? DataResult<MultiNoisePreset>.Success(preset)
            : DataResult<MultiNoisePreset>.Error(() => $"Unknown preset: {id}"),
        preset => preset.Id);
}

//MultiNoiseBiomeSourceParameterListCodec 参数表 codec 对应原版 DIRECT_CODEC
//JSON 只有 preset 一个字段 群系列表由 preset 内置生成
public sealed class MultiNoiseBiomeSourceParameterListCodec : AbstractMapCodec<MultiNoiseBiomeSourceParameterList>
{
    public static readonly MultiNoiseBiomeSourceParameterListCodec Instance = new();

    public override DataResult<MultiNoiseBiomeSourceParameterList> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var presetTag = input.Get("preset");
        if (!presetTag.IsPresent)
            return DataResult<MultiNoiseBiomeSourceParameterList>.Error(() => "Missing key preset");
        var presetResult = MultiNoisePreset.Codec.Parse(ops, presetTag.Get());
        if (!presetResult.Result().IsPresent)
            return DataResult<MultiNoiseBiomeSourceParameterList>.Error(() => "Invalid preset for MultiNoiseBiomeSourceParameterList");
        var preset = presetResult.GetOrThrow();

        //参数表由 preset 现场展开 群系要按 ResourceKey 查 BIOME 注册表
        var biomes = (ops as RegistryOps<U>)?.GetRegistry(Registries.BIOME) ?? BuiltInRegistries.BIOME;
        return DataResult<MultiNoiseBiomeSourceParameterList>.Success(
            new MultiNoiseBiomeSourceParameterList(preset, preset.CreateEntries(key => Lookup(biomes, key))));
    }

    //Lookup 按 ResourceKey 取群系 缺失时抛异常由加载器记成该元素的错误
    //BIOME 是带默认值的注册表 GetValue 对未注册键会回退默认群系 必须先 ContainsKey 否则会静默串成群系
    private static Holder<Biome> Lookup(Registry<Biome> biomes, ResourceKey<Biome> key)
        => biomes.ContainsKey(key.Identifier) && biomes.GetValue(key.Identifier) is { } biome
            ? Holder<Biome>.Direct(biome)
            : throw new InvalidOperationException($"Unknown biome: {key.Identifier}");

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, MultiNoiseBiomeSourceParameterList value,
        RecordBuilder<U> builder)
    {
        if (value.Preset is null)
            return builder.Add("preset", DataResult<U>.Error(() => "MultiNoiseBiomeSourceParameterList has no preset").GetOrThrow());
        builder.Add("preset", ops.CreateString(value.Preset.Id.ToString()));
        return builder;
    }
}
