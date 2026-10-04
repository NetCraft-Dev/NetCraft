using System.Collections.Immutable;
using NetCraft.Codec;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry.Codec;

namespace NetCraft.Registry;

//ChunkStatus stub对应原版net.minecraft.world.level.chunk.status.ChunkStatus
//最小集只支持EMPTY/FULL两个状态完整chain约90个实例待游戏内容就绪
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

    //heightmapsAfter对应原版status.heightmapsAfter
    //EMPTY返回空集其他返回常用六类
    public ISet<Heightmap.Types> HeightmapsAfter()
        => this == EMPTY
            ? new HashSet<Heightmap.Types>()
            : new HashSet<Heightmap.Types>(Heightmap.AllTypes);

    //getChunkType对应原版status.getChunkType
    //EMPTY为PROTOCHUNK其他为LEVELCHUNK
    public ChunkType GetChunkType() => this == EMPTY ? ChunkType.ProtoChunk : ChunkType.LevelChunk;

    //isOrAfter对应原版status.isOrAfter按静态注册顺序比较
    public bool IsOrAfter(ChunkStatus other) => _index >= other._index;

    //ToSimpleState 把 ChunkStatus 映射到 SimpleChunkState 对应原版 ChunkStatus.toSimpleState
    //简化状态机用于 chunk 任务调度阶段分组
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

    //CODEC对应原版ChunkStatus.CODEC
    //序列化为字符串Identifier查表找不到返回EMPTY
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

//Heightmap stub对应原版net.minecraft.world.level.levelgen.Heightmap
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

    //全部类型集合对应原版Heightmap.Types.values
    public static readonly IReadOnlyList<Types> AllTypes =
        Enum.GetValues<Types>().ToImmutableList();

    //getSerializationKey对应原版Types.getSerializationKey
    //返回大写下划线名
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

    //按serializationKey反查类型对应原版Types.getFromKey
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

//ChunkType对应原版net.minecraft.world.level.chunk.status.ChunkType
public enum ChunkType
{
    ProtoChunk,
    LevelChunk
}

//SimpleChunkState 简化区块状态对应原版net.minecraft.world.level.chunk.status.SimpleChunkState
//把13个ChunkStatus合并为5个简化阶段用于chunk任务调度
//EMPTY/STRUCTURE_STARTS/GENERATION/FEATURES/FULL
public enum SimpleChunkState
{
    Empty,
    StructureStarts,
    Generation,
    Features,
    Full
}

//ChunkStatePool 区块状态池对应原版ChunkStatus.StatePool
//用于任务调度器跟踪每个chunk当前到达的简化状态
//按ChunkPos.Pack索引避免重复对象创建
public sealed class ChunkStatePool
{
    private readonly Dictionary<long, SimpleChunkState> _states = new();

    //Get 获取chunk当前状态未记录返回 Empty
    public SimpleChunkState Get(ChunkPos pos)
        => _states.TryGetValue(ChunkPos.Pack(pos.X, pos.Z), out var state) ? state : SimpleChunkState.Empty;

    //Update 更新chunk状态为更靠后的阶段对应原版ChunkStatus.StatePool.update
    //如果新状态比当前状态靠前则忽略保证状态单调推进
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

    //Update 把ChunkStatus映射到SimpleChunkState再Update
    public void Update(ChunkPos pos, ChunkStatus status)
        => Update(pos, status.ToSimpleState());

    //Clear 清空所有状态
    public void Clear() => _states.Clear();
}

//LightLayer对应原版net.minecraft.world.level.LightLayer
public enum LightLayer
{
    Block,
    Sky
}

//UpgradeData stub对应原版net.minecraft.world.level.chunk.UpgradeData
//最小集为空CompoundTag透传实际逻辑待游戏内容就绪
public sealed class UpgradeData
{
    public static readonly UpgradeData Empty = new(new CompoundTag());

    public CompoundTag Data { get; }

    public UpgradeData(CompoundTag data) => Data = data;

    //isEmpty对应原版UpgradeData.isEmpty简化为Data为空
    public bool IsEmpty() => Data.IsEmpty;

    //write对应原版UpgradeData.write直接返回内部CompoundTag副本
    public CompoundTag Write() => (CompoundTag)Data.Copy();

    public UpgradeData Copy() => new((CompoundTag)Data.Copy());
}

//BlendingData.Packed stub对应原版net.minecraft.world.level.levelgen.blending.BlendingData.Packed
//最小集CompoundTag透传不实现完整codec
public sealed class BlendingData
{
    public sealed class Packed
    {
        public CompoundTag Data { get; }

        public Packed(CompoundTag data) => Data = data;

        public Packed Copy() => new((CompoundTag)Data.Copy());
    }
}

//BelowZeroRetrogen stub对应原版net.minecraft.world.level.levelgen.BelowZeroRetrogen
//最小集CompoundTag透传不实现完整codec
public sealed class BelowZeroRetrogen
{
    public CompoundTag Data { get; }

    public BelowZeroRetrogen(CompoundTag data) => Data = data;

    public BelowZeroRetrogen Copy() => new((CompoundTag)Data.Copy());
}
