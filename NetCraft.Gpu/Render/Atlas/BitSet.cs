using System.Runtime.CompilerServices;

namespace NetCraft.Gpu;

//BitSet fixed-length bit set, maps to java.util.BitSet, used by DynamicAtlasAllocator
//Set/Clear/NextSetBit O(1) with internal storage bucketed by ulong
public sealed class BitSet
{
    private readonly ulong[] _bits;
    private readonly int _size;

    public BitSet(int size)
    {
        _size = size;
        _bits = new ulong[(size + 63) >> 6];
    }

    //Set sets the bits in the range [from,to) to 1
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int from, int to)
    {
        for (var i = from; i < to; i++) Set(i);
    }

    //Set sets a single bit to 1
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int index)
    {
        if ((uint)index >= (uint)_size) return;
        _bits[index >> 6] |= 1UL << (index & 63);
    }

    //Clear sets a single bit to 0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear(int index)
    {
        if ((uint)index >= (uint)_size) return;
        _bits[index >> 6] &= ~(1UL << (index & 63));
    }

    //NextSetBit finds the next bit set to 1 starting at from and returns its index, or -1 if none
    //Uses System.Numerics.BitOperations.TrailingZeroCount to compute the lowest set bit
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
