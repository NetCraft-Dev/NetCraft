using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage.Chunk;

namespace NetCraft.Storage.Light;

//LayerLightSectionStorage, single-layer light section storage, maps to vanilla net.minecraft.world.level.lighting.LayerLightSectionStorage
//Responsible for layer state management (SectionState bit packing) and the visible/updating double buffer of DataLayer
//Vanilla keeps storage and engine in the same package with mutual access; here that is opened up to protected internal
public abstract class LayerLightSectionStorage<TSelf> where TSelf : DataLayerStorageMap<TSelf>
{
    private readonly LightLayer _layer;
    protected readonly LightChunkGetter ChunkSource;

    //visibleSectionData is the snapshot external readers see; updatingSectionData is the side the light engine writes
    //Published through Volatile because readers now run outside the light gate: a reader gets one whole snapshot or the other,
    //never a map being mutated. Writers keep the snapshot clean by copying on first touch of a section, so a reader holding a
    //DataLayer never watches it change under its feet
    private TSelf _visibleSectionData;
    protected TSelf VisibleSectionData
    {
        get => Volatile.Read(ref _visibleSectionData);
        private set => Volatile.Write(ref _visibleSectionData, value);
    }

    protected readonly TSelf UpdatingSectionData;

    private bool _hasInconsistencies;

    //Section state table; a value of 0 means absent, maps to defaultReturnValue(0) of the vanilla Long2ByteMap
    protected readonly Dictionary<long, byte> SectionStates = new();

    private readonly HashSet<long> _columnsWithSources = new();
    protected readonly HashSet<long> ChangedSections = new();
    protected readonly HashSet<long> SectionsAffectedByLightUpdates = new();

    //queuedSections, layer data pending write; the packet builder reads it outside the light gate, so it is a concurrent map
    //Vanilla synchronizes this map for the same reason
    protected readonly ConcurrentDictionary<long, DataLayer> QueuedSections = new();

    private readonly HashSet<long> _columnsToRetainQueuedDataFor = new();
    private readonly HashSet<long> _toRemove = new();

    //getLightValue: subclasses read the light level of the given block node per layer semantics
    protected internal abstract int GetLightValue(long blockNode);

    protected LayerLightSectionStorage(LightLayer lightLayer, LightChunkGetter lightChunkGetter, TSelf map)
    {
        _layer = lightLayer;
        ChunkSource = lightChunkGetter;
        UpdatingSectionData = map;
        VisibleSectionData = map.Copy();
    }

    //hasInconsistencies, whether there are section add/removes pending landing
    protected internal bool HasInconsistencies => _hasInconsistencies;

    protected internal bool StoringLightForSection(long sectionNode) => GetDataLayer(sectionNode, true) is not null;

    protected DataLayer? GetDataLayer(long sectionNode, bool updating)
        => GetDataLayer(updating ? UpdatingSectionData : VisibleSectionData, sectionNode);

    protected DataLayer? GetDataLayer(TSelf sections, long sectionNode) => sections.GetLayer(sectionNode);

    //getDataLayerToWrite gets the layer to write; on first write it copies to avoid polluting the visible snapshot
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

    //getDataLayerData returns queued layer data first, so external reads get the latest not-yet-landed result
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

    //markSectionAndNeighborsAsAffected marks the section and its 26 neighbors as affected by light updates
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

    //markNewInconsistencies lands section add/removes; the vanilla LightEngine parameter is unused and so not kept
    protected internal void MarkNewInconsistencies()
    {
        if (!_hasInconsistencies) return;
        _hasInconsistencies = false;

        foreach (var node in _toRemove)
        {
            QueuedSections.TryRemove(node, out var queued);
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

        //Write the queued layer data into the updating side; only sections already storing light are handled
        foreach (var sectionNode in QueuedSections.Keys.ToList())
        {
            if (!StoringLightForSection(sectionNode)) continue;
            if (!QueuedSections.TryGetValue(sectionNode, out var data)) continue;
            if (!ReferenceEquals(UpdatingSectionData.GetLayer(sectionNode), data))
            {
                UpdatingSectionData.SetLayer(sectionNode, data);
                ChangedSections.Add(sectionNode);
            }
            QueuedSections.TryRemove(sectionNode, out _);
        }
    }

    //onNodeAdded: subclasses add layer semantics when a section is added
    protected virtual void OnNodeAdded(long sectionNode) { }

    //onNodeRemoved: subclasses add layer semantics when a section is removed
    protected virtual void OnNodeRemoved(long sectionNode) { }

    protected internal void SetLightEnabled(long zeroNode, bool enable)
    {
        if (enable) _columnsWithSources.Add(zeroNode);
        else _columnsWithSources.Remove(zeroNode);
    }

    protected internal bool LightOnInSection(long sectionNode)
        => _columnsWithSources.Contains(SectionPos.GetZeroNode(sectionNode));

    protected internal bool LightOnInColumn(long sectionZeroNode) => _columnsWithSources.Contains(sectionZeroNode);

    //retainData, whether to keep queued data when a section is removed
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
            QueuedSections.TryRemove(sectionNode, out _);
        }
    }

    //updateSectionStatus syncs the neighbor counts of itself and its 26 neighbors when a section's empty flag changes
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

    //swapSectionMap snapshots the updating side into visible and calls back light updates for all affected sections
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

    //SectionState, section state bit packing, maps to vanilla SectionState
    //The top 3 bits are reserved, bit 5 is the hasData flag, and the low 5 bits are the count of the 26 neighbors that have data
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
                throw new ArgumentOutOfRangeException(nameof(neighborCount), "Neighbor count must be within [0; 26]");
            return (byte)((state & -32) | (neighborCount & NeighborCountBits));
        }

        public static bool HasData(byte state) => (state & HasDataBit) != 0;

        public static int GetNeighborCount(byte state) => state & NeighborCountBits;

        public static SectionType GetType(byte state)
            => state == 0 ? SectionType.Empty : HasData(state) ? SectionType.LightAndData : SectionType.LightOnly;
    }
}

//SectionType, section light state category, maps to vanilla SectionType
public enum SectionType
{
    Empty,
    LightOnly,
    LightAndData,
}
