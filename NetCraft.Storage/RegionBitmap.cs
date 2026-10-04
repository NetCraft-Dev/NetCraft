namespace NetCraft.Storage;

//扇区位图对应原版RegionBitmap
//记录MCA文件中已占用的扇区，allocate找连续空闲段
//原版用BitSet动态扩展，此处用long[]按需翻倍扩容
public sealed class RegionBitmap
{
    private long[] _bits = new long[1];
    private int _length;

    public void Force(int position, int size)
    {
        int end = position + size;
        for (int i = position; i < end; i++)
        {
            EnsureCapacity(i);
            _bits[i >> 6] |= 1L << (i & 63);
        }
    }

    public void Free(int position, int size)
    {
        int end = position + size;
        for (int i = position; i < end; i++)
        {
            int longIndex = i >> 6;
            if (longIndex >= _bits.Length) break;
            _bits[longIndex] &= ~(1L << (i & 63));
        }
    }

    //从头扫描找连续size个空闲扇区的起点，标记后返回
    public int Allocate(int size)
    {
        int i = 0;
        while (true)
        {
            int freeStart = NextClearBit(i);
            int freeEnd = NextSetBit(freeStart);
            if (freeEnd == -1 || freeEnd - freeStart >= size)
            {
                Force(freeStart, size);
                return freeStart;
            }
            i = freeEnd;
        }
    }

    private void EnsureCapacity(int bitIndex)
    {
        int longIndex = bitIndex >> 6;
        if (longIndex >= _bits.Length)
        {
            int newLen = _bits.Length;
            while (longIndex >= newLen) newLen *= 2;
            Array.Resize(ref _bits, newLen);
        }
        if (bitIndex >= _length) _length = bitIndex + 1;
    }

    //从from找下一个未置位的位置，超出已用范围视为clear
    private int NextClearBit(int from)
    {
        for (int i = from; i < _length; i++)
            if ((_bits[i >> 6] & (1L << (i & 63))) == 0) return i;
        return _length;
    }

    //从from找下一个已置位的位置，无则返回-1
    private int NextSetBit(int from)
    {
        for (int i = from; i < _length; i++)
            if ((_bits[i >> 6] & (1L << (i & 63))) != 0) return i;
        return -1;
    }
}
