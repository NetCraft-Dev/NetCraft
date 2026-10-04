using NetCraft.Primitives;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//LiquidSettings 放置时的液体处理方式 对应原版 LiquidSettings
//ApplyWaterlogging 会把结构里的水按含水的方块铺开 IgnoreWaterlogging 直接忽略液体
public enum LiquidSettings
{
    ApplyWaterlogging,
    IgnoreWaterlogging,
}

//StructurePlaceSettings 结构放置设置 对应原版同名类
//承载镜像/旋转/旋转中心/包围盒/处理器链等 放置时整份传给模板
public sealed class StructurePlaceSettings
{
    private readonly List<StructureProcessor> _processors = new();

    public Mirror Mirror { get; private set; } = Mirror.None;

    public Rotation Rotation { get; private set; } = Rotation.None;

    //RotationPivot 旋转与镜像的中心 默认原点
    public BlockPos RotationPivot { get; private set; } = BlockPos.Zero;

    public bool IgnoreEntities { get; private set; }

    public BoundingBoxInt? BoundingBox { get; private set; }

    public LiquidSettings LiquidSettings { get; private set; } = LiquidSettings.ApplyWaterlogging;

    //Random 显式指定的随机源 为空时按放置坐标派生
    public RandomSource? Random { get; private set; }

    public int Palette { get; private set; }

    //KnownShape 已知最终形状时跳过邻居形状回填
    public bool KnownShape { get; private set; }

    public bool FinalizeEntities { get; private set; }

    public IReadOnlyList<StructureProcessor> Processors => _processors;

    //Copy 复制一份设置 结构片段逐区块放置时每块都要独立的包围盒
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

    //ShouldApplyWaterlogging 是否按含水方块铺液体
    public bool ShouldApplyWaterlogging() => LiquidSettings == LiquidSettings.ApplyWaterlogging;

    //GetRandom 取随机源 显式指定优先 否则按放置坐标派生 坐标为 null 时退化到系统时间
    public RandomSource GetRandom(BlockPos? pos)
    {
        if (Random is not null) return Random;
        if (pos is null) return RandomSource.Create(Environment.TickCount64);
        return RandomSource.Create(GetSeed(pos.Value));
    }

    //GetRandomPalette 从多个调色板里按坐标派生的随机数挑一个 对应原版 getRandomPalette
    public StructureTemplatePalette GetRandomPalette(IReadOnlyList<StructureTemplatePalette> palettes, BlockPos? pos)
    {
        if (palettes.Count == 0) throw new InvalidOperationException("结构模板没有调色板");
        return palettes[GetRandom(pos).NextInt(palettes.Count)];
    }

    //GetSeed 坐标派生的种子 与 Mth.getSeed 同式 保证同一坐标每次挑同一个调色板
    public static long GetSeed(BlockPos pos)
        => pos.X * 3129871L ^ pos.Z * 116129781L ^ pos.Y;
}
