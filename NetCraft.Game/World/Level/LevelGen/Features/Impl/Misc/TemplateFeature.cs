using NetCraft.Codec;
using NetCraft.Game.World.Level.LevelGen.Features.Impl;
using NetCraft.Game.World.Level.LevelGen.Structure;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.Codec;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Misc;

//TemplateFeatureConfiguration 结构模板配置 对应原版 TemplateFeatureConfiguration
//权重列表里每一项是一份模板 模板可自带允许的旋转集合
public sealed class TemplateFeatureConfiguration : FeatureConfiguration
{
    public static readonly Codec<TemplateFeatureConfiguration> Codec =
        new SingleFieldMapCodec<TemplateFeatureConfiguration, WeightedList<TemplateEntry>>(
            new WeightedListCodec<TemplateEntry>(TemplateEntry.Codec).FieldOf("templates"),
            templates => new TemplateFeatureConfiguration(templates),
            config => config.Templates);

    public WeightedList<TemplateEntry> Templates { get; }

    public TemplateFeatureConfiguration(WeightedList<TemplateEntry> templates) => Templates = templates;
}

//TemplateEntry 一条模板条目 对应原版 TemplateFeatureConfiguration.TemplateEntry
public sealed class TemplateEntry
{
    //DefaultRotations 四个旋转 声明序与 Rotation.values 一致
    //必须先于 Codec 声明 静态字段按文本顺序初始化 否则构造 codec 时取到 null
    private static readonly IReadOnlyList<Rotation> DefaultRotations =
        new[] { Rotation.None, Rotation.Clockwise90, Rotation.Clockwise180, Rotation.Counterclockwise90 };

    public static readonly Codec<TemplateEntry> Codec =
        RecordCodecBuilder.Of2<TemplateEntry, Identifier, IReadOnlyList<Rotation>>(
            IdentifierCodec.Instance.FieldOf("id").ForGetter<TemplateEntry, Identifier>(e => e.Template),
            RotationCodec.Instance.ListOf().OptionalFieldOf("rotations", DefaultRotations)
                .ForGetter<TemplateEntry, IReadOnlyList<Rotation>>(e => e.Rotations),
            (template, rotations) => new TemplateEntry(template, rotations));

    public Identifier Template { get; }
    public IReadOnlyList<Rotation> Rotations { get; }

    public TemplateEntry(Identifier template, IReadOnlyList<Rotation> rotations)
    {
        Template = template;
        Rotations = rotations;
    }
}

//TemplateFeature 结构模板特征 对应原版 TemplateFeature
//按权重取一份模板与一个旋转 把模板居中放到原点 供硫泉这类预置结构使用
public sealed class TemplateFeature : Feature<TemplateFeatureConfiguration>
{
    private const string FeatureId = "template";

    public static readonly TemplateFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new TemplateFeature());

    //TemplateManager 结构模板管理器 由世界装配注入
    //放置上下文只拿到 WorldGenRegion 拿不到资源包 没注入时读不出模板 与原版读服务端管理器等价
    public static StructureTemplateManager? TemplateManager { get; set; }

    private TemplateFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), TemplateFeatureConfiguration.Codec) { }

    protected override bool Place(TemplateFeatureConfiguration config, FeaturePlaceContext context)
    {
        var random = context.Random;
        var level = context.Level;
        var entry = config.Templates.GetRandomOrThrow(random);
        var rotation = entry.Rotations[random.NextInt(entry.Rotations.Count)];
        //管理器没注入时这两个随机数已经消耗掉 同种子的后续特征不会漂
        var manager = TemplateManager;
        if (manager is null) return false;
        var template = manager.GetOrLoad(entry.Template);
        if (template is null) return false;
        var offsetX = GetRotatedOffset(rotation, Direction.Axis.X, template);
        var offsetZ = GetRotatedOffset(rotation, Direction.Axis.Z, template);
        var pos = context.Origin.Offset(offsetX).Offset(offsetZ);
        var settings = new StructurePlaceSettings().SetRotation(rotation).SetRandom(random);
        return template.PlaceInWorld(level, pos, pos, settings, random);
    }

    //GetRotatedOffset 模板按旋转居中时的偏移量 对应原版 getRotatedOffset
    private static Vec3i GetRotatedOffset(Rotation rotation, Direction.Axis axis, StructureTemplate template)
    {
        var direction = rotation.Rotate(NegativeOf(axis));
        var size = axis.Choose(template.Size.X, template.Size.Y, template.Size.Z);
        return new Vec3i(direction.StepX, direction.StepY, direction.StepZ).Multiply(size / 2);
    }

    //NegativeOf 取该轴上的负方向 对应原版 Direction.Axis.getNegative
    private static Direction NegativeOf(Direction.Axis axis) => axis switch
    {
        Direction.Axis.X => Direction.West,
        Direction.Axis.Y => Direction.Down,
        _ => Direction.North,
    };
}

//RotationCodec 旋转编解码 对应原版 Rotation.CODEC
//JSON 形态是 none/clockwise_90/180/counterclockwise_90
internal sealed class RotationCodec : ScalarCodec<Rotation>
{
    public static readonly RotationCodec Instance = new();

    public override DataResult<Rotation> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (!text.Result().IsPresent) return DataResult<Rotation>.Error(() => "旋转必须是字符串");
        return StructureTransforms.TryParseRotation(text.GetOrThrow()) is { } rotation
            ? DataResult<Rotation>.Success(rotation)
            : DataResult<Rotation>.Error(() => $"未知的旋转: {text.GetOrThrow()}");
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Rotation value)
        => DataResult<U>.Success(ops.CreateString(value.Name()));
}
