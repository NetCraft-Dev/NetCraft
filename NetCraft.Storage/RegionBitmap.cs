namespace NetCraft.Storage;

//Sector bitmap, maps to vanilla RegionBitmap
//Records sectors already used in the MCA file; allocate finds a contiguous free run
//Vanilla uses a BitSet that grows dynamically; here a long[] doubles on demand
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

    //Scan from the start for a run of size free sectors, mark them and return the start index
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

    //Find the next clear bit from from; beyond the used range counts as clear
    private int NextClearBit(int from)
    {
        for (int i = from; i < _length; i++)
            if ((_bits[i >> 6] & (1L << (i & 63))) == 0) return i;
        return _length;
    }

    //Find the next set bit from from, or -1 when none
    private int NextSetBit(int from)
    {
        for (int i = from; i < _length; i++)
            if ((_bits[i >> 6] & (1L << (i & 63))) != 0) return i;
        return -1;
    }
}
