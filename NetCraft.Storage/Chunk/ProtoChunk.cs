using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage.Chunk;
using NetCraft.Storage.Paletted;
using HeightmapRegistry = NetCraft.Registry.Heightmap;

namespace NetCraft.Storage;

//ProtoChunk, proto chunk, maps to vanilla net.minecraft.world.level.chunk.ProtoChunk
//Minimal concrete implementation of the ChunkAccess abstract base, used to fill blocks during the noise stage
//Subclass LevelChunk awaits upgrade once the networking subsystem lands; here it is only the minimal generation-stage implementation
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

    //GetOrCreateSection gets or confirms the section by section Y
    public LevelChunkSection GetOrCreateSection(int sectionY)
        => GetSection(sectionY) ?? throw new ArgumentOutOfRangeException(nameof(sectionY));

    //SetBlockState writes a block to the given world coords, maps to vanilla setBlockState
    //x/y/z are 0..15 local coords within the chunk, sectionY is the section Y
    public BlockState SetBlockState(int sectionY, int sectionX, int sectionYLocal, int sectionZ, BlockState state)
    {
        var section = GetOrCreateSection(sectionY);
        return section.SetBlockState(sectionX, sectionYLocal, sectionZ, state);
    }

    //GetOrCreateCarvingMask gets or creates the carving mask, maps to vanilla getOrCreateCarvingMask
    //Height is the whole chunk height; the minimum comes from the chunk's minimum block Y
    public CarvingMask GetOrCreateCarvingMask()
        => _carvingMask ??= new CarvingMask(SectionsCount * 16, MinSectionY * 16);

    //ExistingCarvingMask, the already-built carving mask; null when nothing has been carved
    public CarvingMask? ExistingCarvingMask => _carvingMask;

    //SetCarvingMask restores the carving mask on deserialization
    public void SetCarvingMask(CarvingMask? mask) => _carvingMask = mask;

    //PostProcessingSections, positions needing post-processing grouped by section
    public override List<short>?[] PostProcessingSections
        => _postProcessing ??= new List<short>?[SectionsCount];

    //SetPostProcessingSections restores post-processing positions on deserialization
    public void SetPostProcessingSections(List<short>?[] sections) => _postProcessing = sections;

    //MarkPosForPostProcessing registers a position needing post-processing, maps to vanilla markPosForPostProcessing
    public override void MarkPosForPostProcessing(int worldX, int worldY, int worldZ)
    {
        var sectionIndex = (worldY >> 4) - MinSectionY;
        if (sectionIndex < 0 || sectionIndex >= SectionsCount) return;
        var sections = PostProcessingSections;
        sections[sectionIndex] ??= new List<short>();
        sections[sectionIndex]!.Add(PackOffset(worldX & 15, worldY & 15, worldZ & 15));
    }

    //PackOffset packs local coords within a section into a short, maps to vanilla packOffset
    internal static short PackOffset(int x, int y, int z) => (short)(x | (z << 4) | (y << 8));

    //GetSectionsInternal exposes the section array to the LevelChunk subclass
    protected LevelChunkSection[] GetSectionsInternal() => _sections;
}

//LevelChunk, full chunk, maps to vanilla net.minecraft.world.level.chunk.LevelChunk
//Extends ProtoChunk to add a Level reference and the ability to upgrade ChunkStatus
//networking serialization is implemented by a partial LevelChunk.Serializer in LevelChunk.Serializer.cs
public sealed partial class LevelChunk : ProtoChunk
{
    public object? Level { get; }
    public new ChunkStatus ChunkStatus { get; set; } = ChunkStatus.FULL;

    //BlockEntityTags, block entity NBT brought back by decoding the network chunk packet, so the client can populate local block entities
    //The server chunk does not use this field; it goes through the level-level block entity collection
    public List<NetCraft.Nbt.CompoundTag>? BlockEntityTags { get; set; }

    public LevelChunk(ChunkPos pos, int minSectionY, int sectionsCount,
        Func<PalettedContainer<BlockState>> statesFactory,
        Func<PalettedContainer<Holder<Biome>>> biomesFactory,
        object? level = null)
        : base(pos, minSectionY, sectionsCount, statesFactory, biomesFactory)
    {
        Level = level;
    }

    //SetSection replaces the given section, maps to vanilla LevelChunk.setSection
    //Used by LevelChunkSerializer.Read to write newly constructed sections during deserialization
    public void SetSection(int sectionY, LevelChunkSection section)
    {
        var idx = sectionY - MinSectionY;
        if (idx < 0 || idx >= SectionsCount) return;
        GetSectionsInternal()[idx] = section;
    }
}
