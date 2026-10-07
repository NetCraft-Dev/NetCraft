using NetCraft.Codec;
using NetCraft.Game.World.Level.LevelGen.Features.Impl;
using NetCraft.Game.World.Level.LevelGen.Structure;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.Codec;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Misc;

//TemplateFeatureConfiguration structure template configuration, maps to vanilla TemplateFeatureConfiguration
//Each weighted entry is one template; a template may carry its own set of allowed rotations
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

//TemplateEntry a template entry, maps to vanilla TemplateFeatureConfiguration.TemplateEntry
public sealed class TemplateEntry
{
    //DefaultRotations the four rotations, declared in the same order as Rotation.values
    //Must be declared before Codec: static fields initialize in textual order, otherwise the codec would see null
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

//TemplateFeature structure template feature, maps to vanilla TemplateFeature
//Picks a weighted template and a rotation, centers the template on the origin; used by preset structures such as sulfur springs
public sealed class TemplateFeature : Feature<TemplateFeatureConfiguration>
{
    private const string FeatureId = "template";

    public static readonly TemplateFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new TemplateFeature());

    //TemplateManager structure template manager, injected by world assembly
    //The placement context only gets a WorldGenRegion, not the resource pack, so without injection templates cannot be read; equivalent to vanilla reading the server manager
    public static StructureTemplateManager? TemplateManager { get; set; }

    private TemplateFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), TemplateFeatureConfiguration.Codec) { }

    protected override bool Place(TemplateFeatureConfiguration config, FeaturePlaceContext context)
    {
        var random = context.Random;
        var level = context.Level;
        var entry = config.Templates.GetRandomOrThrow(random);
        var rotation = entry.Rotations[random.NextInt(entry.Rotations.Count)];
        //When the manager is not injected these two random values are already consumed, so later features do not drift for the same seed
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

    //GetRotatedOffset centering offset of the template for its rotation, maps to vanilla getRotatedOffset
    private static Vec3i GetRotatedOffset(Rotation rotation, Direction.Axis axis, StructureTemplate template)
    {
        var direction = rotation.Rotate(NegativeOf(axis));
        var size = axis.Choose(template.Size.X, template.Size.Y, template.Size.Z);
        return new Vec3i(direction.StepX, direction.StepY, direction.StepZ).Multiply(size / 2);
    }

    //NegativeOf the negative direction along the axis, maps to vanilla Direction.Axis.getNegative
    private static Direction NegativeOf(Direction.Axis axis) => axis switch
    {
        Direction.Axis.X => Direction.West,
        Direction.Axis.Y => Direction.Down,
        _ => Direction.North,
    };
}

//RotationCodec rotation codec, maps to vanilla Rotation.CODEC
//JSON form is none/clockwise_90/180/counterclockwise_90
internal sealed class RotationCodec : ScalarCodec<Rotation>
{
    public static readonly RotationCodec Instance = new();

    public override DataResult<Rotation> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (!text.Result().IsPresent) return DataResult<Rotation>.Error(() => "rotation must be a string");
        return StructureTransforms.TryParseRotation(text.GetOrThrow()) is { } rotation
            ? DataResult<Rotation>.Success(rotation)
            : DataResult<Rotation>.Error(() => $"unknown rotation: {text.GetOrThrow()}");
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Rotation value)
        => DataResult<U>.Success(ops.CreateString(value.Name()));
}
