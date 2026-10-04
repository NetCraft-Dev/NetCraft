using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage.Chunk;

namespace NetCraft.Storage.Light;

//LayerLightSectionStorage 单层光照区段存储对应原版 net.minecraft.world.level.lighting.LayerLightSectionStorage
//职责是层状态管理(SectionState 位打包)与 DataLayer 的 visible/updating 双缓冲
//原版把存储与引擎放在同一包内互相访问 这里对应放开到 protected internal
public abstract class LayerLightSectionStorage<TSelf> where TSelf : DataLayerStorageMap<TSelf>
{
    private readonly LightLayer _layer;
    protected readonly LightChunkGetter ChunkSource;

    //visibleSectionData 是供外部读取的快照 updatingSectionData 是光照引擎增量写入的副本
    //原版 visible 声明为 volatile 是因为光照线程化 这里是同步实现无并发场景
    protected TSelf VisibleSectionData;
    protected readonly TSelf UpdatingSectionData;

    private bool _hasInconsistencies;

    //区段状态表 值 0 表示不存在 对应原版 Long2ByteMap 的 defaultReturnValue(0)
    protected readonly Dictionary<long, byte> SectionStates = new();

    private readonly HashSet<long> _columnsWithSources = new();
    protected readonly HashSet<long> ChangedSections = new();
    protected readonly HashSet<long> SectionsAffectedByLightUpdates = new();

    //queuedSections 待写入的层数据 原版用同步 map 因多线程访问 同步实现下用普通字典
    protected readonly Dictionary<long, DataLayer> QueuedSections = new();

    private readonly HashSet<long> _columnsToRetainQueuedDataFor = new();
    private readonly HashSet<long> _toRemove = new();

    //getLightValue 子类按层语义读取指定方块节点的光照等级
    protected internal abstract int GetLightValue(long blockNode);

    protected LayerLightSectionStorage(LightLayer lightLayer, LightChunkGetter lightChunkGetter, TSelf map)
    {
        _layer = lightLayer;
        ChunkSource = lightChunkGetter;
        UpdatingSectionData = map;
        VisibleSectionData = map.Copy();
    }

    //hasInconsistencies 是否有待落地的区段增删
    protected internal bool HasInconsistencies => _hasInconsistencies;

    protected internal bool StoringLightForSection(long sectionNode) => GetDataLayer(sectionNode, true) is not null;

    protected DataLayer? GetDataLayer(long sectionNode, bool updating)
        => GetDataLayer(updating ? UpdatingSectionData : VisibleSectionData, sectionNode);

    protected DataLayer? GetDataLayer(TSelf sections, long sectionNode) => sections.GetLayer(sectionNode);

    //getDataLayerToWrite 取待写层 首次写入时复制一份避免污染 visible 快照
    protected internal DataLayer? GetDataLayerToWrite(long sectionNode)
    {
        var dataLayer = UpdatingSectionData.GetLayer(sectionNode);
        if (dataLayer is null) return null;
        if (ChangedSections.Add(sectionNode))
        {
            dataLayer = dataLayer.Copy();
            UpdatingSectionData.SetLayer(sectionNode, dataLayer);
        }
        return dataLayer;
    }

    //getDataLayerData 优先返回队列中的层数据 供外部读取最新但未落地的结果
    public DataLayer? GetDataLayerData(long sectionNode)
        => QueuedSections.TryGetValue(sectionNode, out var layer) ? layer : GetDataLayer(sectionNode, false);

    protected internal int GetStoredLevel(long blockNode)
    {
        var sectionNode = SectionPos.BlockToSection(blockNode);
        var layer = GetDataLayer(sectionNode, true);
        return layer?.Get(
            SectionPos.SectionRelative(BlockPos.GetX(blockNode)),
            SectionPos.SectionRelative(BlockPos.GetY(blockNode)),
            SectionPos.SectionRelative(BlockPos.GetZ(blockNode))) ?? 0;
    }

    protected internal void SetStoredLevel(long blockNode, int level)
    {
        var sectionNode = SectionPos.BlockToSection(blockNode);
        var layer = ChangedSections.Add(sectionNode)
            ? UpdatingSectionData.CopyDataLayer(sectionNode)
            : GetDataLayer(sectionNode, true);
        layer?.Set(
            SectionPos.SectionRelative(BlockPos.GetX(blockNode)),
            SectionPos.SectionRelative(BlockPos.GetY(blockNode)),
            SectionPos.SectionRelative(BlockPos.GetZ(blockNode)),
            level);
        SectionPos.AroundAndAtBlockPos(blockNode, node => SectionsAffectedByLightUpdates.Add(node));
    }

    //markSectionAndNeighborsAsAffected 标记该区段与 26 邻居为受光照更新影响
    protected void MarkSectionAndNeighborsAsAffected(long sectionNode)
    {
        var x = SectionPos.GetX(sectionNode);
        var y = SectionPos.GetY(sectionNode);
        var z = SectionPos.GetZ(sectionNode);
        for (var offsetZ = -1; offsetZ <= 1; offsetZ++)
        for (var offsetX = -1; offsetX <= 1; offsetX++)
        for (var offsetY = -1; offsetY <= 1; offsetY++)
            SectionsAffectedByLightUpdates.Add(SectionPos.AsLong(x + offsetX, y + offsetY, z + offsetZ));
    }

    protected virtual DataLayer CreateDataLayer(long sectionNode)
        => QueuedSections.TryGetValue(sectionNode, out var queuedLayer) ? queuedLayer : new DataLayer();

    //markNewInconsistencies 落地区段增删 原版参数 LightEngine 未被使用故不保留
    protected internal void MarkNewInconsistencies()
    {
        if (!_hasInconsistencies) return;
        _hasInconsistencies = false;

        foreach (var node in _toRemove)
        {
            QueuedSections.Remove(node, out var queued);
            var stored = UpdatingSectionData.RemoveLayer(node);
            if (!_columnsToRetainQueuedDataFor.Contains(SectionPos.GetZeroNode(node))) continue;
            if (queued is not null) QueuedSections[node] = queued;
            else if (stored is not null) QueuedSections[node] = stored;
        }

        foreach (var node in _toRemove)
        {
            OnNodeRemoved(node);
            ChangedSections.Add(node);
        }
        _toRemove.Clear();

        //把队列里的层数据写入更新侧 只处理已存光的区段
        foreach (var sectionNode in QueuedSections.Keys.ToList())
        {
            if (!StoringLightForSection(sectionNode)) continue;
            var data = QueuedSections[sectionNode];
            if (!ReferenceEquals(UpdatingSectionData.GetLayer(sectionNode), data))
            {
                UpdatingSectionData.SetLayer(sectionNode, data);
                ChangedSections.Add(sectionNode);
            }
            QueuedSections.Remove(sectionNode);
        }
    }

    //onNodeAdded 子类补充区段新增时的层语义
    protected virtual void OnNodeAdded(long sectionNode) { }

    //onNodeRemoved 子类补充区段移除时的层语义
    protected virtual void OnNodeRemoved(long sectionNode) { }

    protected internal void SetLightEnabled(long zeroNode, bool enable)
    {
        if (enable) _columnsWithSources.Add(zeroNode);
        else _columnsWithSources.Remove(zeroNode);
    }

    protected internal bool LightOnInSection(long sectionNode)
        => _columnsWithSources.Contains(SectionPos.GetZeroNode(sectionNode));

    protected internal bool LightOnInColumn(long sectionZeroNode) => _columnsWithSources.Contains(sectionZeroNode);

    //retainData 区段移除时是否保留队列数据
    public void RetainData(long zeroNode, bool retain)
    {
        if (retain) _columnsToRetainQueuedDataFor.Add(zeroNode);
        else _columnsToRetainQueuedDataFor.Remove(zeroNode);
    }

    protected internal void QueueSectionData(long sectionNode, DataLayer? data)
    {
        if (data is not null)
        {
            QueuedSections[sectionNode] = data;
            _hasInconsistencies = true;
        }
        else
        {
            QueuedSections.Remove(sectionNode);
        }
    }

    //updateSectionStatus 区段空/非空变化时同步自身与 26 邻居的邻居计数
    protected internal void UpdateSectionStatus(long sectionNode, bool sectionEmpty)
    {
        var state = GetSectionState(sectionNode);
        var newState = SectionState.SetHasData(state, !sectionEmpty);
        if (state == newState) return;

        PutSectionState(sectionNode, newState);
        var neighborIncrement = sectionEmpty ? -1 : 1;
        for (var offsetX = -1; offsetX <= 1; offsetX++)
        for (var offsetY = -1; offsetY <= 1; offsetY++)
        for (var offsetZ = -1; offsetZ <= 1; offsetZ++)
        {
            if (offsetX == 0 && offsetY == 0 && offsetZ == 0) continue;
            var neighborNode = SectionPos.Offset(sectionNode, offsetX, offsetY, offsetZ);
            var neighborState = GetSectionState(neighborNode);
            PutSectionState(neighborNode, SectionState.SetNeighborCount(neighborState,
                SectionState.GetNeighborCount(neighborState) + neighborIncrement));
        }
    }

    private byte GetSectionState(long sectionNode)
        => SectionStates.TryGetValue(sectionNode, out var state) ? state : (byte)0;

    protected void PutSectionState(long sectionNode, byte state)
    {
        if (state != 0)
        {
            var existed = SectionStates.TryGetValue(sectionNode, out var old) && old != 0;
            SectionStates[sectionNode] = state;
            if (!existed) InitializeSection(sectionNode);
        }
        else if (SectionStates.Remove(sectionNode))
        {
            RemoveSection(sectionNode);
        }
    }

    private void InitializeSection(long sectionNode)
    {
        if (_toRemove.Remove(sectionNode)) return;
        UpdatingSectionData.SetLayer(sectionNode, CreateDataLayer(sectionNode));
        ChangedSections.Add(sectionNode);
        OnNodeAdded(sectionNode);
        MarkSectionAndNeighborsAsAffected(sectionNode);
        _hasInconsistencies = true;
    }

    private void RemoveSection(long sectionNode)
    {
        _toRemove.Add(sectionNode);
        _hasInconsistencies = true;
    }

    //swapSectionMap 把更新侧快照成 visible 并回调所有受影响区段的光照更新
    protected internal void SwapSectionMap()
    {
        if (ChangedSections.Count > 0)
        {
            VisibleSectionData = UpdatingSectionData.Copy();
            ChangedSections.Clear();
        }
        if (SectionsAffectedByLightUpdates.Count == 0) return;
        foreach (var sectionNode in SectionsAffectedByLightUpdates)
            ChunkSource.OnLightUpdate(_layer, SectionPos.Of(sectionNode));
        SectionsAffectedByLightUpdates.Clear();
    }

    public SectionType GetDebugSectionType(long sectionNode) => SectionState.GetType(GetSectionState(sectionNode));

    //SectionState 区段状态位打包对应原版 SectionState
    //高 3 位保留 第 5 位是 hasData 标志 低 5 位是 26 邻居中有数据的数量
    protected static class SectionState
    {
        private const int MaxNeighbors = 26;
        private const byte HasDataBit = 32;
        private const byte NeighborCountBits = 31;

        public static byte SetHasData(byte state, bool hasData)
            => (byte)(hasData ? state | HasDataBit : state & -33);

        public static byte SetNeighborCount(byte state, int neighborCount)
        {
            if (neighborCount < 0 || neighborCount > MaxNeighbors)
                throw new ArgumentOutOfRangeException(nameof(neighborCount), "邻居数量必须落在 [0; 26]");
            return (byte)((state & -32) | (neighborCount & NeighborCountBits));
        }

        public static bool HasData(byte state) => (state & HasDataBit) != 0;

        public static int GetNeighborCount(byte state) => state & NeighborCountBits;

        public static SectionType GetType(byte state)
            => state == 0 ? SectionType.Empty : HasData(state) ? SectionType.LightAndData : SectionType.LightOnly;
    }
}

//SectionType 区段光照状态类别对应原版 SectionType
public enum SectionType
{
    Empty,
    LightOnly,
    LightAndData,
}
