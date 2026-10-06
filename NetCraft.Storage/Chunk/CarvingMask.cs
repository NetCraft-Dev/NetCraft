namespace NetCraft.Storage.Chunk;

//CarvingMask, carving mask, maps to vanilla net.minecraft.world.level.chunk.CarvingMask
//Indexed by x|z<<4|(y-minY)<<8, records blocks already carved; the same position is never carved twice
//Also keeps a compact long array form for saves; trailing all-zero words are trimmed, matching vanilla toLongArray
public sealed class CarvingMask
{
    //Mask, the external supplementary mask, maps to vanilla CarvingMask.Mask; used during structure generation to exclude structure bounds
    public interface Mask
    {
        bool Test(int x, int y, int z);
    }

    private readonly int _minY;
    private long[] _words;
    private Mask? _additionalMask;

    public CarvingMask(int height, int minY)
    {
        _minY = minY;
        _words = new long[(256L * height + 63L) >> 6];
    }

    public CarvingMask(long[] array, int minY)
    {
        _minY = minY;
        _words = (long[])array.Clone();
    }

    public void SetAdditionalMask(Mask mask) => _additionalMask = mask;

    //GetIndex builds the bit index from 4 bits of x and z plus y relative to minY, maps to vanilla getIndex
    private int GetIndex(int x, int y, int z) => (x & 15) | ((z & 15) << 4) | ((y - _minY) << 8);

    public void Set(int x, int y, int z)
    {
        var index = GetIndex(x, y, z);
        var word = index >> 6;
        //Vanilla BitSet grows automatically; out-of-range bits are handled by growing
        if (word >= _words.Length) Array.Resize(ref _words, word + 1);
        _words[word] |= 1L << index;
    }

    public bool Get(int x, int y, int z)
    {
        if (_additionalMask is not null && _additionalMask.Test(x, y, z)) return true;
        var index = GetIndex(x, y, z);
        var word = index >> 6;
        if (word < 0 || word >= _words.Length) return false;
        return (_words[word] & (1L << index)) != 0L;
    }

    //ToArray exports a compact long array, maps to vanilla toLongArray
    public long[] ToArray()
    {
        var length = _words.Length;
        while (length > 0 && _words[length - 1] == 0L) length--;
        var result = new long[length];
        Array.Copy(_words, result, length);
        return result;
    }
}
