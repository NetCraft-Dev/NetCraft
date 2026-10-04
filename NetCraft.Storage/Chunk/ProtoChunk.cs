using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage.Chunk;
using NetCraft.Storage.Paletted;
using HeightmapRegistry = NetCraft.Registry.Heightmap;

namespace NetCraft.Storage;

//ProtoChunk 原型区块对应原版 net.minecraft.world.level.chunk.ProtoChunk
//ChunkAccess 抽象基类的最简具体实现用于噪声阶段填方块
//子类 LevelChunk 待 networking 子系统接入升级此处仅做生成阶段最小实现
public class ProtoChunk : ChunkAccess
{
    public override ChunkPos Pos { get; }
    public override int MinSectionY { get; }
    public override int SectionsCount { get; }
    public override ChunkStatus ChunkStatus { get; } = ChunkStatus.EMPTY;

    private readonly LevelChunkSection[] _sections;
    private readonly Dictionary<HeightmapRegistry.Types, long[]> _heightmaps;
    private CarvingMask? _carvingMask;
    private List<short>?[]? _postProcessing;

    public override IDictionary<HeightmapRegistry.Types, long[]> Heightmaps => _heightmaps;

    public ProtoChunk(ChunkPos pos, int minSectionY, int sectionsCount,
        Func<PalettedContainer<BlockState>> statesFactory,
        Func<PalettedContainer<Holder<Biome>>> biomesFactory)
    {
        Pos = pos;
        MinSectionY = minSectionY;
        SectionsCount = sectionsCount;
        _sections = new LevelChunkSection[sectionsCount];
        for (var i = 0; i < sectionsCount; i++)
            _sections[i] = new LevelChunkSection(statesFactory, biomesFactory);
        _heightmaps = new Dictionary<HeightmapRegistry.Types, long[]>();
    }

    public override LevelChunkSection? GetSection(int sectionY)
    {
        var idx = sectionY - MinSectionY;
        return idx >= 0 && idx < _sections.Length ? _sections[idx] : null;
    }

    //GetOrCreateSection 按区段 Y 获取或确认区段存在
    public LevelChunkSection GetOrCreateSection(int sectionY)
        => GetSection(sectionY) ?? throw new ArgumentOutOfRangeException(nameof(sectionY));

    //SetBlockState 写入方块到指定世界坐标对应原版 setBlockState
    //x/y/z 为区块内 0..15 局部坐标sectionY 为区段 Y
    public BlockState SetBlockState(int sectionY, int sectionX, int sectionYLocal, int sectionZ, BlockState state)
    {
        var section = GetOrCreateSection(sectionY);
        return section.SetBlockState(sectionX, sectionYLocal, sectionZ, state);
    }

    //GetOrCreateCarvingMask 取或建雕刻标记对应原版 getOrCreateCarvingMask
    //高度取整块高度最低位取区块最低方块 Y
    public CarvingMask GetOrCreateCarvingMask()
        => _carvingMask ??= new CarvingMask(SectionsCount * 16, MinSectionY * 16);

    //ExistingCarvingMask 已建好的雕刻标记未雕刻过时为 null
    public CarvingMask? ExistingCarvingMask => _carvingMask;

    //SetCarvingMask 反序列化时恢复雕刻标记
    public void SetCarvingMask(CarvingMask? mask) => _carvingMask = mask;

    //PostProcessingSections 需要后处理的位置按区段分组
    public override List<short>?[] PostProcessingSections
        => _postProcessing ??= new List<short>?[SectionsCount];

    //SetPostProcessingSections 反序列化时恢复后处理位置
    public void SetPostProcessingSections(List<short>?[] sections) => _postProcessing = sections;

    //MarkPosForPostProcessing 登记一个需要后处理的位置对应原版 markPosForPostProcessing
    public override void MarkPosForPostProcessing(int worldX, int worldY, int worldZ)
    {
        var sectionIndex = (worldY >> 4) - MinSectionY;
        if (sectionIndex < 0 || sectionIndex >= SectionsCount) return;
        var sections = PostProcessingSections;
        sections[sectionIndex] ??= new List<short>();
        sections[sectionIndex]!.Add(PackOffset(worldX & 15, worldY & 15, worldZ & 15));
    }

    //PackOffset 把区段内局部坐标压成 short 对应原版 packOffset
    internal static short PackOffset(int x, int y, int z) => (short)(x | (z << 4) | (y << 8));

    //GetSectionsInternal 暴露区段数组供 LevelChunk 子类访问
    protected LevelChunkSection[] GetSectionsInternal() => _sections;
}

//LevelChunk 完整区块对应原版 net.minecraft.world.level.chunk.LevelChunk
//继承 ProtoChunk 扩展 Level 引用与 ChunkStatus 升级能力
//networking 序列化通过 partial LevelChunk.Serializer 在 LevelChunk.Serializer.cs 实现
public sealed partial class LevelChunk : ProtoChunk
{
    public object? Level { get; }
    public new ChunkStatus ChunkStatus { get; set; } = ChunkStatus.FULL;

    //BlockEntityTags 网络区块包解码带回来的方块实体 NBT 供客户端填充本地方块实体
    //服务端区块不用这个字段 它走关卡级的方块实体集合
    public List<NetCraft.Nbt.CompoundTag>? BlockEntityTags { get; set; }

    public LevelChunk(ChunkPos pos, int minSectionY, int sectionsCount,
        Func<PalettedContainer<BlockState>> statesFactory,
        Func<PalettedContainer<Holder<Biome>>> biomesFactory,
        object? level = null)
        : base(pos, minSectionY, sectionsCount, statesFactory, biomesFactory)
    {
        Level = level;
    }

    //SetSection 替换指定区段对应原版 LevelChunk.setSection
    //供 LevelChunkSerializer.Read 反序列化时写入新构造的区段
    public void SetSection(int sectionY, LevelChunkSection section)
    {
        var idx = sectionY - MinSectionY;
        if (idx < 0 || idx >= SectionsCount) return;
        GetSectionsInternal()[idx] = section;
    }
}
