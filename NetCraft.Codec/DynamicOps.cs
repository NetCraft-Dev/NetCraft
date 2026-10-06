namespace NetCraft.Codec;

//Abstract type-operation interface, mirroring vanilla com.mojang.serialization.DynamicOps
//Defines how to create/read/merge elements of some type T; NbtOps implements DynamicOps<Tag>
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

    //Vanilla createNumeric takes a Number; this port uses double throughout
    T CreateNumeric(double value);

    T CreateString(string value);

    T CreateList(IEnumerable<T> stream);

    T CreateMap(IEnumerable<Pair<T, T>> map);

    //createByteList defaults to CreateList + CreateByte, mirroring vanilla createByteList
    T CreateByteList(IEnumerable<byte> stream) => CreateList(stream.Select(CreateByte));

    //createIntList defaults to CreateList + CreateInt, mirroring vanilla createIntList
    T CreateIntList(IEnumerable<int> stream) => CreateList(stream.Select(CreateInt));

    //createLongList defaults to CreateList + CreateLong, mirroring vanilla createLongList
    T CreateLongList(IEnumerable<long> stream) => CreateList(stream.Select(CreateLong));

    //Vanilla getNumberValue returns a Number, simplified to double here
    DataResult<double> GetNumberValue(T input);

    //Exact version of vanilla getNumberValue for longs
    //Going through NumberValue passes an integer through double, so values wider than 53 bits get rounded
    //Chunk block_states data and heightmaps are 64-bit packed ints, and losing precision would scramble the indices
    //Ops with a native integer format, such as NbtOps, must override this method
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

    //Convert input of the current ops into an element of the target ops
    U ConvertTo<U>(DynamicOps<U> ops, T input);

    //Returns a record builder for MapCodec field accumulation, mirroring vanilla mapBuilder
    RecordBuilder<T> MapBuilder();
}
