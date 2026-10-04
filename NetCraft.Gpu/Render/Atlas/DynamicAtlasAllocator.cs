namespace NetCraft.Gpu;

//DynamicAtlasAllocator<K> 固定网格槽位分配器对标原版 DynamicAtlasAllocator
//图集纹理按 width×height 网格切分每个槽位 1 单位×1 单位（实际像素由调用方 slotTextureSize 决定）
//用 BitSet 标记空闲槽位 nextSetBit O(1) 找空位分配
//reclaimSpaceFor 释放非目标 key 槽位腾出空间 endFrame 释放 discardAfterFrame 槽位
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

    //ReclaimSpaceFor 释放非目标 key 槽位腾出空间直到能容纳 keys
    //keys 全部已在用返回 true 否则释放非 keys 槽位直到 needSpaceFor=0
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

    //EndFrame 释放 discardAfterFrame 标记的槽位对标原版 endFrame
    //动画物品每帧重画 discardAfterFrame=true 帧末释放避免占满图集
    public void EndFrame()
    {
        FreeSlotIf((_, slot) => slot.DiscardAfterFrame);
    }

    //FreeSlotIf 按 predicate 释放槽位归还到 freeSlots
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

    //HasSpaceForAll keys 与已用槽位并集是否不超过总槽位数
    public bool HasSpaceForAll(IReadOnlySet<K> keys)
    {
        var unionCount = _usedSlotByKey.Count;
        foreach (var key in keys)
            if (!_usedSlotByKey.ContainsKey(key)) unionCount++;
        return unionCount <= _slots.Count;
    }

    //GetOrAllocate 按 key 查槽位命中返回并标记 READY 否则找空闲槽位分配
    //discardAfterFrame 动画物品 true 帧末释放
    //返回 null 表示图集满需调 ReclaimSpaceFor 腾空间
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

    //FreeSlotCount 剩余空闲槽位数测试用
    public int FreeSlotCount => _slots.Count - _usedSlotByKey.Count;

    //UsedSlotKeys 已用槽位 key 集合测试用
    public IReadOnlyCollection<K> UsedSlotKeys => _usedSlotByKey.Keys;

    //Slot 槽位记录图集网格坐标和状态
    public sealed class Slot
    {
        public int X { get; }
        public int Y { get; }
        //DiscardAfterFrame 帧末释放动画物品 true
        public bool DiscardAfterFrame;
        //Fresh 首次分配标记首次后改 false 后续分配走 STALE 路径
        public bool Fresh = true;
        //ExternalState EMPTY 新槽位 STALE 有旧数据需清 READY 已就绪
        public SlotState ExternalState = SlotState.Empty;

        internal Slot(int x, int y)
        {
            X = x;
            Y = y;
        }

        public SlotState State => ExternalState;
    }

    //SlotState 槽位状态机对标原版
    public enum SlotState
    {
        //Empty 新槽位首次分配 fresh=true 改 false 后首次
        Empty,
        //Stale 非首次分配有旧数据需上传前清
        Stale,
        //Ready 已就绪有数据可直接 blit
        Ready
    }
}
