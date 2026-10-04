namespace NetCraft.Storage.Chunk;

//光照数据层对应原版net.minecraft.world.level.chunk.DataLayer
//2048字节byte数组存4096个4bit值用于光照存储
public sealed class DataLayer
{
    public const int LayerCount = 16;
    public const int LayerSize = 128;
    public const int Size = 2048;

    private byte[]? _data;
    private int _defaultValue;

    public DataLayer() : this(0) { }

    public DataLayer(int defaultValue) => _defaultValue = defaultValue;

    public DataLayer(byte[] data)
    {
        if (data.Length != Size)
            throw new ArgumentException($"DataLayer should be {Size} bytes not: {data.Length}");
        _data = data;
        _defaultValue = 0;
    }

    public int Get(int x, int y, int z) => Get(GetIndex(x, y, z));

    public void Set(int x, int y, int z, int value) => Set(GetIndex(x, y, z), value);

    private static int GetIndex(int x, int y, int z) => (y << 8) | (z << 4) | x;

    private int Get(int index)
    {
        if (_data is null) return _defaultValue;
        var position = GetByteIndex(index);
        var nibble = GetNibbleIndex(index);
        return (_data[position] >> (4 * nibble)) & 15;
    }

    private void Set(int index, int value)
    {
        var data = GetData();
        var position = GetByteIndex(index);
        var nibble = GetNibbleIndex(index);
        var mask = (15 << (4 * nibble)) ^ -1;
        var valueToSet = (value & 15) << (4 * nibble);
        data[position] = (byte)((data[position] & mask) | valueToSet);
    }

    private static int GetNibbleIndex(int index) => index & 1;

    private static int GetByteIndex(int position) => position >> 1;

    public void Fill(int value)
    {
        _defaultValue = value;
        _data = null;
    }

    //把全填充值打包成字节对应原版packFilled
    private static byte PackFilled(int value)
    {
        var packed = (byte)value;
        for (var i = 4; i < 8; i += 4)
            packed = (byte)(packed | (value << i));
        return packed;
    }

    public byte[] GetData()
    {
        if (_data is null)
        {
            _data = new byte[Size];
            if (_defaultValue != 0) Array.Fill(_data, PackFilled(_defaultValue));
        }
        return _data;
    }

    public DataLayer Copy()
        => _data is null ? new DataLayer(_defaultValue) : new DataLayer((byte[])_data.Clone());

    public bool IsDefinitelyHomogenous => _data is null;

    public bool IsDefinitelyFilledWith(int value) => _data is null && _defaultValue == value;

    public bool IsEmpty => _data is null && _defaultValue == 0;
}
