namespace NetCraft.Gpu;

//DynamicAtlasAllocator<K> fixed-grid slot allocator, maps to vanilla DynamicAtlasAllocator
//The atlas texture is split into a width×height grid, each slot 1×1 unit (actual pixels decided by the caller's slotTextureSize)
//Uses a BitSet to mark free slots; nextSetBit O(1) finds a slot to allocate
//reclaimSpaceFor frees non-target key slots to make room; endFrame frees discardAfterFrame slots
public sealed class DynamicAtlasAllocator<K> where K : notnull
{
    private readonly int _width;
    private readonly List<Slot> _slots;
    private readonly Dictionary<K, Slot> _usedSlotByKey = new();
    private readonly BitSet _freeSlots;

    public DynamicAtlasAllocator(int width, int height)
    {
        _width = width;
        var size = width * height;
        _slots = new List<Slot>(size);
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            _slots.Add(new Slot(x, y));
        _freeSlots = new BitSet(size);
        _freeSlots.Set(0, size);
    }

    //ReclaimSpaceFor frees non-target key slots to make room until keys fit
    //Returns true if all keys are already in use; otherwise frees non-key slots until needSpaceFor=0
    public bool ReclaimSpaceFor(IReadOnlySet<K> keys)
    {
        var preexisting = 0;
        foreach (var key in keys)
            if (_usedSlotByKey.ContainsKey(key)) preexisting++;
        if (preexisting == keys.Count) return true;

        var needSpaceFor = keys.Count - preexisting;
        FreeSlotIf((key, _) =>
        {
            if (needSpaceFor == 0 || keys.Contains(key)) return false;
            needSpaceFor--;
            return true;
        });
        return needSpaceFor == 0;
    }

    //EndFrame frees slots marked discardAfterFrame, maps to vanilla endFrame
    //Animated items redraw every frame with discardAfterFrame=true and are freed at frame end to avoid filling the atlas
    public void EndFrame()
    {
        FreeSlotIf((_, slot) => slot.DiscardAfterFrame);
    }

    //FreeSlotIf frees slots matching the predicate and returns them to freeSlots
    private void FreeSlotIf(Func<K, Slot, bool> predicate)
    {
        var keysToRemove = new List<K>();
        foreach (var (key, slot) in _usedSlotByKey)
        {
            if (!predicate(key, slot)) continue;
            _freeSlots.Set(slot.X + slot.Y * _width);
            slot.DiscardAfterFrame = false;
            keysToRemove.Add(key);
        }
        foreach (var key in keysToRemove) _usedSlotByKey.Remove(key);
    }

    //HasSpaceForAll whether the union of keys and used slots fits within the total slot count
    public bool HasSpaceForAll(IReadOnlySet<K> keys)
    {
        var unionCount = _usedSlotByKey.Count;
        foreach (var key in keys)
            if (!_usedSlotByKey.ContainsKey(key)) unionCount++;
        return unionCount <= _slots.Count;
    }

    //GetOrAllocate looks up a slot by key, returns and marks READY on a hit, otherwise finds a free slot to allocate
    //discardAfterFrame true for animated items, freed at frame end
    //Returns null when the atlas is full and ReclaimSpaceFor must be called to make room
    public Slot? GetOrAllocate(K key, bool discardAfterFrame)
    {
        if (_usedSlotByKey.TryGetValue(key, out var usedSlot))
        {
            usedSlot.DiscardAfterFrame |= discardAfterFrame;
            usedSlot.ExternalState = SlotState.Ready;
            return usedSlot;
        }
        var freeSlotIndex = _freeSlots.NextSetBit(0);
        if (freeSlotIndex < 0) return null;
        var freeSlot = _slots[freeSlotIndex];
        freeSlot.ExternalState = freeSlot.Fresh ? SlotState.Empty : SlotState.Stale;
        freeSlot.Fresh = false;
        freeSlot.DiscardAfterFrame = discardAfterFrame;
        _usedSlotByKey[key] = freeSlot;
        _freeSlots.Clear(freeSlotIndex);
        return freeSlot;
    }

    //FreeSlotCount remaining free slot count, for tests
    public int FreeSlotCount => _slots.Count - _usedSlotByKey.Count;

    //UsedSlotKeys set of used slot keys, for tests
    public IReadOnlyCollection<K> UsedSlotKeys => _usedSlotByKey.Keys;

    //Slot slot record with atlas grid coordinates and state
    public sealed class Slot
    {
        public int X { get; }
        public int Y { get; }
        //DiscardAfterFrame true for animated items freed at frame end
        public bool DiscardAfterFrame;
        //Fresh marks the first allocation; set false afterward so later allocations take the STALE path
        public bool Fresh = true;
        //ExternalState EMPTY new slot STALE has old data to clear READY ready
        public SlotState ExternalState = SlotState.Empty;

        internal Slot(int x, int y)
        {
            X = x;
            Y = y;
        }

        public SlotState State => ExternalState;
    }

    //SlotState slot state machine, maps to vanilla
    public enum SlotState
    {
        //Empty new slot first allocation, fresh set to false afterward
        Empty,
        //Stale non-first allocation with old data that must be cleared before upload
        Stale,
        //Ready ready with data, can blit directly
        Ready
    }
}
