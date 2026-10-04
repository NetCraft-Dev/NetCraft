using NetCraft.Codec;
using NetCraft.Registry;
using NetCraft.Registry.Codec;
using GameIntProvider = NetCraft.Game.World.Level.LevelGen.IntProvider;

namespace NetCraft.Game.World.Level.LevelGen.Dimension;

//DimensionType 维度类型 对应原版 net.minecraft.world.level.dimension.DimensionType
//决定维度的坐标缩放 高度范围 天光与天花板 以及怪物生成的光照门限
//26.2 的 attributes/timelines/default_clock 属环境属性与时钟体系 本阶段不参与语义 解析时跳过不报错
//skybox 与 cardinal_light 偏渲染侧 先按原样存标识符 渲染接入时再转枚举
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
    //MinHeight 维度最小高度 对应原版 DimensionType.MIN_HEIGHT
    public const int MinHeight = 16;

    //MaxHeight 维度最大高度 对应原版 DimensionType.Y_SIZE
    public const int MaxHeight = 4064;

    //MinYLimit 最低高度下限 对应原版 DimensionType.MIN_Y
    public const int MinYLimit = -2032;

    //DirectCodec 元素 codec 对应原版 DIRECT_CODEC 只解析本作有语义的字段
    public static readonly Codec<DimensionType> DirectCodec = new DimensionTypeCodec();

    //ElementCodec 注册表元素 codec 注册表按标记接口持有元素
    public static readonly Codec<NetCraft.Registry.DimensionType> ElementCodec = DirectCodec.ComapFlatMap(
        type => DataResult<NetCraft.Registry.DimensionType>.Success(type),
        type => (DimensionType)type);

    //SectionCount 区段数量 一格段 16 格高
    public int SectionCount => Height / 16;

    //MinSectionY 最低区段序号 供关卡构造直接使用
    public int MinSectionY => MinY / 16;

    //MaxY 最高可放置高度 开区间上界
    public int MaxY => MinY + Height;

    //Id 注册名 内置常量在构造处给定 数据驱动装载由注册表按文件名回填
    public Identifier Id { get; private set; } = Identifier.WithDefaultNamespace("overworld");

    public void SetRegistryId(Identifier id) => Id = id;

    //GetTeleportationScale 两维度之间的坐标缩放比 对应原版 getTeleportationScale
    //主世界与下界 1:8 靠它算 传送时坐标要乘这个比
    public static double GetTeleportationScale(DimensionType from, DimensionType to)
        => from.CoordinateScale / to.CoordinateScale;
}

//MonsterSettings 怪物生成的光照门限 对应原版 DimensionType.MonsterSettings
//MonsterSpawnLightTest 是范围内取值的整数提供者 主世界是 0..7 的下界与末地是常量
public sealed record MonsterSettings(GameIntProvider MonsterSpawnLightTest, int MonsterSpawnBlockLightLimit);

//DimensionTypeCodec 维度类型 codec 对应原版 DimensionType.DIRECT_CODEC
//原版字段更多 本作只解有语义的那批 其余键读不读都不影响解析结果
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
            return DataResult<DimensionType>.Error(() => "维度类型缺少必填字段");
        //高度与最低高度都要能被 16 整除 区段按 16 切 不满足会在关卡构造时错位
        if (height.Value < DimensionType.MinHeight || height.Value > DimensionType.MaxHeight
            || height.Value % 16 != 0)
            return DataResult<DimensionType>.Error(() =>
                $"维度高度必须是 16 的倍数且落在 {DimensionType.MinHeight}..{DimensionType.MaxHeight} 实际 {height.Value}");
        if (minY.Value < DimensionType.MinYLimit || minY.Value % 16 != 0)
            return DataResult<DimensionType>.Error(() =>
                $"最低高度必须是 16 的倍数且不小于 {DimensionType.MinYLimit} 实际 {minY.Value}");
        if (logicalHeight.Value < 0 || logicalHeight.Value > height.Value)
            return DataResult<DimensionType>.Error(() =>
                $"逻辑高度不能超过总高度 {logicalHeight.Value} > {height.Value}");

        var lightLevelTag = input.Get("monster_spawn_light_level");
        if (!lightLevelTag.IsPresent)
            return DataResult<DimensionType>.Error(() => "维度类型缺 monster_spawn_light_level");
        var lightLevel = IntProviders.Codec.Parse(ops, lightLevelTag.Get());
        if (!lightLevel.Result().IsPresent)
            return DataResult<DimensionType>.Error(() => "monster_spawn_light_level 解析失败");
        var blockLightLimit = ReadInt(ops, input, "monster_spawn_block_light_limit");
        if (blockLightLimit is null)
            return DataResult<DimensionType>.Error(() => "维度类型缺 monster_spawn_block_light_limit");

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

    //ReadBool 读布尔字段缺失返回 null
    private static bool? ReadBool<U>(DynamicOps<U> ops, MapLike<U> input, string name)
    {
        var tag = input.Get(name);
        if (!tag.IsPresent) return null;
        var value = ops.GetBooleanValue(tag.Get());
        return value.Result().IsPresent ? value.GetOrThrow() : null;
    }

    //ReadInt 读整数字段缺失或非数字返回 null
    private static int? ReadInt<U>(DynamicOps<U> ops, MapLike<U> input, string name)
    {
        var tag = input.Get(name);
        if (!tag.IsPresent) return null;
        var value = ops.GetNumberValue(tag.Get());
        return value.Result().IsPresent ? (int)value.GetOrThrow() : null;
    }

    //ReadDouble 读双精度字段缺失或非数字返回 null
    private static double? ReadDouble<U>(DynamicOps<U> ops, MapLike<U> input, string name)
    {
        var tag = input.Get(name);
        if (!tag.IsPresent) return null;
        var value = ops.GetNumberValue(tag.Get());
        return value.Result().IsPresent ? value.GetOrThrow() : null;
    }

    //ReadFloat 读浮点字段缺失或非数字返回 null
    private static float? ReadFloat<U>(DynamicOps<U> ops, MapLike<U> input, string name)
    {
        var tag = input.Get(name);
        if (!tag.IsPresent) return null;
        var value = ops.GetNumberValue(tag.Get());
        return value.Result().IsPresent ? (float)value.GetOrThrow() : null;
    }

    //ReadIdentifier 读标识符字段缺失或非法返回 null
    private static Identifier? ReadIdentifier<U>(DynamicOps<U> ops, MapLike<U> input, string name)
    {
        var text = ReadString(ops, input, name);
        return text is null ? null : Identifier.TryParse(text);
    }

    //ReadTagId 读方块标签引用 字段形如 #minecraft:infiniburn_nether 去掉井号存标识符
    private static Identifier? ReadTagId<U>(DynamicOps<U> ops, MapLike<U> input, string name)
    {
        var text = ReadString(ops, input, name);
        if (text is null) return null;
        return Identifier.TryParse(text.StartsWith('#') ? text[1..] : text);
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
