namespace NetCraft.Codec;

//Dynamic value wrapper, mirroring vanilla com.mojang.serialization.Dynamic<T>
//Holds a value under some ops and can convert across ops with Convert
//Provides convenience operations such as Get/AsString/AsStream/Update/Set/Create*
public sealed class Dynamic<T>
{
    public DynamicOps<T> Ops { get; }

    public T Value { get; }

    public Dynamic(DynamicOps<T> ops, T value)
    {
        Ops = ops;
        Value = value;
    }

    //Convert the current value to the target ops with the source ops.ConvertTo
    public Dynamic<U> Convert<U>(DynamicOps<U> ops)
        => new(ops, Ops.ConvertTo(ops, Value));

    public Dynamic<T> WithValue(T value) => new(Ops, value);

    //Empty map, mirroring vanilla emptyMap
    public Dynamic<T> EmptyMap() => new(Ops, Ops.EmptyMap());

    //Empty list, mirroring vanilla emptyList
    public Dynamic<T> EmptyList() => new(Ops, Ops.EmptyList());

    //Get the child Dynamic for a string key, returning an OptionalDynamic that carries the error message on failure
    public OptionalDynamic<T> Get(string key)
        => new(Ops, Ops.GetMap(Value).FlatMap(m =>
            m.Get(key).IsPresent
                ? DataResult<T>.Success(m.Get(key).Get())
                : DataResult<T>.Error(() => "Key not found: " + key)));

    //Get the child Dynamic for a key of type T
    public OptionalDynamic<T> Get(T key)
        => new(Ops, Ops.GetMap(Value).FlatMap(m =>
            m.Get(key).IsPresent
                ? DataResult<T>.Success(m.Get(key).Get())
                : DataResult<T>.Error(() => "Key not found")));

    //===value conversion DataResult===

    public DataResult<double> AsNumber() => Ops.GetNumberValue(Value);

    public double AsNumber(double def) => AsNumber().Result().OrElse(def);

    public DataResult<string> AsString() => Ops.GetStringValue(Value);

    public string AsString(string def) => AsString().Result().OrElse(def);

    public DataResult<bool> AsBoolean() => Ops.GetBooleanValue(Value);

    public bool AsBoolean(bool def) => AsBoolean().Result().OrElse(def);

    public int AsInt(int def) => (int)AsNumber(def);

    public long AsLong(long def) => (long)AsNumber(def);

    public float AsFloat(float def) => (float)AsNumber(def);

    public double AsDouble(double def) => AsNumber(def);

    public byte AsByte(byte def) => (byte)AsNumber(def);

    public short AsShort(short def) => (short)AsNumber(def);

    //===stream conversion===

    //Convert to a Dynamic stream, returning an error DataResult on failure
    public DataResult<IEnumerable<Dynamic<T>>> AsStream()
        => Ops.GetStream(Value).Map(s => s.Select(e => new Dynamic<T>(Ops, e)));

    //Convert to a Dynamic stream, returning an empty sequence on partial failure
    public IEnumerable<Dynamic<T>> AsStreamOpt()
        => AsStream().Result().OrElse(Enumerable.Empty<Dynamic<T>>());

    //Convert to a Pair Dynamic stream, returning an error DataResult on failure
    public DataResult<IEnumerable<Pair<Dynamic<T>, Dynamic<T>>>> AsMap()
        => Ops.GetMapValues(Value).Map(s => s.Select(p =>
            new Pair<Dynamic<T>, Dynamic<T>>(new(Ops, p.First), new(Ops, p.Second))));

    //Convert to a Pair Dynamic stream, returning an empty sequence on partial failure
    public IEnumerable<Pair<Dynamic<T>, Dynamic<T>>> AsMapOpt()
        => AsMap().Result().OrElse(Enumerable.Empty<Pair<Dynamic<T>, Dynamic<T>>>());

    //===mutation operations===

    //Update the child value at a string key with fn and return a new Dynamic
    public Dynamic<T> Update(string key, Func<Dynamic<T>, Dynamic<T>> fn)
        => Set(key, fn(Get(key).OrElse(EmptyMap())));

    //Update the child value at a key of type T with fn
    public Dynamic<T> Update(T key, Func<Dynamic<T>, Dynamic<T>> fn)
        => Set(key, fn(Get(key).OrElse(EmptyMap())));

    //Set the child value at a string key to value and return a new Dynamic
    public Dynamic<T> Set(string key, Dynamic<T> value)
        => new(Ops, Ops.MergeToMap(Value, Ops.CreateString(key), value.Value).GetOrThrow(err => new InvalidOperationException(err)));

    //Set the child value at a key of type T
    public Dynamic<T> Set(T key, Dynamic<T> value)
        => new(Ops, Ops.MergeToMap(Value, key, value.Value).GetOrThrow(err => new InvalidOperationException(err)));

    //Set the value for the key when it exists, keeping the old value otherwise
    public Dynamic<T> SetFieldIfPresent(string key, Optional<Dynamic<T>> value)
        => value.IsPresent ? Set(key, value.Get()) : this;

    //Remove the given string key and return a new Dynamic
    public Dynamic<T> Remove(string key) => new(Ops, Ops.Remove(Value, key));

    //Rename the field oldKey to newKey, keeping the value unchanged
    public Dynamic<T> RenameField(string oldKey, string newKey)
    {
        var opt = Get(oldKey).Result();
        var removed = Remove(oldKey);
        return opt.IsPresent ? removed.Set(newKey, opt.Get()) : removed;
    }

    //Rename the field and apply fn to the value
    public Dynamic<T> RenameAndFixField(string oldKey, string newKey, Func<Dynamic<T>, Dynamic<T>> fn)
    {
        var opt = Get(oldKey).Result();
        var removed = Remove(oldKey);
        return opt.IsPresent ? removed.Set(newKey, fn(opt.Get())) : removed;
    }

    //Copy the srcKey field of src to the destKey field of dest and return the new dest
    public static Dynamic<T> CopyField(Dynamic<T> src, string srcKey, Dynamic<T> dest, string destKey)
    {
        var opt = src.Get(srcKey).Result();
        return opt.IsPresent ? dest.Set(destKey, opt.Get()) : dest;
    }

    //Apply fn to every key-value pair in the map and return a new Dynamic
    public Dynamic<T> UpdateMapValues(Func<Pair<Dynamic<T>, Dynamic<T>>, Pair<Dynamic<T>, Dynamic<T>>> fn)
    {
        var newEntries = AsMapOpt().Select(fn)
            .Select(p => new Pair<T, T>(p.First.Value, p.Second.Value));
        return new(Ops, Ops.CreateMap(newEntries));
    }

    //Returns an empty map on failure
    public Dynamic<T> OrElseEmptyMap()
        => Ops.GetMap(Value).Result().IsPresent ? this : EmptyMap();

    //Returns an empty list on failure
    public Dynamic<T> OrElseEmptyList()
        => Ops.GetStream(Value).Result().IsPresent ? this : EmptyList();

    //===factory methods===

    public Dynamic<T> CreateString(string value) => new(Ops, Ops.CreateString(value));

    public Dynamic<T> CreateInt(int value) => new(Ops, Ops.CreateInt(value));

    public Dynamic<T> CreateLong(long value) => new(Ops, Ops.CreateLong(value));

    public Dynamic<T> CreateByte(byte value) => new(Ops, Ops.CreateByte(value));

    public Dynamic<T> CreateShort(short value) => new(Ops, Ops.CreateShort(value));

    public Dynamic<T> CreateFloat(float value) => new(Ops, Ops.CreateFloat(value));

    public Dynamic<T> CreateDouble(double value) => new(Ops, Ops.CreateDouble(value));

    public Dynamic<T> CreateBoolean(bool value) => new(Ops, Ops.CreateBoolean(value));

    //Build a map Dynamic from a set of Pair Dynamics
    public Dynamic<T> CreateMap(IEnumerable<Pair<Dynamic<T>, Dynamic<T>>> map)
        => new(Ops, Ops.CreateMap(map.Select(p => new Pair<T, T>(p.First.Value, p.Second.Value))));

    //Build a list Dynamic from a set of Dynamics
    public Dynamic<T> CreateList(IEnumerable<Dynamic<T>> list)
        => new(Ops, Ops.CreateList(list.Select(d => d.Value)));
}
