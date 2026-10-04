using NetCraft.Codec;
using NetCraft.Game.World.Level.LevelGen.Features.Impl.Vegetation;
using NetCraft.Game.World.Level.LevelGen.Structure;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;
using GameStructureProcessorList = NetCraft.Game.World.Level.LevelGen.Structure.StructureProcessorList;
using RegistryProcessorList = NetCraft.Registry.StructureProcessorList;
using StructureProcessor = NetCraft.Game.World.Level.LevelGen.Structure.StructureProcessor;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Misc;

//FossilFeatureConfiguration 化石配置 对应原版 FossilFeatureConfiguration
//化石本体与覆盖层结构一一对应 同一序号配一对
public sealed class FossilFeatureConfiguration : FeatureConfiguration
{
    public static readonly Codec<FossilFeatureConfiguration> Codec =
        RecordCodecBuilder.Of5<FossilFeatureConfiguration, IReadOnlyList<Identifier>, IReadOnlyList<Identifier>,
            Holder<RegistryProcessorList>, Holder<RegistryProcessorList>, int>(
            StructurePoolCodecs.TemplateLocation.ListOf().FieldOf("fossil_structures")
                .ForGetter<FossilFeatureConfiguration, IReadOnlyList<Identifier>>(c => c.FossilStructures),
            StructurePoolCodecs.TemplateLocation.ListOf().FieldOf("overlay_structures")
                .ForGetter<FossilFeatureConfiguration, IReadOnlyList<Identifier>>(c => c.OverlayStructures),
            StructurePoolCodecs.ProcessorListRef.FieldOf("fossil_processors")
                .ForGetter<FossilFeatureConfiguration, Holder<RegistryProcessorList>>(c => c.FossilProcessors),
            StructurePoolCodecs.ProcessorListRef.FieldOf("overlay_processors")
                .ForGetter<FossilFeatureConfiguration, Holder<RegistryProcessorList>>(c => c.OverlayProcessors),
            Codecs.Int.FieldOf("max_empty_corners_allowed")
                .ForGetter<FossilFeatureConfiguration, int>(c => c.MaxEmptyCornersAllowed),
            (fossilStructures, overlayStructures, fossilProcessors, overlayProcessors, maxEmptyCornersAllowed) =>
                new FossilFeatureConfiguration(fossilStructures, overlayStructures, fossilProcessors,
                    overlayProcessors, maxEmptyCornersAllowed));

    public IReadOnlyList<Identifier> FossilStructures { get; }
    public IReadOnlyList<Identifier> OverlayStructures { get; }
    public Holder<RegistryProcessorList> FossilProcessors { get; }
    public Holder<RegistryProcessorList> OverlayProcessors { get; }
    public int MaxEmptyCornersAllowed { get; }

    public FossilFeatureConfiguration(IReadOnlyList<Identifier> fossilStructures,
        IReadOnlyList<Identifier> overlayStructures, Holder<RegistryProcessorList> fossilProcessors,
        Holder<RegistryProcessorList> overlayProcessors, int maxEmptyCornersAllowed)
    {
        FossilStructures = fossilStructures;
        OverlayStructures = overlayStructures;
        FossilProcessors = fossilProcessors;
        OverlayProcessors = overlayProcessors;
        MaxEmptyCornersAllowed = maxEmptyCornersAllowed;
    }
}

//FossilFeature 化石特征 对应原版 FossilFeature
//按区块范围取一块沙地高度带 从模板里挑一具骨架贴到地面下方 再叠一层矿石覆盖结构
public sealed class FossilFeature : Feature<FossilFeatureConfiguration>
{
    private const string FeatureId = "fossil";

    public static readonly FossilFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new FossilFeature());

    //TemplateManager 结构模板管理器 由世界装配注入
    //特征放置上下文只拿到 WorldGenRegion 拿不到资源包 没注入就读不出 minecraft:fossil/... 模板
    public static StructureTemplateManager? TemplateManager { get; set; }

    private FossilFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), FossilFeatureConfiguration.Codec) { }

    protected override bool Place(FossilFeatureConfiguration config, FeaturePlaceContext context)
    {
        var random = context.Random;
        var level = context.Level;
        var origin = context.Origin;
        //模板管理器为空时也要先把这两个随机数消耗掉 否则同种子的后续特征会漂
        var rotation = StructureTransforms.GetRandomRotation(random);
        var fossilIndex = random.NextInt(config.FossilStructures.Count);
        var manager = TemplateManager;
        if (manager is null) return false;
        var fossilBase = manager.GetOrLoad(config.FossilStructures[fossilIndex]);
        var fossilOverlay = manager.GetOrLoad(config.OverlayStructures[fossilIndex]);
        if (fossilBase is null || fossilOverlay is null) return false;
        var chunkX = origin.X >> 4;
        var chunkZ = origin.Z >> 4;
        var minY = level.MinSectionY * 16;
        var maxY = (level.MaxSectionY + 1) * 16 - 1;
        var boundingBox = new BoundingBoxInt(chunkX * 16 - 16, minY, chunkZ * 16 - 16,
            chunkX * 16 + 15 + 16, maxY, chunkZ * 16 + 15 + 16);
        var settings = new StructurePlaceSettings()
            .SetRotation(rotation)
            .SetBoundingBox(boundingBox)
            .SetRandom(random);
        var size = fossilBase.GetSize(rotation);
        var lowCorner = origin.Offset(-size.X / 2, 0, -size.Z / 2);
        var lowestSurfaceY = origin.Y;
        for (var xScan = 0; xScan < size.X; xScan++)
        {
            for (var zScan = 0; zScan < size.Z; zScan++)
            {
                var surface = level.GetHeight(Heightmap.Types.OceanFloorWg, lowCorner.X + xScan,
                    lowCorner.Z + zScan);
                if (surface < lowestSurfaceY) lowestSurfaceY = surface;
            }
        }
        var targetY = Math.Max((lowestSurfaceY - 15) - random.NextInt(10), minY + 10);
        var targetPos = StructureTemplate.GetZeroPositionWithTransform(
            new BlockPos(lowCorner.X, targetY, lowCorner.Z), Mirror.None, rotation, size.X, size.Z);
        if (CountEmptyCorners(level, fossilBase.GetBoundingBox(settings, targetPos)) > config.MaxEmptyCornersAllowed)
            return false;
        settings.ClearProcessors();
        foreach (var processor in ProcessorsOf(config.FossilProcessors)) settings.AddProcessor(processor);
        fossilBase.PlaceInWorld(level, targetPos, targetPos, settings, random);
        settings.ClearProcessors();
        foreach (var processor in ProcessorsOf(config.OverlayProcessors)) settings.AddProcessor(processor);
        fossilOverlay.PlaceInWorld(level, targetPos, targetPos, settings, random);
        return true;
    }

    //ProcessorsOf 取处理器列表引用里的处理器 未绑定时按空列表处理
    private static IReadOnlyList<StructureProcessor> ProcessorsOf(Holder<RegistryProcessorList> holder)
        => holder.IsBound() && holder.Value is GameStructureProcessorList list
            ? list.Processors
            : Array.Empty<StructureProcessor>();

    //CountEmptyCorners 包围盒八个角里有几个是空气或液体 对应原版 countEmptyCorners
    //角点顺序照原版 只统计数量所以顺序本身不影响结果
    private static int CountEmptyCorners(WorldGenRegion level, BoundingBoxInt bounds)
    {
        var count = 0;
        foreach (var x in new[] { bounds.MaxX, bounds.MinX })
        foreach (var y in new[] { bounds.MaxY, bounds.MinY })
        foreach (var z in new[] { bounds.MaxZ, bounds.MinZ })
        {
            var state = level.GetBlockState(x, y, z);
            if (state.Owner.IsAir || VegetationSupport.IsState(state, "lava")
                || VegetationSupport.IsState(state, "water")) count++;
        }
        return count;
    }
}
