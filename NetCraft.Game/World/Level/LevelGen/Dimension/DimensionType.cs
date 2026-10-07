using NetCraft.Codec;
using NetCraft.Registry;
using NetCraft.Registry.Codec;
using GameIntProvider = NetCraft.Game.World.Level.LevelGen.IntProvider;

namespace NetCraft.Game.World.Level.LevelGen.Dimension;

//DimensionType dimension type, maps to vanilla net.minecraft.world.level.dimension.DimensionType
//Determines coordinate scale, height range, skylight and ceiling, and the light thresholds for monster spawning
//26.2 attributes/timelines/default_clock belong to the environment and clock systems; they carry no semantics at this stage and are skipped without error
//skybox and cardinal_light are rendering-side; store the identifiers as-is and convert to enums once rendering is wired up
public sealed record DimensionType(
    bool HasFixedTime,
    bool HasSkyLight,
    bool HasCeiling,
    bool HasEnderDragonFight,
    double CoordinateScale,
    int MinY,
    int Height,
    int LogicalHeight,
    Identifier? Infiniburn,
    float AmbientLight,
    MonsterSettings MonsterSettings,
    Identifier? Skybox,
    Identifier? CardinalLight) : NetCraft.Registry.DimensionType, RegistryIdentified
{
    //MinHeight minimum dimension height, maps to vanilla DimensionType.MIN_HEIGHT
    public const int MinHeight = 16;

    //MaxHeight maximum dimension height, maps to vanilla DimensionType.Y_SIZE
    public const int MaxHeight = 4064;

    //MinYLimit lower bound for min_y, maps to vanilla DimensionType.MIN_Y
    public const int MinYLimit = -2032;

    //DirectCodec element codec, maps to vanilla DIRECT_CODEC; only parses the fields that carry semantics here
    public static readonly Codec<DimensionType> DirectCodec = new DimensionTypeCodec();

    //ElementCodec registry element codec; the registry holds elements by marker interface
    public static readonly Codec<NetCraft.Registry.DimensionType> ElementCodec = DirectCodec.ComapFlatMap(
        type => DataResult<NetCraft.Registry.DimensionType>.Success(type),
        type => (DimensionType)type);

    //SectionCount number of sections; one section is 16 blocks tall
    public int SectionCount => Height / 16;

    //MinSectionY index of the lowest section, used directly when building the level
    public int MinSectionY => MinY / 16;

    //MaxY highest placeable height, exclusive upper bound
    public int MaxY => MinY + Height;

    //Id registry name; built-in constants set it at construction, data-driven loading fills it back from the file name
    public Identifier Id { get; private set; } = Identifier.WithDefaultNamespace("overworld");

    public void SetRegistryId(Identifier id) => Id = id;

    //GetTeleportationScale coordinate scale ratio between two dimensions, maps to vanilla getTeleportationScale
    //Computes the 1:8 ratio between overworld and nether; coordinates are multiplied by it on teleport
    public static double GetTeleportationScale(DimensionType from, DimensionType to)
        => from.CoordinateScale / to.CoordinateScale;
}

//MonsterSettings light thresholds for monster spawning, maps to vanilla DimensionType.MonsterSettings
//MonsterSpawnLightTest is an int provider sampled from a range: 0..7 in the overworld, a constant in the nether and the end
public sealed record MonsterSettings(GameIntProvider MonsterSpawnLightTest, int MonsterSpawnBlockLightLimit);

//DimensionTypeCodec dimension type codec, maps to vanilla DimensionType.DIRECT_CODEC
//Vanilla has more fields; only the semantic ones are parsed here, the rest do not affect the result
internal sealed class DimensionTypeCodec : AbstractMapCodec<DimensionType>
{
    public override DataResult<DimensionType> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var hasSkyLight = ReadBool(ops, input, "has_skylight");
        var hasCeiling = ReadBool(ops, input, "has_ceiling");
        var hasEnderDragonFight = ReadBool(ops, input, "has_ender_dragon_fight");
        var coordinateScale = ReadDouble(ops, input, "coordinate_scale");
        var minY = ReadInt(ops, input, "min_y");
        var height = ReadInt(ops, input, "height");
        var logicalHeight = ReadInt(ops, input, "logical_height");
        var ambientLight = ReadFloat(ops, input, "ambient_light");
        if (hasSkyLight is null || hasCeiling is null || hasEnderDragonFight is null
            || coordinateScale is null || minY is null || height is null || logicalHeight is null
            || ambientLight is null)
            return DataResult<DimensionType>.Error(() => "dimension type is missing required fields");
        //Height and min_y must both be divisible by 16 since sections are cut at 16; otherwise the level is misaligned
        if (height.Value < DimensionType.MinHeight || height.Value > DimensionType.MaxHeight
            || height.Value % 16 != 0)
            return DataResult<DimensionType>.Error(() =>
                $"dimension height must be a multiple of 16 within {DimensionType.MinHeight}..{DimensionType.MaxHeight}, got {height.Value}");
        if (minY.Value < DimensionType.MinYLimit || minY.Value % 16 != 0)
            return DataResult<DimensionType>.Error(() =>
                $"min_y must be a multiple of 16 and at least {DimensionType.MinYLimit}, got {minY.Value}");
        if (logicalHeight.Value < 0 || logicalHeight.Value > height.Value)
            return DataResult<DimensionType>.Error(() =>
                $"logical height cannot exceed the total height {logicalHeight.Value} > {height.Value}");

        var lightLevelTag = input.Get("monster_spawn_light_level");
        if (!lightLevelTag.IsPresent)
            return DataResult<DimensionType>.Error(() => "dimension type is missing monster_spawn_light_level");
        var lightLevel = IntProviders.Codec.Parse(ops, lightLevelTag.Get());
        if (!lightLevel.Result().IsPresent)
            return DataResult<DimensionType>.Error(() => "failed to parse monster_spawn_light_level");
        var blockLightLimit = ReadInt(ops, input, "monster_spawn_block_light_limit");
        if (blockLightLimit is null)
            return DataResult<DimensionType>.Error(() => "dimension type is missing monster_spawn_block_light_limit");

        return DataResult<DimensionType>.Success(new DimensionType(
            ReadBool(ops, input, "has_fixed_time") ?? false,
            hasSkyLight.Value,
            hasCeiling.Value,
            hasEnderDragonFight.Value,
            coordinateScale.Value,
            minY.Value,
            height.Value,
            logicalHeight.Value,
            ReadTagId(ops, input, "infiniburn"),
            ambientLight.Value,
            new MonsterSettings(lightLevel.GetOrThrow(), blockLightLimit.Value),
            ReadIdentifier(ops, input, "skybox"),
            ReadIdentifier(ops, input, "cardinal_light")));
    }

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, DimensionType value, RecordBuilder<U> builder)
    {
        builder.Add("has_fixed_time", ops.CreateBoolean(value.HasFixedTime));
        builder.Add("has_skylight", ops.CreateBoolean(value.HasSkyLight));
        builder.Add("has_ceiling", ops.CreateBoolean(value.HasCeiling));
        builder.Add("has_ender_dragon_fight", ops.CreateBoolean(value.HasEnderDragonFight));
        builder.Add("coordinate_scale", ops.CreateDouble(value.CoordinateScale));
        builder.Add("min_y", ops.CreateInt(value.MinY));
        builder.Add("height", ops.CreateInt(value.Height));
        builder.Add("logical_height", ops.CreateInt(value.LogicalHeight));
        if (value.Infiniburn is { } infiniburn) builder.Add("infiniburn", ops.CreateString($"#{infiniburn}"));
        builder.Add("ambient_light", ops.CreateFloat(value.AmbientLight));
        builder.Add("monster_spawn_light_level",
            IntProviders.Codec.EncodeStart(ops, value.MonsterSettings.MonsterSpawnLightTest).GetOrThrow());
        builder.Add("monster_spawn_block_light_limit",
            ops.CreateInt(value.MonsterSettings.MonsterSpawnBlockLightLimit));
        if (value.Skybox is { } skybox) builder.Add("skybox", ops.CreateString(skybox.ToString()));
        if (value.CardinalLight is { } cardinalLight)
            builder.Add("cardinal_light", ops.CreateString(cardinalLight.ToString()));
        return builder;
    }

    //ReadBool read a boolean field; returns null when missing
    private static bool? ReadBool<U>(DynamicOps<U> ops, MapLike<U> input, string name)
    {
        var tag = input.Get(name);
        if (!tag.IsPresent) return null;
        var value = ops.GetBooleanValue(tag.Get());
        return value.Result().IsPresent ? value.GetOrThrow() : null;
    }

    //ReadInt read an int field; returns null when missing or not a number
    private static int? ReadInt<U>(DynamicOps<U> ops, MapLike<U> input, string name)
    {
        var tag = input.Get(name);
        if (!tag.IsPresent) return null;
        var value = ops.GetNumberValue(tag.Get());
        return value.Result().IsPresent ? (int)value.GetOrThrow() : null;
    }

    //ReadDouble read a double field; returns null when missing or not a number
    private static double? ReadDouble<U>(DynamicOps<U> ops, MapLike<U> input, string name)
    {
        var tag = input.Get(name);
        if (!tag.IsPresent) return null;
        var value = ops.GetNumberValue(tag.Get());
        return value.Result().IsPresent ? value.GetOrThrow() : null;
    }

    //ReadFloat read a float field; returns null when missing or not a number
    private static float? ReadFloat<U>(DynamicOps<U> ops, MapLike<U> input, string name)
    {
        var tag = input.Get(name);
        if (!tag.IsPresent) return null;
        var value = ops.GetNumberValue(tag.Get());
        return value.Result().IsPresent ? (float)value.GetOrThrow() : null;
    }

    //ReadIdentifier read an identifier field; returns null when missing or invalid
    private static Identifier? ReadIdentifier<U>(DynamicOps<U> ops, MapLike<U> input, string name)
    {
        var text = ReadString(ops, input, name);
        return text is null ? null : Identifier.TryParse(text);
    }

    //ReadTagId read a block tag reference shaped like #minecraft:infiniburn_nether; strips the leading hash and stores an identifier
    private static Identifier? ReadTagId<U>(DynamicOps<U> ops, MapLike<U> input, string name)
    {
        var text = ReadString(ops, input, name);
        if (text is null) return null;
        return Identifier.TryParse(text.StartsWith('#') ? text[1..] : text);
    }

    //ReadString read a string field; returns null when missing or not a string
    private static string? ReadString<U>(DynamicOps<U> ops, MapLike<U> input, string name)
    {
        var tag = input.Get(name);
        if (!tag.IsPresent) return null;
        var text = ops.GetStringValue(tag.Get());
        return text.Result().IsPresent ? text.GetOrThrow() : null;
    }
}
