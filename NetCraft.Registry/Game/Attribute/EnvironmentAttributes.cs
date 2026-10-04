using NetCraft.Codec;
using NetCraft.Registry.Codec;
using BackgroundMusicSettings = NetCraft.Registry.Environment.BackgroundMusic;
using AmbientSoundSettings = NetCraft.Registry.Environment.AmbientSounds;

namespace NetCraft.Registry.Environment;

//EnvironmentAttributes 内置环境属性定义对应原版 EnvironmentAttributes
public static class EnvironmentAttributes
{
    private static readonly AttributeType<bool> BooleanType =
        AttributeType<bool>.OfNotInterpolated(Codecs.Bool, AttributeModifier.BooleanLibrary);

    private static readonly AttributeType<float> FloatType =
        AttributeType<float>.OfInterpolated(Codecs.Float, AttributeModifier.FloatLibrary);

    private static readonly AttributeType<int> RgbColorType =
        AttributeType<int>.OfInterpolated(HexColorCodec.StringRgb, AttributeModifier.RgbColorLibrary);

    private static readonly AttributeType<IReadOnlyList<AmbientParticle>> AmbientParticlesType =
        AttributeType<IReadOnlyList<AmbientParticle>>.OfNotInterpolated(AmbientParticle.Codec.ListOf());

    private static readonly AttributeType<BackgroundMusicSettings> BackgroundMusicType =
        AttributeType<BackgroundMusicSettings>.OfNotInterpolated(BackgroundMusicSettings.Codec);

    private static readonly AttributeType<AmbientSoundSettings> AmbientSoundsType =
        AttributeType<AmbientSoundSettings>.OfNotInterpolated(AmbientSoundSettings.Codec);

    public static readonly EnvironmentAttribute<int> FogColor = EnvironmentAttribute<int>.CreateBuilder(RgbColorType)
        .DefaultValue(0).SpatiallyInterpolated().Syncable().Build();

    public static readonly EnvironmentAttribute<int> WaterFogColor = EnvironmentAttribute<int>.CreateBuilder(RgbColorType)
        .DefaultValue(-16448205).SpatiallyInterpolated().Syncable().Build();

    public static readonly EnvironmentAttribute<float> WaterFogEndDistance = EnvironmentAttribute<float>.CreateBuilder(FloatType)
        .DefaultValue(96.0f).ValueRange(AttributeRange<float>.NonNegativeFloat).SpatiallyInterpolated().Syncable().Build();

    public static readonly EnvironmentAttribute<int> SkyColor = EnvironmentAttribute<int>.CreateBuilder(RgbColorType)
        .DefaultValue(0).SpatiallyInterpolated().Syncable().Build();

    public static readonly EnvironmentAttribute<IReadOnlyList<AmbientParticle>> AmbientParticles =
        EnvironmentAttribute<IReadOnlyList<AmbientParticle>>.CreateBuilder(AmbientParticlesType)
            .DefaultValue(Array.Empty<AmbientParticle>()).Syncable().Build();

    public static readonly EnvironmentAttribute<BackgroundMusicSettings> BackgroundMusic =
        EnvironmentAttribute<BackgroundMusicSettings>.CreateBuilder(BackgroundMusicType)
            .DefaultValue(BackgroundMusicSettings.Empty).Syncable().Build();

    public static readonly EnvironmentAttribute<float> MusicVolume = EnvironmentAttribute<float>.CreateBuilder(FloatType)
        .DefaultValue(1.0f).ValueRange(AttributeRange<float>.UnitFloat).Syncable().Build();

    public static readonly EnvironmentAttribute<AmbientSoundSettings> AmbientSounds =
        EnvironmentAttribute<AmbientSoundSettings>.CreateBuilder(AmbientSoundsType)
            .DefaultValue(AmbientSoundSettings.Empty).Syncable().Build();

    public static readonly EnvironmentAttribute<bool> IncreasedFireBurnout = EnvironmentAttribute<bool>.CreateBuilder(BooleanType)
        .DefaultValue(false).Build();

    public static readonly EnvironmentAttribute<bool> SnowGolemMelts = EnvironmentAttribute<bool>.CreateBuilder(BooleanType)
        .DefaultValue(false).Build();

    public static readonly EnvironmentAttribute<bool> CanPillagerPatrolSpawn = EnvironmentAttribute<bool>.CreateBuilder(BooleanType)
        .DefaultValue(true).Build();

    //SkyLightLevel 不参与位置采样，仅用于维度级同步
    public static readonly EnvironmentAttribute<float> SkyLightLevel = EnvironmentAttribute<float>.CreateBuilder(FloatType)
        .DefaultValue(15.0f).ValueRange(AttributeRange<float>.OfFloat(0.0f, 15.0f)).NotPositional().Syncable().Build();

    //Codec 按 id 在注册表中解析环境属性
    public static readonly Codec<IEnvironmentAttribute> Codec = new EnvironmentAttributeByIdCodec();

    private static bool _registered;

    static EnvironmentAttributes() { RegisterAll(); }

    //RegisterAll 注册全部内置环境属性，重复调用幂等
    public static void RegisterAll()
    {
        if (_registered) return;
        _registered = true;
        Register(FogColor, "visual/fog_color");
        Register(WaterFogColor, "visual/water_fog_color");
        Register(WaterFogEndDistance, "visual/water_fog_end_distance");
        Register(SkyColor, "visual/sky_color");
        Register(AmbientParticles, "visual/ambient_particles");
        Register(BackgroundMusic, "audio/background_music");
        Register(MusicVolume, "audio/music_volume");
        Register(AmbientSounds, "audio/ambient_sounds");
        Register(IncreasedFireBurnout, "gameplay/increased_fire_burnout");
        Register(SnowGolemMelts, "gameplay/snow_golem_melts");
        Register(CanPillagerPatrolSpawn, "gameplay/can_pillager_patrol_spawn");
        Register(SkyLightLevel, "gameplay/sky_light_level");
    }

    private static void Register(IEnvironmentAttribute attribute, string name)
    {
        var id = Identifier.WithDefaultNamespace(name);
        if (BuiltInRegistries.ENVIRONMENT_ATTRIBUTE.ContainsKey(id)) return;
        Registry<IEnvironmentAttribute>.Register(BuiltInRegistries.ENVIRONMENT_ATTRIBUTE, id, attribute);
    }
}

//EnvironmentAttributeByIdCodec 按注册名解析环境属性
internal sealed class EnvironmentAttributeByIdCodec : ScalarCodec<IEnvironmentAttribute>
{
    public override DataResult<IEnvironmentAttribute> Parse<U>(DynamicOps<U> ops, U input)
        => IdentifierCodec.Instance.Parse(ops, input).FlatMap(id =>
        {
            var attribute = BuiltInRegistries.ENVIRONMENT_ATTRIBUTE.GetValue(id);
            return attribute is null
                ? DataResult<IEnvironmentAttribute>.Error(() => $"Unknown environment attribute: {id}")
                : DataResult<IEnvironmentAttribute>.Success(attribute);
        });

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, IEnvironmentAttribute value)
    {
        var id = BuiltInRegistries.ENVIRONMENT_ATTRIBUTE.GetKey(value);
        return id is null
            ? DataResult<U>.Error(() => "Environment attribute is not registered")
            : DataResult<U>.Success(ops.CreateString(id.Value.ToString()));
    }
}
