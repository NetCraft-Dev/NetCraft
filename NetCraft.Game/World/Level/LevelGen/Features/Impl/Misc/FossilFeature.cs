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

//FossilFeatureConfiguration fossil configuration, maps to vanilla FossilFeatureConfiguration
//The fossil body and overlay structures pair one to one by index
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

//FossilFeature fossil feature, maps to vanilla FossilFeature
//Takes a sand height band across the chunk, picks a skeleton template and places it below the surface, then layers the ore overlay structure
public sealed class FossilFeature : Feature<FossilFeatureConfiguration>
{
    private const string FeatureId = "fossil";

    public static readonly FossilFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new FossilFeature());

    //TemplateManager structure template manager, injected by world assembly
    //The feature placement context only gets a WorldGenRegion, not the resource pack, so without injection minecraft:fossil/... templates cannot be read
    public static StructureTemplateManager? TemplateManager { get; set; }

    private FossilFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), FossilFeatureConfiguration.Codec) { }

    protected override bool Place(FossilFeatureConfiguration config, FeaturePlaceContext context)
    {
        var random = context.Random;
        var level = context.Level;
        var origin = context.Origin;
        //Even when the template manager is null these two random values must still be consumed, otherwise later features drift for the same seed
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

    //ProcessorsOf fetch the processors from the processor list holder; treat an unbound holder as an empty list
    private static IReadOnlyList<StructureProcessor> ProcessorsOf(Holder<RegistryProcessorList> holder)
        => holder.IsBound() && holder.Value is GameStructureProcessorList list
            ? list.Processors
            : Array.Empty<StructureProcessor>();

    //CountEmptyCorners how many of the bounding box's eight corners are air or fluid, maps to vanilla countEmptyCorners
    //Corner order follows vanilla; only the count matters so the order does not affect the result
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
