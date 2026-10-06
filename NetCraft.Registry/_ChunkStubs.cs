using System.Collections.Immutable;
using NetCraft.Codec;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry.Codec;

namespace NetCraft.Registry;

//ChunkStatus stub, maps to vanilla net.minecraft.world.level.chunk.status.ChunkStatus
//Minimal set; a full chain of about 90 instances will come once game content is ready
public sealed class ChunkStatus
{
    private static readonly List<ChunkStatus> _order = new();

    public static readonly ChunkStatus EMPTY = Register("empty");
    public static readonly ChunkStatus STRUCTURE_START = Register("structure_starts");
    public static readonly ChunkStatus STRUCTURE_REFERENCES = Register("structure_references");
    public static readonly ChunkStatus BIOMES = Register("biomes");
    public static readonly ChunkStatus NOISE = Register("noise");
    public static readonly ChunkStatus SURFACE = Register("surface");
    public static readonly ChunkStatus CARVERS = Register("carvers");
    public static readonly ChunkStatus LIQUID_CARVERS = Register("liquid_carvers");
    public static readonly ChunkStatus FEATURES = Register("features");
    public static readonly ChunkStatus LIGHT = Register("light");
    public static readonly ChunkStatus SPAWN = Register("spawn");
    public static readonly ChunkStatus HEIGHTMAPS = Register("heightmaps");
    public static readonly ChunkStatus FULL = Register("full");

    private readonly int _index;

    public string Name { get; }

    private ChunkStatus(string name)
    {
        Name = name;
        _index = _order.Count;
    }

    private static ChunkStatus Register(string name)
    {
        var status = new ChunkStatus(name);
        _order.Add(status);
        return status;
    }

    //heightmapsAfter maps to vanilla status.heightmapsAfter
    //EMPTY returns an empty set and the others return the six common types
    public ISet<Heightmap.Types> HeightmapsAfter()
        => this == EMPTY
            ? new HashSet<Heightmap.Types>()
            : new HashSet<Heightmap.Types>(Heightmap.AllTypes);

    //getChunkType maps to vanilla status.getChunkType
    //EMPTY is PROTOCHUNK and the others are LEVELCHUNK
    public ChunkType GetChunkType() => this == EMPTY ? ChunkType.ProtoChunk : ChunkType.LevelChunk;

    //isOrAfter maps to vanilla status.isOrAfter, comparing by static registration order
    public bool IsOrAfter(ChunkStatus other) => _index >= other._index;

    //ToSimpleState maps ChunkStatus to SimpleChunkState, maps to vanilla ChunkStatus.toSimpleState
    //A simplified state machine for chunk task scheduling stage grouping
    public SimpleChunkState ToSimpleState()
    {
        if (this == EMPTY) return SimpleChunkState.Empty;
        if (this == STRUCTURE_START || this == STRUCTURE_REFERENCES || this == BIOMES)
            return SimpleChunkState.StructureStarts;
        if (this == NOISE || this == SURFACE || this == CARVERS || this == LIQUID_CARVERS)
            return SimpleChunkState.Generation;
        if (this == FEATURES || this == LIGHT || this == SPAWN || this == HEIGHTMAPS)
            return SimpleChunkState.Features;
        return SimpleChunkState.Full;
    }

    public override string ToString() => Name;

    //CODEC maps to vanilla ChunkStatus.CODEC
    //Serialized as a string Identifier; returns EMPTY when the table lookup misses
    public static readonly Codec<ChunkStatus> Codec = IdentifierCodec.Instance.ComapFlatMap(
        id =>
        {
            var status = _order.FirstOrDefault(s => s.Name == id.Path);
            return status is null
                ? DataResult<ChunkStatus>.Success(EMPTY)
                : DataResult<ChunkStatus>.Success(status);
        },
        status => Identifier.WithDefaultNamespace(status.Name));
}

//Heightmap stub, maps to vanilla net.minecraft.world.level.levelgen.Heightmap
public static class Heightmap
{
    public enum Types
    {
        WorldSurfaceWg,
        WorldSurface,
        OceanFloorWg,
        OceanFloor,
        MotionBlocking,
        MotionBlockingNoLeaves
    }

    //All types, maps to vanilla Heightmap.Types.values
    public static readonly IReadOnlyList<Types> AllTypes =
        Enum.GetValues<Types>().ToImmutableList();

    //getSerializationKey maps to vanilla Types.getSerializationKey
    //Returns the uppercase underscore name
    public static string GetSerializationKey(this Types type)
        => type switch
        {
            Types.WorldSurfaceWg => "WORLD_SURFACE_WG",
            Types.WorldSurface => "WORLD_SURFACE",
            Types.OceanFloorWg => "OCEAN_FLOOR_WG",
            Types.OceanFloor => "OCEAN_FLOOR",
            Types.MotionBlocking => "MOTION_BLOCKING",
            Types.MotionBlockingNoLeaves => "MOTION_BLOCKING_NO_LEAVES",
            _ => type.ToString().ToUpperInvariant()
        };

    //Reverse lookup the type by serializationKey, maps to vanilla Types.getFromKey
    public static Types? FromSerializationKey(string key)
        => key switch
        {
            "WORLD_SURFACE_WG" => Types.WorldSurfaceWg,
            "WORLD_SURFACE" => Types.WorldSurface,
            "OCEAN_FLOOR_WG" => Types.OceanFloorWg,
            "OCEAN_FLOOR" => Types.OceanFloor,
            "MOTION_BLOCKING" => Types.MotionBlocking,
            "MOTION_BLOCKING_NO_LEAVES" => Types.MotionBlockingNoLeaves,
            _ => null
        };
}

//ChunkType, maps to vanilla net.minecraft.world.level.chunk.status.ChunkType
public enum ChunkType
{
    ProtoChunk,
    LevelChunk
}

//SimpleChunkState simplified chunk state, maps to vanilla net.minecraft.world.level.chunk.status.SimpleChunkState
//Merges the 13 ChunkStatus values into 5 simplified stages for chunk task scheduling
//EMPTY/STRUCTURE_STARTS/GENERATION/FEATURES/FULL
public enum SimpleChunkState
{
    Empty,
    StructureStarts,
    Generation,
    Features,
    Full
}

//ChunkStatePool chunk state pool, maps to vanilla ChunkStatus.StatePool
//Used by the task scheduler to track the simplified state each chunk has reached
//Indexed by ChunkPos.Pack to avoid creating duplicate objects
public sealed class ChunkStatePool
{
    private readonly Dictionary<long, SimpleChunkState> _states = new();

    //Get the current state of a chunk, returning Empty when not recorded
    public SimpleChunkState Get(ChunkPos pos)
        => _states.TryGetValue(ChunkPos.Pack(pos.X, pos.Z), out var state) ? state : SimpleChunkState.Empty;

    //Update advances the chunk state to a later stage, maps to vanilla ChunkStatus.StatePool.update
    //If the new state is earlier than the current one it is ignored, keeping the state monotonic
    public void Update(ChunkPos pos, SimpleChunkState newState)
    {
        var key = ChunkPos.Pack(pos.X, pos.Z);
        if (_states.TryGetValue(key, out var current))
        {
            if (newState > current) _states[key] = newState;
        }
        else
        {
            _states[key] = newState;
        }
    }

    //Update maps ChunkStatus to SimpleChunkState and then updates
    public void Update(ChunkPos pos, ChunkStatus status)
        => Update(pos, status.ToSimpleState());

    //Clear all states
    public void Clear() => _states.Clear();
}

//LightLayer, maps to vanilla net.minecraft.world.level.LightLayer
public enum LightLayer
{
    Block,
    Sky
}

//UpgradeData stub, maps to vanilla net.minecraft.world.level.chunk.UpgradeData
//Minimal set passing through an empty CompoundTag; the real logic will come once game content is ready
public sealed class UpgradeData
{
    public static readonly UpgradeData Empty = new(new CompoundTag());

    public CompoundTag Data { get; }

    public UpgradeData(CompoundTag data) => Data = data;

    //isEmpty maps to vanilla UpgradeData.isEmpty, simplified to Data being empty
    public bool IsEmpty() => Data.IsEmpty;

    //write maps to vanilla UpgradeData.write, directly returning a copy of the internal CompoundTag
    public CompoundTag Write() => (CompoundTag)Data.Copy();

    public UpgradeData Copy() => new((CompoundTag)Data.Copy());
}

//BlendingData.Packed stub, maps to vanilla net.minecraft.world.level.levelgen.blending.BlendingData.Packed
//Minimal set passing through a CompoundTag; the full codec is not implemented
public sealed class BlendingData
{
    public sealed class Packed
    {
        public CompoundTag Data { get; }

        public Packed(CompoundTag data) => Data = data;

        public Packed Copy() => new((CompoundTag)Data.Copy());
    }
}

//BelowZeroRetrogen stub, maps to vanilla net.minecraft.world.level.levelgen.BelowZeroRetrogen
//Minimal set passing through a CompoundTag; the full codec is not implemented
public sealed class BelowZeroRetrogen
{
    public CompoundTag Data { get; }

    public BelowZeroRetrogen(CompoundTag data) => Data = data;

    public BelowZeroRetrogen Copy() => new((CompoundTag)Data.Copy());
}
