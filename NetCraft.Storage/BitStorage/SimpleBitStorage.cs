namespace NetCraft.Storage;

//基础位存储对应原版net.minecraft.util.SimpleBitStorage
//每元素占固定bits紧凑存储在long数组中
public sealed class SimpleBitStorage : BitStorage
{
    //MAGIC数组对应原版static MAGIC查表用valuesPerLong-1索引
    //值含负数0xFFFFFFFF作为uint处理C#用int保留位模式
    private static readonly int[] Magic = BuildMagic();

    private readonly long[] _data;
    private readonly int _bits;
    private readonly long _mask;
    private readonly int _size;
    private readonly int _valuesPerLong;
    private readonly uint _divideMul;
    private readonly uint _divideAdd;
    private readonly int _divideShift;

    //初始化异常对应原版InitializationException
    public sealed class InitializationException : Exception
    {
        public InitializationException(string message) : base(message) { }
    }

    //从int[] values构建紧凑存储对应原版(bits,size,values)
    public SimpleBitStorage(int bits, int size, int[] values) : this(bits, size, (long[]?)null)
    {
        var outputIndex = 0;
        var inputOffset = 0;
        while (inputOffset <= _size - _valuesPerLong)
        {
            long packedValue = 0;
            for (var indexInLong = _valuesPerLong - 1; indexInLong >= 0; indexInLong--)
                packedValue = (packedValue << bits) | (values[inputOffset + indexInLong] & _mask);
            _data[outputIndex++] = packedValue;
            inputOffset += _valuesPerLong;
        }
        var remainderCount = _size - inputOffset;
        if (remainderCount > 0)
        {
            long lastPackedValue = 0;
            for (var indexInLong2 = remainderCount - 1; indexInLong2 >= 0; indexInLong2--)
                lastPackedValue = (lastPackedValue << bits) | (values[inputOffset + indexInLong2] & _mask);
            _data[outputIndex] = lastPackedValue;
        }
    }

    public SimpleBitStorage(int bits, int size) : this(bits, size, (long[]?)null) { }

    //从long[] data构建紧凑存储对应原版(bits,size,data)
    public SimpleBitStorage(int bits, int size, long[]? data)
    {
        if (bits < 1 || bits > 32)
            throw new ArgumentOutOfRangeException(nameof(bits));
        _size = size;
        _bits = bits;
        _mask = (1L << bits) - 1;
        _valuesPerLong = 64 / bits;
        var row = 3 * (_valuesPerLong - 1);
        _divideMul = (uint)Magic[row + 0];
        _divideAdd = (uint)Magic[row + 1];
        _divideShift = Magic[row + 2];
        var requiredLength = (size + _valuesPerLong - 1) / _valuesPerLong;
        if (data != null)
        {
            if (data.Length != requiredLength)
                throw new InitializationException($"Invalid length given for storage, got: {data.Length} but expected: {requiredLength}");
            _data = data;
        }
        else
        {
            _data = new long[requiredLength];
        }
    }

    public int Size => _size;
    public int Bits => _bits;

    private int CellIndex(int bitIndex)
    {
        var mul = _divideMul;
        var add = _divideAdd;
        return (int)((((ulong)bitIndex * mul) + add) >> 32) >> _divideShift;
    }

    public int Get(int index)
    {
        ValidateIndex(index);
        var cellIndex = CellIndex(index);
        var cellValue = _data[cellIndex];
        var bitIndex = (index - cellIndex * _valuesPerLong) * _bits;
        return (int)((cellValue >> bitIndex) & _mask);
    }

    public int GetAndSet(int index, int value)
    {
        ValidateIndex(index);
        ValidateValue(value);
        var cellIndex = CellIndex(index);
        var cellValue = _data[cellIndex];
        var bitIndex = (index - cellIndex * _valuesPerLong) * _bits;
        var oldValue = (int)((cellValue >> bitIndex) & _mask);
        _data[cellIndex] = (cellValue & ((_mask << bitIndex) ^ -1L)) | (((long)value & _mask) << bitIndex);
        return oldValue;
    }

    public void Set(int index, int value)
    {
        ValidateIndex(index);
        ValidateValue(value);
        var cellIndex = CellIndex(index);
        var cellValue = _data[cellIndex];
        var bitIndex = (index - cellIndex * _valuesPerLong) * _bits;
        _data[cellIndex] = (cellValue & ((_mask << bitIndex) ^ -1L)) | (((long)value & _mask) << bitIndex);
    }

    public long[] GetRaw() => _data;

    public void GetAll(Action<int> output)
    {
        var count = 0;
        for (var i = 0; i < _data.Length; i++)
        {
            var cellValue = _data[i];
            for (var v = 0; v < _valuesPerLong; v++)
            {
                output((int)(cellValue & _mask));
                cellValue >>= _bits;
                count++;
                if (count >= _size) return;
            }
        }
    }

    public void Unpack(int[] output)
    {
        var dataLength = _data.Length;
        var outputOffset = 0;
        for (var i = 0; i < dataLength - 1; i++)
        {
            var cellValue = _data[i];
            for (var indexInLong = 0; indexInLong < _valuesPerLong; indexInLong++)
            {
                output[outputOffset + indexInLong] = (int)(cellValue & _mask);
                cellValue >>= _bits;
            }
            outputOffset += _valuesPerLong;
        }
        var remainder = _size - outputOffset;
        if (remainder > 0)
        {
            var cellValue = _data[dataLength - 1];
            for (var indexInLong2 = 0; indexInLong2 < remainder; indexInLong2++)
            {
                output[outputOffset + indexInLong2] = (int)(cellValue & _mask);
                cellValue >>= _bits;
            }
        }
    }

    public BitStorage Copy() => new SimpleBitStorage(_bits, _size, (long[])_data.Clone());

    private void ValidateIndex(int index)
    {
        if (index < 0 || index >= _size)
            throw new ArgumentOutOfRangeException(nameof(index));
    }

    private void ValidateValue(int value)
    {
        if ((long)value < 0 || (long)value > _mask)
            throw new ArgumentOutOfRangeException(nameof(value));
    }

    //MAGIC表对应原版static MAGIC数组
    //包含divideMul/divideAdd/divideShift三元组供CellIndex快速除法
    private static int[] BuildMagic()
    {
        return new int[]
        {
            -1, -1, 0, int.MinValue, 0, 0, 1431655765, 1431655765, 0, int.MinValue, 0, 1,
            858993459, 858993459, 0, 715827882, 715827882, 0, 613566756, 613566756, 0, int.MinValue, 0, 2,
            477218588, 477218588, 0, 429496729, 429496729, 0, 390451572, 390451572, 0, 357913941, 357913941, 0,
            330382099, 330382099, 0, 306783378, 306783378, 0, 286331153, 286331153, 0, int.MinValue, 0, 3,
            252645135, 252645135, 0, 238609294, 238609294, 0, 226050910, 226050910, 0, 214748364, 214748364, 0,
            204522252, 204522252, 0, 195225786, 195225786, 0, 186737708, 186737708, 0, 178956970, 178956970, 0,
            171798691, 171798691, 0, 165191049, 165191049, 0, 159072862, 159072862, 0, 153391689, 153391689, 0,
            148102320, 148102320, 0, 143165576, 143165576, 0, 138547332, 138547332, 0, int.MinValue, 0, 4,
            130150524, 130150524, 0, 126322567, 126322567, 0, 122713351, 122713351, 0, 119304647, 119304647, 0,
            116080197, 116080197, 0, 113025455, 113025455, 0, 110127366, 110127366, 0, 107374182, 107374182, 0,
            104755299, 104755299, 0, 102261126, 102261126, 0, 99882960, 99882960, 0, 97612893, 97612893, 0,
            95443717, 95443717, 0, 93368854, 93368854, 0, 91382282, 91382282, 0, 89478485, 89478485, 0,
            87652393, 87652393, 0, 85899345, 85899345, 0, 84215045, 84215045, 0, 82595524, 82595524, 0,
            81037118, 81037118, 0, 79536431, 79536431, 0, 78090314, 78090314, 0, 76695844, 76695844, 0,
            75350303, 75350303, 0, 74051160, 74051160, 0, 72796055, 72796055, 0, 71582788, 71582788, 0,
            70409299, 70409299, 0, 69273666, 69273666, 0, 68174084, 68174084, 0, int.MinValue, 0, 5
        };
    }
}
