using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage.Chunk;

namespace NetCraft.Storage.Light;

//LevelLightEngine 世界光照门面对应原版 net.minecraft.world.level.lighting.LevelLightEngine
//把方块光与天光两个引擎合成一个 LightEventListener 对外统一转发
public class LevelLightEngine : LightEventListener
{
    public const int LightSectionPadding = 1;

    public static readonly LevelLightEngine Empty = new();

    private readonly LevelHeightAccessor _heightAccessor;
    private readonly LightEngine<BlockLightSectionStorage.BlockDataLayerStorageMap, BlockLightSectionStorage>? _blockEngine;
    private readonly LightEngine<SkyLightSectionStorage.SkyDataLayerStorageMap, SkyLightSectionStorage>? _skyEngine;

    public LevelLightEngine(LightChunkGetter chunkSource, bool hasBlockLight, bool hasSkyLight)
    {
        _heightAccessor = chunkSource.GetLevel();
        _blockEngine = hasBlockLight ? new BlockLightEngine(chunkSource) : null;
        _skyEngine = hasSkyLight ? new SkyLightEngine(chunkSource) : null;
    }

    private LevelLightEngine()
    {
        _heightAccessor = new SimpleLevelHeightAccessor(0, 0);
        _blockEngine = null;
        _skyEngine = null;
    }

    public void CheckBlock(BlockPos pos)
    {
        _blockEngine?.CheckBlock(pos);
        _skyEngine?.CheckBlock(pos);
    }

    public bool HasLightWork()
    {
        if (_skyEngine is not null && _skyEngine.HasLightWork()) return true;
        return _blockEngine is not null && _blockEngine.HasLightWork();
    }

    public int RunLightUpdates() => RunLightUpdates(0);

    //RunLightUpdates 透传预算给两层引擎 分批推进时每批只处理有限条
    public int RunLightUpdates(int budget)
    {
        var count = 0;
        if (_blockEngine is not null) count += _blockEngine.RunLightUpdates(budget);
        if (_skyEngine is not null) count += _skyEngine.RunLightUpdates(budget);
        return count;
    }

    public void UpdateSectionStatus(SectionPos pos, bool sectionEmpty)
    {
        _blockEngine?.UpdateSectionStatus(pos, sectionEmpty);
        _skyEngine?.UpdateSectionStatus(pos, sectionEmpty);
    }

    public void SetLightEnabled(ChunkPos pos, bool enable)
    {
        _blockEngine?.SetLightEnabled(pos, enable);
        _skyEngine?.SetLightEnabled(pos, enable);
    }

    public void PropagateLightSources(ChunkPos pos)
    {
        _blockEngine?.PropagateLightSources(pos);
        _skyEngine?.PropagateLightSources(pos);
    }

    //getLayerListener 取单层监听 该层未启用时返回空实现
    public LayerLightEventListener GetLayerListener(LightLayer layer)
    {
        if (layer == LightLayer.Block)
        {
            if (_blockEngine is null) return DummyLightLayerEventListener.Instance;
            return _blockEngine;
        }
        if (_skyEngine is null) return DummyLightLayerEventListener.Instance;
        return _skyEngine;
    }

    public string GetDebugData(LightLayer layer, SectionPos pos)
    {
        if (layer == LightLayer.Block)
            return _blockEngine?.GetDebugData(pos.AsLong()) ?? "n/a";
        return _skyEngine?.GetDebugData(pos.AsLong()) ?? "n/a";
    }

    public SectionType GetDebugSectionType(LightLayer layer, SectionPos pos)
    {
        if (layer == LightLayer.Block)
        {
            if (_blockEngine is not null) return _blockEngine.GetDebugSectionType(pos.AsLong());
        }
        else if (_skyEngine is not null)
        {
            return _skyEngine.GetDebugSectionType(pos.AsLong());
        }
        return SectionType.Empty;
    }

    public void QueueSectionData(LightLayer layer, SectionPos pos, DataLayer? data)
    {
        if (layer == LightLayer.Block) _blockEngine?.QueueSectionData(pos.AsLong(), data);
        else _skyEngine?.QueueSectionData(pos.AsLong(), data);
    }

    public void RetainData(ChunkPos pos, bool retain)
    {
        _blockEngine?.RetainData(pos, retain);
        _skyEngine?.RetainData(pos, retain);
    }

    public int GetRawBrightness(BlockPos pos, int skyDampen)
    {
        var skyLight = _skyEngine is null ? 0 : _skyEngine.GetLightValue(pos) - skyDampen;
        var blockLight = _blockEngine is null ? 0 : _blockEngine.GetLightValue(pos);
        return Math.Max(blockLight, skyLight);
    }

    //lightOnInColumn 该列是否两层光照都已启用
    public bool LightOnInColumn(long sectionZeroNode)
        => (_blockEngine is null || _blockEngine.Storage.LightOnInColumn(sectionZeroNode))
           && (_skyEngine is null || _skyEngine.Storage.LightOnInColumn(sectionZeroNode));

    public int GetLightSectionCount() => _heightAccessor.SectionsCount + 2;

    public int GetMinLightSection() => _heightAccessor.MinSectionY - 1;

    public int GetMaxLightSection() => GetMinLightSection() + GetLightSectionCount();
}
