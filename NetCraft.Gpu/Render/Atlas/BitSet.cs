using System.Runtime.CompilerServices;

namespace NetCraft.Gpu;

//BitSet 固定长度位集合对标 java.util.BitSet 供 DynamicAtlasAllocator 用
//Set/Clear/NextSetBit O(1) 按 ulong 分桶内部存储
public sealed class BitSet
{
    private readonly ulong[] _bits;
    private readonly int _size;

    public BitSet(int size)
    {
        _size = size;
        _bits = new ulong[(size + 63) >> 6];
    }

    //Set 把 [from,to) 范围位设为 1
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int from, int to)
    {
        for (var i = from; i < to; i++) Set(i);
    }

    //Set 单位设为 1
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int index)
    {
        if ((uint)index >= (uint)_size) return;
        _bits[index >> 6] |= 1UL << (index & 63);
    }

    //Clear 单位设为 0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear(int index)
    {
        if ((uint)index >= (uint)_size) return;
        _bits[index >> 6] &= ~(1UL << (index & 63));
    }

    //NextSetBit 从 from 开始找下一个为 1 的位返回索引无返回 -1
    //用 System.Numerics.BitOperations.TrailingZeroCount 计算最低有效位
    public int NextSetBit(int from)
    {
        if (from < 0) from = 0;
        if (from >= _size) return -1;
        var wordIndex = from >> 6;
        var bitInWord = from & 63;
        var word = _bits[wordIndex] & (~0UL << bitInWord);
        while (true)
        {
            if (word != 0)
            {
                var idx = (wordIndex << 6) + System.Numerics.BitOperations.TrailingZeroCount(word);
                return idx < _size ? idx : -1;
            }
            wordIndex++;
            if (wordIndex >= _bits.Length) return -1;
            word = _bits[wordIndex];
        }
    }
}
