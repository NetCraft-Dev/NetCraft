using NetCraft.Game.World.Level.LevelGen.Structure;
using NetCraft.Registry;
using NetCraft.Storage;
using NetCraft.Util.Random;
using HeightmapRegistry = NetCraft.Registry.Heightmap;
using LevelHeightmap = NetCraft.Storage.LevelGen.Heightmap;

namespace NetCraft.Game.World.Level.LevelGen;

//ChunkStatusProcessor chunk status machine processor, maps to the vanilla ChunkStatus state machine pipeline
//Phase E simplified implementation calls the matching ChunkGenerator method by ChunkStatus name
//Phase 11.54-B wired up real structure generation for STRUCTURE_START/STRUCTURE_REFERENCES
public sealed class ChunkStatusProcessor
{
    private readonly ChunkGenerator _generator;
    private readonly RandomSource _random;
    private readonly long _seed;
    //_chunkProvider neighbour chunk provider; decoration collects biomes within a 3x3 and, without neighbours, decorates the centre chunk only
    private readonly Func<int, int, ChunkAccess?>? _chunkProvider;

    //FinalHeightmaps final heightmap set, maps to vanilla ChunkStatus.FINAL_HEIGHTMAPS
    //OCEAN_FLOOR counts only solid blocks that block movement; the other three include fluids and non-opaque blocks
    private static readonly HeightmapRegistry.Types[] FinalHeightmaps =
    {
        HeightmapRegistry.Types.OceanFloor,
        HeightmapRegistry.Types.WorldSurface,
        HeightmapRegistry.Types.MotionBlocking,
        HeightmapRegistry.Types.MotionBlockingNoLeaves,
    };
    //StructureFeatures structure manager holding the structures and references assembled by this pipeline for later stages
    public StructureFeatureManager StructureFeatures { get; }

    //DecorationWriteRadius writable chunk radius during decoration; 1 means the centre chunk plus its eight neighbours
    //Matches the WorldGenRegion write radius vanilla uses in the FEATURES stage
    private const int DecorationWriteRadius = 1;

    public ChunkStatusProcessor(ChunkGenerator generator, RandomSource random)
        : this(generator, random, null, 0L) { }

    //Constructor with a structure registry, used by structure assembly scenarios
    //structureRegistry null makes the structure stages no-ops; seed drives carver and structure placement seed derivation
    //sharedStructures is shared across the dimension by the generation chain, so structures assembled in neighbour chunks are visible during this chunk's decoration
    public ChunkStatusProcessor(ChunkGenerator generator, RandomSource random,
        StructurePlacementRegistry? structureRegistry, long seed, Func<int, int, ChunkAccess?>? chunkProvider = null,
        StructureFeatureManager? sharedStructures = null)
    {
        _generator = generator;
        _random = random;
        _seed = seed;
        _chunkProvider = chunkProvider;
        StructureFeatures = sharedStructures
            ?? (structureRegistry is null
                ? new StructureFeatureManager()
                : new StructureFeatureManager(generator, structureRegistry));
    }

    //ProcessChunk runs the matching generation stage for a ChunkStatus
    //Returns true when the status was handled and false when there is no handler
    public bool ProcessChunk(ChunkAccess chunk, ChunkStatus status)
    {
        var structures = StructureManager.Default;
        if (status == ChunkStatus.STRUCTURE_START)
        {
            //STRUCTURE_START assembles the structures this chunk hits from the structure sets
            StructureFeatures.CreateStarts(chunk);
            return true;
        }
        if (status == ChunkStatus.STRUCTURE_REFERENCES)
        {
            //STRUCTURE_REFERENCES scans a 17x17 of neighbours and collects structure references whose bounding boxes cover this chunk
            //Used by the later FEATURES stage to avoid decorating there
            StructureFeatures.CollectReferences(chunk.Pos);
            return true;
        }
        if (status == ChunkStatus.BIOMES)
        {
            //Walks every section by quart and writes the biome queried from BiomeSource, matching vanilla point sampling
            var biomeSource = _generator.BiomeSource;
            var posX = chunk.Pos.X;
            var posZ = chunk.Pos.Z;
            for (var sectionIdx = 0; sectionIdx < chunk.SectionsCount; sectionIdx++)
            {
                var sectionY = chunk.MinSectionY + sectionIdx;
                for (var qY = 0; qY < 4; qY++)
                {
                    for (var qX = 0; qX < 4; qX++)
                    {
                        for (var qZ = 0; qZ < 4; qZ++)
                        {
                            var wx = posX * 16 + qX * 4;
                            var wy = sectionY * 16 + qY * 4;
                            var wz = posZ * 16 + qZ * 4;
                            var biome = biomeSource.GetBiome(wx, wy, wz);
                            chunk.SetBiome(wx, wy, wz, Holder<Biome>.Direct(biome));
                        }
                    }
                }
            }
            return true;
        }
        if (status == ChunkStatus.NOISE)
        {
            //The noise stage calls FillFromNoise to fill blocks
            _generator.FillFromNoise(new object(), structures, chunk, _random);
            return true;
        }
        if (status == ChunkStatus.SURFACE)
        {
            //The surface stage calls BuildSurface to apply surface rules
            _generator.BuildSurface(new object(), structures, chunk, _random);
            return true;
        }
        if (status == ChunkStatus.CARVERS)
        {
            //The carver stage digs caves with the biomes' configured carvers; liquid carving is merged into this stage
            _generator.ApplyCarvers(_seed, chunk, _random);
            return true;
        }
        if (status == ChunkStatus.LIQUID_CARVERS)
        {
            //Liquid carving is merged into CARVERS; this stage is empty in vanilla
            return true;
        }
        if (status == ChunkStatus.FEATURES)
        {
            //Compute the final heightmaps up front before decorating, matching the start of vanilla ChunkStatusTasks.generateFeatures
            //Feature placement and spawn column lookups both depend on them
            LevelHeightmap.PrimeHeightmaps(chunk, FinalHeightmaps);
            //Decoration may only write the centre chunk and its neighbours, out-of-range writes are dropped silently, matching the vanilla WorldGenRegion write radius
            //Structure placement and feature placement both happen inside this region, structures before features
            var region = new WorldGenRegion(chunk.MinSectionY, chunk.SectionsCount)
            {
                Seed = _seed,
                CenterChunk = chunk.Pos,
                WriteRadius = DecorationWriteRadius,
            };
            region.AddChunk(chunk);
            if (_chunkProvider is not null)
            {
                for (var offsetX = -DecorationWriteRadius; offsetX <= DecorationWriteRadius; offsetX++)
                for (var offsetZ = -DecorationWriteRadius; offsetZ <= DecorationWriteRadius; offsetZ++)
                {
                    if (offsetX == 0 && offsetZ == 0) continue;
                    var neighbour = _chunkProvider(chunk.Pos.X + offsetX, chunk.Pos.Z + offsetZ);
                    if (neighbour is not null) region.AddChunk(neighbour);
                }
            }
            _generator.ApplyBiomeDecoration(region, chunk, StructureFeatures);
            return true;
        }
        if (status == ChunkStatus.LIGHT)
        {
            //Lighting for the LIGHT stage is run uniformly by ServerChunkCache.ProcessLight once the chunk is ready
            //Not repeated here to avoid lighting the same chunk twice
            return true;
        }
        if (status == ChunkStatus.SPAWN || status == ChunkStatus.HEIGHTMAPS)
        {
            //Mob spawning placeholder; real wiring needs the matching subsystem ready
            //Heightmaps are not handled here: vanilla 26.2 has no separate HEIGHTMAPS stage; heightmaps are computed up front
            //from FINAL_HEIGHTMAPS before FEATURES, covered by GetHeight's lazy fill and incremental maintenance on block changes
            return true;
        }
        if (status == ChunkStatus.EMPTY || status == ChunkStatus.FULL)
        {
            //EMPTY and FULL have no generation work
            return true;
        }
        return false;
    }

    //ProcessToStatus advances a chunk from its current status to the target status
    //Calls ProcessChunk in ChunkStatus registration order until target is reached
    public void ProcessToStatus(ChunkAccess chunk, ChunkStatus target)
    {
        var current = chunk.ChunkStatus;
        while (current != target && current is not null)
        {
            var next = NextStatus(current);
            if (next is null) break;
            ProcessChunk(chunk, next);
            current = next;
        }
    }

    //StatusOrder stage advance order, made static to avoid allocating an array on every NextStatus
    private static readonly ChunkStatus[] StatusOrder =
    {
        ChunkStatus.EMPTY,
        ChunkStatus.STRUCTURE_START,
        ChunkStatus.STRUCTURE_REFERENCES,
        ChunkStatus.BIOMES,
        ChunkStatus.NOISE,
        ChunkStatus.SURFACE,
        ChunkStatus.CARVERS,
        ChunkStatus.LIQUID_CARVERS,
        ChunkStatus.FEATURES,
        ChunkStatus.LIGHT,
        ChunkStatus.SPAWN,
        ChunkStatus.HEIGHTMAPS,
        ChunkStatus.FULL
    };

    //NextStatus looks up the next status by the ChunkStatus static registration order
    private static ChunkStatus? NextStatus(ChunkStatus status)
    {
        var index = System.Array.IndexOf(StatusOrder, status);
        return index >= 0 && index < StatusOrder.Length - 1 ? StatusOrder[index + 1] : null;
    }

}
