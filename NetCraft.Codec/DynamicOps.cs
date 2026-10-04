namespace NetCraft.Codec;

//抽象类型操作接口对应原版com.mojang.serialization.DynamicOps
//定义如何创建/读取/合并某种类型T的元素NbtOps实现DynamicOps<Tag>
public interface DynamicOps<T>
{
    T Empty();

    T EmptyList();

    T EmptyMap();

    T CreateByte(byte value);

    T CreateShort(short value);

    T CreateInt(int value);

    T CreateLong(long value);

    T CreateFloat(float value);

    T CreateDouble(double value);

    T CreateBoolean(bool value);

    //原版createNumeric接收Number统一用double
    T CreateNumeric(double value);

    T CreateString(string value);

    T CreateList(IEnumerable<T> stream);

    T CreateMap(IEnumerable<Pair<T, T>> map);

    //createByteList默认转CreateList+CreateByte对应原版createByteList
    T CreateByteList(IEnumerable<byte> stream) => CreateList(stream.Select(CreateByte));

    //createIntList默认转CreateList+CreateInt对应原版createIntList
    T CreateIntList(IEnumerable<int> stream) => CreateList(stream.Select(CreateInt));

    //createLongList默认转CreateList+CreateLong对应原版createLongList
    T CreateLongList(IEnumerable<long> stream) => CreateList(stream.Select(CreateLong));

    //原版getNumberValue返回Number简化为double
    DataResult<double> GetNumberValue(T input);

    //原版getNumberValue取long的精确版本
    //走NumberValue会经过double超过53位的整数被舍入
    //区块block_states的data与heightmaps都是int64位打包数据丢精度会让索引错乱
    //整数原生格式如NbtOps必须覆写此方法
    DataResult<long> GetLongValue(T input) => GetNumberValue(input).Map(v => (long)v);

    DataResult<string> GetStringValue(T input);

    DataResult<bool> GetBooleanValue(T input);

    DataResult<T> MergeToList(T list, T value);

    DataResult<T> MergeToList(T list, IReadOnlyList<T> values);

    DataResult<T> MergeToMap(T map, T key, T value);

    DataResult<T> MergeToMap(T map, MapLike<T> values);

    DataResult<T> MergeToMap(T map, IReadOnlyDictionary<T, T> values);

    DataResult<MapLike<T>> GetMap(T input);

    DataResult<IEnumerable<Pair<T, T>>> GetMapValues(T input);

    DataResult<IEnumerable<T>> GetStream(T input);

    T Remove(T input, string key);

    //把当前ops的input转换为目标ops的元素
    U ConvertTo<U>(DynamicOps<U> ops, T input);

    //返回record builder用于MapCodec累积字段对应原版mapBuilder
    RecordBuilder<T> MapBuilder();
}
