using NetCraft.Game.World.Level.LevelGen.Structure;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;
using NetCraft.Storage.Paletted;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen;

//ChunkGenerationHelper chunk generation helper
//Wraps ChunkGenerator + ChunkStatusProcessor into the generator callback ServerChunkCache needs
//On a cache miss it drives the ChunkStatus state machine pipeline from EMPTY to FULL to generate a new chunk
public static class ChunkGenerationHelper
{
    //CreateGenerator creates the generator closure and hands the shared structure manager back to the caller for saving and loading
    //minSectionY/sectionsCount build the SimpleLevelHeightAccessor and fix the section range of generated chunks
    //worldSeed drives structure placement; chunkProvider supplies neighbours and, when absent, decoration degrades to the centre chunk only
    public static (Func<ChunkPos, ChunkAccess?> Generator, StructureFeatureManager Structures) CreateGenerator(
        ChunkGenerator generator,
        int minSectionY,
        int sectionsCount,
        RandomSource random,
        PalettedContainerFactory factory,
        long worldSeed = 0L,
        Func<int, int, ChunkAccess?>? chunkProvider = null,
        bool generateStructures = true)
    {
        var level = new SimpleLevelHeightAccessor(minSectionY, sectionsCount);
        //The world seed is injected into the generator; noise random state and structure placement derive from it, decoupled from the caller's random source
        generator.WorldSeed = worldSeed;
        //The base seed is drawn once; each chunk then derives its own random source from its coordinate
        var baseSeed = random.NextLong();
        var structureRegistry = BuildStructureRegistry(worldSeed);
        //Structure results are shared across the dimension; one per chunk would hide neighbouring chunks' assembled structures during decoration
        var structures = new StructureFeatureManager(generator, structureRegistry)
        {
            //With the server generate-structures option off, not even structure templates are loaded
            ShouldGenerateStructures = generateStructures,
        };
        Func<ChunkPos, ChunkAccess?> generatorFunc = pos =>
        {
            //Each chunk gets its own processor and random source; generation runs concurrently on the thread pool
            //A shared instance would corrupt the RandomSource internal state and stall generation
            //Deriving the seed from the coordinate keeps a chunk reproducible
            var processor = new ChunkStatusProcessor(generator, RandomSource.Create(baseSeed + pos.Pack()),
                structureRegistry, worldSeed, chunkProvider, structures);
            return GenerateChunk(processor, level, factory, pos);
        };
        return (generatorFunc, structures);
    }

    //BuildStructureRegistry feeds the loaded structure sets into the placement registry
    //Matches all structure sets held by vanilla ChunkGenerator at construction; missing one means that set's structures never generate
    private static StructurePlacementRegistry BuildStructureRegistry(long worldSeed)
    {
        var registry = new StructurePlacementRegistry(worldSeed);
        foreach (var holder in BuiltInRegistries.STRUCTURE_SET.ListElements())
        {
            //Both the Registry layer and the Game layer have a StructureSet name; the Game layer is the real type carrying placement parameters
            if (holder.Value is NetCraft.Game.World.Level.LevelGen.Structure.StructureSet set)
                registry.AddSet(set);
        }
        return registry;
    }

    //GenerateChunk single-chunk generation flow, maps to the vanilla chunk generator pipeline
    //1. Build a ProtoChunk at status EMPTY
    //2. Advance to FULL through ChunkStatusProcessor.ProcessToStatus
    //3. Return the proto for ServerChunkCache to cache
    private static ChunkAccess GenerateChunk(
        ChunkStatusProcessor processor,
        LevelHeightAccessor level,
        PalettedContainerFactory factory,
        ChunkPos pos)
    {
        var proto = new ProtoChunk(
            pos,
            level.MinSectionY,
            level.SectionsCount,
            factory.CreateForBlockStates,
            factory.CreateForBiomes);
        processor.ProcessToStatus(proto, ChunkStatus.FULL);
        return proto;
    }
}
