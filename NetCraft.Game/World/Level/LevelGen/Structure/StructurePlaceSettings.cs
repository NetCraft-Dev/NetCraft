using NetCraft.Primitives;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//LiquidSettings liquid handling during placement, maps to vanilla LiquidSettings
//ApplyWaterlogging spreads water through waterloggable blocks in the structure; IgnoreWaterlogging ignores liquids entirely
public enum LiquidSettings
{
    ApplyWaterlogging,
    IgnoreWaterlogging,
}

//StructurePlaceSettings structure placement settings, maps to the identically named vanilla class
//Carries mirror / rotation / rotation pivot / bounding box / processor chain and more; passed wholesale to the template during placement
public sealed class StructurePlaceSettings
{
    private readonly List<StructureProcessor> _processors = new();

    public Mirror Mirror { get; private set; } = Mirror.None;

    public Rotation Rotation { get; private set; } = Rotation.None;

    //RotationPivot center of rotation and mirroring, defaults to the origin
    public BlockPos RotationPivot { get; private set; } = BlockPos.Zero;

    public bool IgnoreEntities { get; private set; }

    public BoundingBoxInt? BoundingBox { get; private set; }

    public LiquidSettings LiquidSettings { get; private set; } = LiquidSettings.ApplyWaterlogging;

    //Random explicitly specified random source; when null it is derived from the placement position
    public RandomSource? Random { get; private set; }

    public int Palette { get; private set; }

    //KnownShape skips neighbor shape backfill when the final shape is known
    public bool KnownShape { get; private set; }

    public bool FinalizeEntities { get; private set; }

    public IReadOnlyList<StructureProcessor> Processors => _processors;

    //Copy duplicates the settings; each block needs its own bounding box when a piece is placed chunk by chunk
    public StructurePlaceSettings Copy()
    {
        var copy = new StructurePlaceSettings
        {
            Mirror = Mirror,
            Rotation = Rotation,
            RotationPivot = RotationPivot,
            IgnoreEntities = IgnoreEntities,
            BoundingBox = BoundingBox,
            LiquidSettings = LiquidSettings,
            Random = Random,
            Palette = Palette,
            KnownShape = KnownShape,
            FinalizeEntities = FinalizeEntities,
        };
        copy._processors.AddRange(_processors);
        return copy;
    }

    public StructurePlaceSettings SetMirror(Mirror mirror)
    {
        Mirror = mirror;
        return this;
    }

    public StructurePlaceSettings SetRotation(Rotation rotation)
    {
        Rotation = rotation;
        return this;
    }

    public StructurePlaceSettings SetRotationPivot(BlockPos rotationPivot)
    {
        RotationPivot = rotationPivot;
        return this;
    }

    public StructurePlaceSettings SetIgnoreEntities(bool ignoreEntities)
    {
        IgnoreEntities = ignoreEntities;
        return this;
    }

    public StructurePlaceSettings SetBoundingBox(BoundingBoxInt? boundingBox)
    {
        BoundingBox = boundingBox;
        return this;
    }

    public StructurePlaceSettings SetRandom(RandomSource? random)
    {
        Random = random;
        return this;
    }

    public StructurePlaceSettings SetLiquidSettings(LiquidSettings liquidSettings)
    {
        LiquidSettings = liquidSettings;
        return this;
    }

    public StructurePlaceSettings SetPalette(int palette)
    {
        Palette = palette;
        return this;
    }

    public StructurePlaceSettings SetKnownShape(bool knownShape)
    {
        KnownShape = knownShape;
        return this;
    }

    public StructurePlaceSettings SetFinalizeEntities(bool finalizeEntities)
    {
        FinalizeEntities = finalizeEntities;
        return this;
    }

    public StructurePlaceSettings ClearProcessors()
    {
        _processors.Clear();
        return this;
    }

    public StructurePlaceSettings AddProcessor(StructureProcessor processor)
    {
        _processors.Add(processor);
        return this;
    }

    public StructurePlaceSettings PopProcessor(StructureProcessor processor)
    {
        _processors.Remove(processor);
        return this;
    }

    //ShouldApplyWaterlogging whether to spread liquid through waterloggable blocks
    public bool ShouldApplyWaterlogging() => LiquidSettings == LiquidSettings.ApplyWaterlogging;

    //GetRandom returns the random source; explicit wins, otherwise derived from the placement position, degrading to system time when the position is null
    public RandomSource GetRandom(BlockPos? pos)
    {
        if (Random is not null) return Random;
        if (pos is null) return RandomSource.Create(Environment.TickCount64);
        return RandomSource.Create(GetSeed(pos.Value));
    }

    //GetRandomPalette picks one of several palettes from a coordinate-derived random, maps to vanilla getRandomPalette
    public StructureTemplatePalette GetRandomPalette(IReadOnlyList<StructureTemplatePalette> palettes, BlockPos? pos)
    {
        if (palettes.Count == 0) throw new InvalidOperationException("structure template has no palette");
        return palettes[GetRandom(pos).NextInt(palettes.Count)];
    }

    //GetSeed coordinate-derived seed, same formula as Mth.getSeed, so the same position always picks the same palette
    public static long GetSeed(BlockPos pos)
        => pos.X * 3129871L ^ pos.Z * 116129781L ^ pos.Y;
}
