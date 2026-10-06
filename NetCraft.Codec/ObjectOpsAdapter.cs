#nullable disable
namespace NetCraft.Codec;

using System.Collections.Generic;
using System.Linq;

//Adapter from a generic DynamicOps<T> to DynamicOps<object>, matching the DynamicOps<?> wildcard semantics of Java type erasure
//C# generic invariance forbids converting T to object directly, so the adapter wraps the calls in delegates
//Used by EmptyPartPassthrough and similar places that need to treat any ops as object ops
//Requires T to be a reference type; value types are unsupported. Real DFU only uses NbtOps with T=Tag
public sealed class ObjectOpsAdapter<T> : DynamicOps<object>
{
    private readonly DynamicOps<T> _inner;

    public ObjectOpsAdapter(DynamicOps<T> inner) { _inner = inner; }

    public object Empty() => _inner.Empty();
    public object EmptyList() => _inner.EmptyList();
    public object EmptyMap() => _inner.EmptyMap();

    public object CreateByte(byte value) => _inner.CreateByte(value);
    public object CreateShort(short value) => _inner.CreateShort(value);
    public object CreateInt(int value) => _inner.CreateInt(value);
    public object CreateLong(long value) => _inner.CreateLong(value);
    public object CreateFloat(float value) => _inner.CreateFloat(value);
    public object CreateDouble(double value) => _inner.CreateDouble(value);
    public object CreateBoolean(bool value) => _inner.CreateBoolean(value);
    public object CreateNumeric(double value) => _inner.CreateNumeric(value);
    public object CreateString(string value) => _inner.CreateString(value);

    public object CreateList(IEnumerable<object> stream)
        => _inner.CreateList(stream.Select(CastT));

    public object CreateMap(IEnumerable<Pair<object, object>> map)
        => _inner.CreateMap(map.Select(p => new Pair<T, T>(CastT(p.First), CastT(p.Second))));

    public object CreateByteList(IEnumerable<byte> stream) => _inner.CreateByteList(stream);
    public object CreateIntList(IEnumerable<int> stream) => _inner.CreateIntList(stream);
    public object CreateLongList(IEnumerable<long> stream) => _inner.CreateLongList(stream);

    public DataResult<double> GetNumberValue(object input)
        => input is T t ? _inner.GetNumberValue(t) : DataResult<double>.Error(() => "Not a " + typeof(T) + ": " + input);

    public DataResult<long> GetLongValue(object input)
        => input is T t ? _inner.GetLongValue(t) : DataResult<long>.Error(() => "Not a " + typeof(T) + ": " + input);

    public DataResult<string> GetStringValue(object input)
        => input is T t ? _inner.GetStringValue(t) : DataResult<string>.Error(() => "Not a " + typeof(T) + ": " + input);

    public DataResult<bool> GetBooleanValue(object input)
        => input is T t ? _inner.GetBooleanValue(t) : DataResult<bool>.Error(() => "Not a " + typeof(T) + ": " + input);

    public DataResult<object> MergeToList(object list, object value)
        => _inner.MergeToList(CastT(list), CastT(value)).Map(v => (object)v);

    public DataResult<object> MergeToList(object list, IReadOnlyList<object> values)
        => _inner.MergeToList(CastT(list), values.Select(CastT).ToArray()).Map(v => (object)v);

    public DataResult<object> MergeToMap(object map, object key, object value)
        => _inner.MergeToMap(CastT(map), CastT(key), CastT(value)).Map(v => (object)v);

    public DataResult<object> MergeToMap(object map, MapLike<object> values)
        => _inner.MergeToMap(CastT(map), new MapLikeObjectToTAdapter<T>(values)).Map(v => (object)v);

    public DataResult<object> MergeToMap(object map, IReadOnlyDictionary<object, object> values)
    {
        var dict = new Dictionary<T, T>();
        foreach (var kv in values) dict[CastT(kv.Key)] = CastT(kv.Value);
        return _inner.MergeToMap(CastT(map), dict).Map(v => (object)v);
    }

    public DataResult<MapLike<object>> GetMap(object input)
        => input is T t
            ? _inner.GetMap(t).Map(m => (MapLike<object>)new MapLikeTToObjectAdapter<T>(m))
            : DataResult<MapLike<object>>.Error(() => "Not a " + typeof(T) + ": " + input);

    public DataResult<IEnumerable<Pair<object, object>>> GetMapValues(object input)
        => input is T t
            ? _inner.GetMapValues(t).Map(s => s.Select(p => new Pair<object, object>(p.First, p.Second)))
            : DataResult<IEnumerable<Pair<object, object>>>.Error(() => "Not a " + typeof(T) + ": " + input);

    public DataResult<IEnumerable<object>> GetStream(object input)
        => input is T t
            ? _inner.GetStream(t).Map(s => s.Select(o => (object)o))
            : DataResult<IEnumerable<object>>.Error(() => "Not a " + typeof(T) + ": " + input);

    public object Remove(object input, string key)
        => input is T t ? _inner.Remove(t, key) : input;

    public U ConvertTo<U>(DynamicOps<U> ops, object input)
        => input is T t ? _inner.ConvertTo(ops, t) : ops.Empty();

    public RecordBuilder<object> MapBuilder()
        => new RecordBuilderTToObjectAdapter<T>(_inner.MapBuilder(), this);

    //A direct cast when T is a reference type, InvalidCastException when it is a value type, matching Java type erasure
    private static T CastT(object o) => (T)o;
}

//Adapter from MapLike<T> to MapLike<object>, delegating the T side to the object side
internal sealed class MapLikeTToObjectAdapter<T> : MapLike<object>
{
    private readonly MapLike<T> _inner;
    public MapLikeTToObjectAdapter(MapLike<T> inner) { _inner = inner; }

    public Optional<object> Get(object key)
        => key is T t ? _inner.Get(t).Map(v => (object)v) : Optional<object>.Empty();

    public Optional<object> Get(string key)
        => _inner.Get(key).Map(v => (object)v);

    public IEnumerable<Pair<object, object>> Entries()
        => _inner.Entries().Select(p => new Pair<object, object>(p.First, p.Second));
}

//Adapter from MapLike<object> to MapLike<T>, delegating the object side to the T side
//Used for the reverse delegation of MergeToMap(MapLike<object>)
internal sealed class MapLikeObjectToTAdapter<T> : MapLike<T>
{
    private readonly MapLike<object> _inner;
    public MapLikeObjectToTAdapter(MapLike<object> inner) { _inner = inner; }

    public Optional<T> Get(T key)
        => _inner.Get(key!).Map(CastT);

    public Optional<T> Get(string key)
        => _inner.Get(key).Map(CastT);

    public IEnumerable<Pair<T, T>> Entries()
        => _inner.Entries().Select(p => new Pair<T, T>(CastT(p.First), CastT(p.Second)));

    private static T CastT(object o) => (T)o;
}

//Adapter from RecordBuilder<T> to RecordBuilder<object>, delegating accumulation to the T side
internal sealed class RecordBuilderTToObjectAdapter<T> : RecordBuilder<object>
{
    private readonly RecordBuilder<T> _inner;
    public RecordBuilderTToObjectAdapter(RecordBuilder<T> inner, DynamicOps<object> ops) { _inner = inner; Ops = ops; }

    public DynamicOps<object> Ops { get; }

    public RecordBuilder<object> Add(string key, object value)
    {
        _inner.Add(key, CastT(value));
        return this;
    }

    public RecordBuilder<object> Add(string key, object value, object prefix)
    {
        _inner.Add(key, CastT(value), CastT(prefix));
        return this;
    }

    public DataResult<object> Build(object prefix)
        => _inner.Build(CastT(prefix)).Map(v => (object)v);

    private static T CastT(object o) => (T)o;
}
