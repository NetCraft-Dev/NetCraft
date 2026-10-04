namespace NetCraft.Codec;

//动态值包装对应原版com.mojang.serialization.Dynamic<T>
//持有一段ops下的值可通过Convert跨ops转换
//提供Get/AsString/AsStream/Update/Set/Create*等便捷操作
public sealed class Dynamic<T>
{
    public DynamicOps<T> Ops { get; }

    public T Value { get; }

    public Dynamic(DynamicOps<T> ops, T value)
    {
        Ops = ops;
        Value = value;
    }

    //把当前值用源ops.ConvertTo转到目标ops
    public Dynamic<U> Convert<U>(DynamicOps<U> ops)
        => new(ops, Ops.ConvertTo(ops, Value));

    public Dynamic<T> WithValue(T value) => new(Ops, value);

    //空map对应原版emptyMap
    public Dynamic<T> EmptyMap() => new(Ops, Ops.EmptyMap());

    //空list对应原版emptyList
    public Dynamic<T> EmptyList() => new(Ops, Ops.EmptyList());

    //取string key对应子Dynamic返回OptionalDynamic失败时携带错误信息
    public OptionalDynamic<T> Get(string key)
        => new(Ops, Ops.GetMap(Value).FlatMap(m =>
            m.Get(key).IsPresent
                ? DataResult<T>.Success(m.Get(key).Get())
                : DataResult<T>.Error(() => "Key not found: " + key)));

    //取T类型key对应子Dynamic
    public OptionalDynamic<T> Get(T key)
        => new(Ops, Ops.GetMap(Value).FlatMap(m =>
            m.Get(key).IsPresent
                ? DataResult<T>.Success(m.Get(key).Get())
                : DataResult<T>.Error(() => "Key not found")));

    //===取值转换DataResult===

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

    //===流式转换===

    //转Dynamic流失败返回错误DataResult
    public DataResult<IEnumerable<Dynamic<T>>> AsStream()
        => Ops.GetStream(Value).Map(s => s.Select(e => new Dynamic<T>(Ops, e)));

    //转Dynamic流失败部分返回空序列
    public IEnumerable<Dynamic<T>> AsStreamOpt()
        => AsStream().Result().OrElse(Enumerable.Empty<Dynamic<T>>());

    //转Pair Dynamic流失败返回错误DataResult
    public DataResult<IEnumerable<Pair<Dynamic<T>, Dynamic<T>>>> AsMap()
        => Ops.GetMapValues(Value).Map(s => s.Select(p =>
            new Pair<Dynamic<T>, Dynamic<T>>(new(Ops, p.First), new(Ops, p.Second))));

    //转Pair Dynamic流失败部分返回空序列
    public IEnumerable<Pair<Dynamic<T>, Dynamic<T>>> AsMapOpt()
        => AsMap().Result().OrElse(Enumerable.Empty<Pair<Dynamic<T>, Dynamic<T>>>());

    //===修改操作===

    //用fn更新指定string key对应子值返回新Dynamic
    public Dynamic<T> Update(string key, Func<Dynamic<T>, Dynamic<T>> fn)
        => Set(key, fn(Get(key).OrElse(EmptyMap())));

    //用fn更新指定T key对应子值
    public Dynamic<T> Update(T key, Func<Dynamic<T>, Dynamic<T>> fn)
        => Set(key, fn(Get(key).OrElse(EmptyMap())));

    //设置指定string key对应子值为value返回新Dynamic
    public Dynamic<T> Set(string key, Dynamic<T> value)
        => new(Ops, Ops.MergeToMap(Value, Ops.CreateString(key), value.Value).GetOrThrow(err => new InvalidOperationException(err)));

    //设置指定T key对应子值
    public Dynamic<T> Set(T key, Dynamic<T> value)
        => new(Ops, Ops.MergeToMap(Value, key, value.Value).GetOrThrow(err => new InvalidOperationException(err)));

    //存在时设置key对应值不存在保持原值
    public Dynamic<T> SetFieldIfPresent(string key, Optional<Dynamic<T>> value)
        => value.IsPresent ? Set(key, value.Get()) : this;

    //删除指定string key返回新Dynamic
    public Dynamic<T> Remove(string key) => new(Ops, Ops.Remove(Value, key));

    //重命名字段oldKey为newKey值保持不变
    public Dynamic<T> RenameField(string oldKey, string newKey)
    {
        var opt = Get(oldKey).Result();
        var removed = Remove(oldKey);
        return opt.IsPresent ? removed.Set(newKey, opt.Get()) : removed;
    }

    //重命名字段并应用fn修改对应值
    public Dynamic<T> RenameAndFixField(string oldKey, string newKey, Func<Dynamic<T>, Dynamic<T>> fn)
    {
        var opt = Get(oldKey).Result();
        var removed = Remove(oldKey);
        return opt.IsPresent ? removed.Set(newKey, fn(opt.Get())) : removed;
    }

    //把src的srcKey字段复制到dest的destKey字段返回新dest
    public static Dynamic<T> CopyField(Dynamic<T> src, string srcKey, Dynamic<T> dest, string destKey)
    {
        var opt = src.Get(srcKey).Result();
        return opt.IsPresent ? dest.Set(destKey, opt.Get()) : dest;
    }

    //用fn修改map中所有键值对返回新Dynamic
    public Dynamic<T> UpdateMapValues(Func<Pair<Dynamic<T>, Dynamic<T>>, Pair<Dynamic<T>, Dynamic<T>>> fn)
    {
        var newEntries = AsMapOpt().Select(fn)
            .Select(p => new Pair<T, T>(p.First.Value, p.Second.Value));
        return new(Ops, Ops.CreateMap(newEntries));
    }

    //失败时返回空map
    public Dynamic<T> OrElseEmptyMap()
        => Ops.GetMap(Value).Result().IsPresent ? this : EmptyMap();

    //失败时返回空list
    public Dynamic<T> OrElseEmptyList()
        => Ops.GetStream(Value).Result().IsPresent ? this : EmptyList();

    //===工厂方法===

    public Dynamic<T> CreateString(string value) => new(Ops, Ops.CreateString(value));

    public Dynamic<T> CreateInt(int value) => new(Ops, Ops.CreateInt(value));

    public Dynamic<T> CreateLong(long value) => new(Ops, Ops.CreateLong(value));

    public Dynamic<T> CreateByte(byte value) => new(Ops, Ops.CreateByte(value));

    public Dynamic<T> CreateShort(short value) => new(Ops, Ops.CreateShort(value));

    public Dynamic<T> CreateFloat(float value) => new(Ops, Ops.CreateFloat(value));

    public Dynamic<T> CreateDouble(double value) => new(Ops, Ops.CreateDouble(value));

    public Dynamic<T> CreateBoolean(bool value) => new(Ops, Ops.CreateBoolean(value));

    //用一组Pair Dynamic构造map类型Dynamic
    public Dynamic<T> CreateMap(IEnumerable<Pair<Dynamic<T>, Dynamic<T>>> map)
        => new(Ops, Ops.CreateMap(map.Select(p => new Pair<T, T>(p.First.Value, p.Second.Value))));

    //用一组Dynamic构造list类型Dynamic
    public Dynamic<T> CreateList(IEnumerable<Dynamic<T>> list)
        => new(Ops, Ops.CreateList(list.Select(d => d.Value)));
}
