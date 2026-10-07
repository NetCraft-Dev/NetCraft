using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructureType structure type, maps to vanilla net.minecraft.world.level.levelgen.structure.StructureType
//Carries only the registry name and element codec; loading dispatches the JSON to a concrete structure's codec by the type field
public sealed class StructureType : NetCraft.Registry.StructureType<object>
{
    public Identifier Id { get; }

    //ElementCodec element codec for this structure type, parses the whole JSON including generation settings
    //Test and programmatic structures do not participate in data-driven loading, so this may be null
    public Codec<NetCraft.Registry.Structure>? ElementCodec { get; }

    public StructureType(Identifier id, Codec<NetCraft.Registry.Structure>? elementCodec = null)
    {
        Id = id;
        ElementCodec = elementCodec;
    }

    public override string ToString() => $"StructureType[{Id}]";
}

//Structure structure abstraction, maps to vanilla net.minecraft.world.level.levelgen.structure.Structure
//A structure = generation settings + the logic that finds a generation point; the assembled pieces are carried by StructureStart
//The decoration stage's placeInChunk actually writes the pieces into the world, not this class
public abstract class Structure : NetCraft.Registry.Structure
{
    public abstract Identifier Id { get; }

    //Type structure type, used for registry dispatch and serialization write-back
    public abstract StructureType Type { get; }

    //Settings generation settings; the biome range and decoration step live here
    public StructureGenerationSettings Settings { get; }

    protected Structure(StructureGenerationSettings settings) => Settings = settings;

    //FindGenerationPoint finds the generation point, maps to vanilla findGenerationPoint
    //Returning null means the chunk generates nothing
    public abstract GenerationStub? FindGenerationPoint(GenerationContext context);

    //Generate assembles a StructureStart, maps to vanilla generate
    //Biome filtering is handled by context.ValidBiome; a failure is judged invalid
    public StructureStart Generate(GenerationContext context)
    {
        var stub = FindGenerationPoint(context);
        if (stub is null) return StructureStart.Invalid;
        if (!context.ValidBiome(stub.Position)) return StructureStart.Invalid;
        var start = new StructureStart(this, context.ChunkPos, stub.Build());
        return start.IsValid ? start : StructureStart.Invalid;
    }
}

//StructurePiecesBuilder piece collector, maps to vanilla StructurePiecesBuilder
//The generation point only feeds pieces in; they are taken out all at once after assembly
public sealed class StructurePiecesBuilder
{
    private readonly List<StructurePiece> _pieces = new();

    public bool IsEmpty => _pieces.Count == 0;

    public int Count => _pieces.Count;

    //AddPiece appends a piece
    public void AddPiece(StructurePiece piece) => _pieces.Add(piece);

    //Build takes out the piece list
    public IReadOnlyList<StructurePiece> Build() => _pieces;
}

//GenerationStub generation point, maps to vanilla Structure.GenerationStub
//Holds the structure origin and the piece source; pieces can be given as a list directly or generated lazily on Build
public sealed class GenerationStub
{
    //Position structure origin; biome filtering and later alignment both use it
    public BlockPos Position { get; }

    private readonly IReadOnlyList<StructurePiece>? _pieces;
    private readonly Action<StructurePiecesBuilder>? _generator;

    public GenerationStub(BlockPos position, IReadOnlyList<StructurePiece> pieces)
    {
        Position = position;
        _pieces = pieces;
    }

    public GenerationStub(BlockPos position, Action<StructurePiecesBuilder> generator)
    {
        Position = position;
        _generator = generator;
    }

    //Build produces the piece list; a directly supplied list takes the fast path
    public IReadOnlyList<StructurePiece> Build()
    {
        if (_pieces is not null) return _pieces;
        var builder = new StructurePiecesBuilder();
        _generator!(builder);
        return builder.Build();
    }
}

//GenerationContext structure generation context, maps to vanilla Structure.GenerationContext
//Bundles the generator, seed, chunk and random source for the structure; the random source derives from the seed and chunk coords for reproducibility
public sealed class GenerationContext
{
    public ChunkGenerator ChunkGenerator { get; }
    public long Seed { get; }
    public ChunkPos ChunkPos { get; }

    //Random random source for structure generation, maps to vanilla WorldgenRandom based on LegacyRandomSource
    public RandomSource Random { get; }

    public LevelHeightAccessor HeightAccessor { get; }

    //ValidBiome biome filter, maps to the predicate in vanilla findValidGenerationPoint
    //Allows by default; the caller replaces it per the structure's declared biomes, and tests and scenarios without biome constraints use the default
    public Func<BlockPos, bool> ValidBiome { get; set; } = _ => true;

    public GenerationContext(ChunkGenerator chunkGenerator, long seed, ChunkPos chunkPos,
        LevelHeightAccessor heightAccessor)
    {
        ChunkGenerator = chunkGenerator;
        Seed = seed;
        ChunkPos = chunkPos;
        HeightAccessor = heightAccessor;
        var random = new LegacyRandomSource(0L);
        WorldgenRandom.SetLargeFeatureSeed(random, seed, chunkPos.X, chunkPos.Z);
        Random = random;
    }

    //IsBiomeAllowed judges by the structure's declared biomes; no declared biomes means not allowed
    public bool IsBiomeAllowed(Structure structure, BlockPos pos)
    {
        if (structure.Settings.Biomes.Size == 0) return false;
        var current = ChunkGenerator.BiomeSource.GetBiome(pos.X, pos.Y, pos.Z);
        foreach (var holder in structure.Settings.Biomes)
        {
            if (holder.Value is { } biome && biome.Id == current.Id) return true;
        }
        return false;
    }
}
