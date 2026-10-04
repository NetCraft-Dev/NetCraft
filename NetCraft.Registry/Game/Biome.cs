using NetCraft.Codec;
using NetCraft.Registry.Codec;
using NetCraft.Registry.Environment;

namespace NetCraft.Registry;

//Biome 生物群系数据类对应原版 net.minecraft.world.level.biome.Biome
//DirectCodec 的 5 个成员全部内联 气候/属性/效果/生成/生成设置的字段直接是群系 JSON 顶层字段
public class Biome : RegistryIdentified
{
    //Plains 平原占位实例 数据驱动装载接入前供 Bootstrap 与测试使用 数值对齐 plains.json
    public static readonly Biome Plains = new Biome(
        new ClimateSettings(true, 0.8f, TemperatureModifier.None, 0.4f),
        EnvironmentAttributeMap.Empty,
        new BiomeSpecialEffects(unchecked((int)0xFF3F76E4), Optional<int>.Empty(), Optional<int>.Empty(), Optional<int>.Empty(),
            GrassColorModifier.None),
        BiomeGenerationSettings.Empty,
        MobSpawnSettings.Empty).WithId(Identifier.WithDefaultNamespace("plains"));

    //DirectCodec 元素 codec 对应原版 Biome.DIRECT_CODEC
    public static readonly Codec<Biome> DirectCodec =
        RecordCodecBuilder.Of5<Biome, ClimateSettings, EnvironmentAttributeMap, BiomeSpecialEffects, BiomeGenerationSettings, MobSpawnSettings>(
            ClimateSettings.Codec.ForGetter<Biome, ClimateSettings>(b => b.Climate),
            EnvironmentAttributeMap.CodecOnlyPositional.OptionalFieldOf("attributes", EnvironmentAttributeMap.Empty)
                .ForGetter<Biome, EnvironmentAttributeMap>(b => b.Attributes),
            BiomeSpecialEffects.Codec.FieldOf("effects").ForGetter<Biome, BiomeSpecialEffects>(b => b.Effects),
            BiomeGenerationSettings.Codec.ForGetter<Biome, BiomeGenerationSettings>(b => b.Generation),
            MobSpawnSettings.Codec.ForGetter<Biome, MobSpawnSettings>(b => b.MobSettings),
            (climate, attributes, effects, generation, mobSettings) =>
                new Biome(climate, attributes, effects, generation, mobSettings));

    //NetworkCodec 网络同步 codec 对应原版 Biome.NETWORK_CODEC 只含气候/属性/效果
    //生成与生成设置不参与同步 解出后置空
    public static readonly Codec<Biome> NetworkCodec =
        RecordCodecBuilder.Of3<Biome, ClimateSettings, EnvironmentAttributeMap, BiomeSpecialEffects>(
            ClimateSettings.Codec.ForGetter<Biome, ClimateSettings>(b => b.Climate),
            EnvironmentAttributeMap.NetworkCodec.OptionalFieldOf("attributes", EnvironmentAttributeMap.Empty)
                .ForGetter<Biome, EnvironmentAttributeMap>(b => b.Attributes),
            BiomeSpecialEffects.Codec.FieldOf("effects").ForGetter<Biome, BiomeSpecialEffects>(b => b.Effects),
            (climate, attributes, effects) =>
                new Biome(climate, attributes, effects, BiomeGenerationSettings.Empty, MobSpawnSettings.Empty));

    //Codec 按 Identifier 查 BIOME 注册表对应原版 Biome.CODEC 的注册表引用形态
    //供 biome_is 之类的群系引用解析使用 不是元素 codec
    //BIOME 是 DefaultedRegistry 找不到的 id 返回 plains 默认值
    public static readonly Codec<Biome> Codec = IdentifierCodec.Instance.ComapFlatMap(
        id => DataResult<Biome>.Success(BuiltInRegistries.BIOME.GetValue(id)!),
        biome => biome.Id);

    private Identifier? _id;

    public Biome(ClimateSettings climate, EnvironmentAttributeMap attributes, BiomeSpecialEffects effects,
        BiomeGenerationSettings generation, MobSpawnSettings mobSettings)
    {
        Climate = climate;
        Attributes = attributes;
        Effects = effects;
        Generation = generation;
        MobSettings = mobSettings;
    }

    //受保护无参构造供占位子类使用 只实现 Id 的占位实例气候/效果取空默认值
    protected Biome()
        : this(
            new ClimateSettings(false, 0.5f, TemperatureModifier.None, 0.0f),
            EnvironmentAttributeMap.Empty,
            new BiomeSpecialEffects(unchecked((int)0xFF1E98E2), Optional<int>.Empty(), Optional<int>.Empty(), Optional<int>.Empty(),
                GrassColorModifier.None),
            BiomeGenerationSettings.Empty,
            MobSpawnSettings.Empty)
    {
    }

    public ClimateSettings Climate { get; }

    public EnvironmentAttributeMap Attributes { get; }

    public BiomeSpecialEffects Effects { get; }

    public BiomeGenerationSettings Generation { get; }

    public MobSpawnSettings MobSettings { get; }

    //Id 群系注册名 数据驱动装载时由注册表按元素路径回填 未回填时回退注册表默认值 plains
    public virtual Identifier Id => _id ?? Identifier.WithDefaultNamespace("plains");

    //WithId 回填注册名 供装载流程与占位实例使用
    public Biome WithId(Identifier id)
    {
        _id = id;
        return this;
    }

    //SetRegistryId 由数据驱动装载流程在写入注册表时回填注册名
    public void SetRegistryId(Identifier id) => _id = id;
}
