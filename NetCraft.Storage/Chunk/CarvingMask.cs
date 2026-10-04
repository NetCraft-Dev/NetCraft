namespace NetCraft.Storage.Chunk;

//CarvingMask 雕刻标记对应原版 net.minecraft.world.level.chunk.CarvingMask
//按 x|z<<4|(y-minY)<<8 索引记录已被雕刻过的方块 同一位置不会重复雕刻
//同时给存档留 long 数组的紧凑形式 尾部全零的字会被裁掉与原版 toLongArray 一致
public sealed class CarvingMask
{
    //Mask 外部补充掩码对应原版 CarvingMask.Mask 结构生成期用它排除结构范围
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

    //GetIndex 位索引按 x/z 各 4 位与相对最低位的 y 拼成对应原版 getIndex
    private int GetIndex(int x, int y, int z) => (x & 15) | ((z & 15) << 4) | ((y - _minY) << 8);

    public void Set(int x, int y, int z)
    {
        var index = GetIndex(x, y, z);
        var word = index >> 6;
        //原版 BitSet 会自动扩容 越界位按扩容处理
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

    //ToArray 导出紧凑 long 数组对应原版 toLongArray
    public long[] ToArray()
    {
        var length = _words.Length;
        while (length > 0 && _words[length - 1] == 0L) length--;
        var result = new long[length];
        Array.Copy(_words, result, length);
        return result;
    }
}
