using NetCraft.Codec;
using NetCraft.Game.World.Level.LevelGen.Features.Impl;
using NetCraft.Game.World.Level.LevelGen.Features.Impl.Vegetation;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util.Random;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Misc;

//SpeleothemConfiguration 单根石笋配置 对应原版 SpeleothemConfiguration
//基石方块定根部材质 尖端方块定石笋本体 四个概率字段控制根部蔓延范围与长度
public sealed class SpeleothemConfiguration : FeatureConfiguration
{
    public static readonly Codec<SpeleothemConfiguration> Codec =
        RecordCodecBuilder.Of7<SpeleothemConfiguration, BlockState, BlockState, HolderSet<RegBlock>, float, float,
            float, float>(
            BlockStateCodec.Instance.FieldOf("base_block")
                .ForGetter<SpeleothemConfiguration, BlockState>(c => c.BaseBlock),
            BlockStateCodec.Instance.FieldOf("pointed_block")
                .ForGetter<SpeleothemConfiguration, BlockState>(c => c.PointedBlock),
            HolderSetCodecs.BlockSet.FieldOf("replaceable_blocks")
                .ForGetter<SpeleothemConfiguration, HolderSet<RegBlock>>(c => c.ReplaceableBlocks),
            Codecs.Float.OptionalFieldOf("chance_of_taller_generation", 0.2f)
                .ForGetter<SpeleothemConfiguration, float>(c => c.ChanceOfTallerGeneration),
            Codecs.Float.OptionalFieldOf("chance_of_directional_spread", 0.7f)
                .ForGetter<SpeleothemConfiguration, float>(c => c.ChanceOfDirectionalSpread),
            Codecs.Float.OptionalFieldOf("chance_of_spread_radius2", 0.5f)
                .ForGetter<SpeleothemConfiguration, float>(c => c.ChanceOfSpreadRadius2),
            Codecs.Float.OptionalFieldOf("chance_of_spread_radius3", 0.5f)
                .ForGetter<SpeleothemConfiguration, float>(c => c.ChanceOfSpreadRadius3),
            (baseBlock, pointedBlock, replaceableBlocks, chanceOfTallerGeneration, chanceOfDirectionalSpread,
                chanceOfSpreadRadius2, chanceOfSpreadRadius3) => new SpeleothemConfiguration(baseBlock, pointedBlock,
                replaceableBlocks, chanceOfTallerGeneration, chanceOfDirectionalSpread, chanceOfSpreadRadius2,
                chanceOfSpreadRadius3));

    public BlockState BaseBlock { get; }
    public BlockState PointedBlock { get; }
    public HolderSet<RegBlock> ReplaceableBlocks { get; }
    public float ChanceOfTallerGeneration { get; }
    public float ChanceOfDirectionalSpread { get; }
    public float ChanceOfSpreadRadius2 { get; }
    public float ChanceOfSpreadRadius3 { get; }

    public SpeleothemConfiguration(BlockState baseBlock, BlockState pointedBlock,
        HolderSet<RegBlock> replaceableBlocks, float chanceOfTallerGeneration, float chanceOfDirectionalSpread,
        float chanceOfSpreadRadius2, float chanceOfSpreadRadius3)
    {
        BaseBlock = baseBlock;
        PointedBlock = pointedBlock;
        ReplaceableBlocks = replaceableBlocks;
        ChanceOfTallerGeneration = chanceOfTallerGeneration;
        ChanceOfDirectionalSpread = chanceOfDirectionalSpread;
        ChanceOfSpreadRadius2 = chanceOfSpreadRadius2;
        ChanceOfSpreadRadius3 = chanceOfSpreadRadius3;
    }
}

//SpeleothemFeature 单根石笋特征 对应原版 SpeleothemFeature
//先判上下哪边是岩石定出尖端朝向 再在根部铺一小片基石 最后长出一到两格石笋
public sealed class SpeleothemFeature : Feature<SpeleothemConfiguration>
{
    private const string FeatureId = "speleothem";

    public static readonly SpeleothemFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new SpeleothemFeature());

    private SpeleothemFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), SpeleothemConfiguration.Codec) { }

    protected override bool Place(SpeleothemConfiguration config, FeaturePlaceContext context)
    {
        var level = context.Level;
        var pos = context.Origin;
        var random = context.Random;
        var tipDirection = GetTipDirection(level, pos, random, config);
        if (tipDirection is not { } tip) return false;
        var rootPos = pos.Offset(tip.Opposite);
        CreatePatchOfBaseBlocks(level, random, rootPos, config);
        //尖端前一格不是空腔或水时只能长一格 概率判定先掷 顺序与原版一致
        var height = random.NextFloat() >= config.ChanceOfTallerGeneration
            || !SpeleothemUtils.IsEmptyOrWater(level, pos.Offset(tip))
            ? 1
            : 2;
        SpeleothemUtils.GrowSpeleothem(level, pos, tip, height, false, config.BaseBlock.Owner,
            config.PointedBlock.Owner, config.ReplaceableBlocks);
        return true;
    }

    //GetTipDirection 按上下方块是不是岩石定石笋朝向 上下都是时随机取一个 对应原版 getTipDirection
    private static Direction? GetTipDirection(WorldGenRegion level, BlockPos pos, RandomSource random,
        SpeleothemConfiguration config)
    {
        var canPlaceAbove = SpeleothemUtils.IsBase(VegetationSupport.Get(level, pos.Offset(Direction.Up)),
            config.BaseBlock.Owner, config.ReplaceableBlocks);
        var canPlaceBelow = SpeleothemUtils.IsBase(VegetationSupport.Get(level, pos.Offset(Direction.Down)),
            config.BaseBlock.Owner, config.ReplaceableBlocks);
        if (canPlaceAbove && canPlaceBelow) return random.NextBoolean() ? Direction.Down : Direction.Up;
        if (canPlaceAbove) return Direction.Down;
        if (canPlaceBelow) return Direction.Up;
        return null;
    }

    //CreatePatchOfBaseBlocks 自根部向外铺基石 逐圈按概率扩散到半径三 对应原版 createPatchOfBaseBlocks
    private static void CreatePatchOfBaseBlocks(WorldGenRegion level, RandomSource random, BlockPos pos,
        SpeleothemConfiguration config)
    {
        SpeleothemUtils.PlaceBaseBlockIfPossible(level, pos, config.BaseBlock.Owner, config.ReplaceableBlocks);
        foreach (var direction in VegetationSupport.HorizontalPlane)
        {
            if (random.NextFloat() > config.ChanceOfDirectionalSpread) continue;
            var pos1 = pos.Offset(direction);
            SpeleothemUtils.PlaceBaseBlockIfPossible(level, pos1, config.BaseBlock.Owner, config.ReplaceableBlocks);
            if (random.NextFloat() > config.ChanceOfSpreadRadius2) continue;
            var pos2 = pos1.Offset(RandomDirection(random));
            SpeleothemUtils.PlaceBaseBlockIfPossible(level, pos2, config.BaseBlock.Owner, config.ReplaceableBlocks);
            if (random.NextFloat() > config.ChanceOfSpreadRadius3) continue;
            var pos3 = pos2.Offset(RandomDirection(random));
            SpeleothemUtils.PlaceBaseBlockIfPossible(level, pos3, config.BaseBlock.Owner, config.ReplaceableBlocks);
        }
    }

    //RandomDirection 六个方向里随机取一个 对应原版 Direction.getRandom
    private static Direction RandomDirection(RandomSource random) => Direction.Values[random.NextInt(6)];
}
