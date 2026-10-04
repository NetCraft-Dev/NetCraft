using NetCraft.Codec;

namespace NetCraft.Nbt;

//NBT的DynamicOps实现对应原版net.minecraft.nbt.NbtOps
//把Tag作为序列化载体实现Codec框架的所有类型操作
public sealed class NbtOps : DynamicOps<Tag>
{
    public static readonly NbtOps Instance = new();

    private NbtOps() { }

    public Tag Empty() => EndTag.Instance;

    public Tag EmptyList() => new ListTag();

    public Tag EmptyMap() => new CompoundTag();

    public Tag CreateByte(byte value) => ByteTag.ValueOf(value);

    public Tag CreateShort(short value) => ShortTag.ValueOf(value);

    public Tag CreateInt(int value) => IntTag.ValueOf(value);

    public Tag CreateLong(long value) => LongTag.ValueOf(value);

    public Tag CreateFloat(float value) => FloatTag.ValueOf(value);

    public Tag CreateDouble(double value) => DoubleTag.ValueOf(value);

    public Tag CreateBoolean(bool value) => ByteTag.ValueOf(value);

    public Tag CreateNumeric(double value) => DoubleTag.ValueOf(value);

    public Tag CreateString(string value) => StringTag.ValueOf(value);

    public Tag CreateList(IEnumerable<Tag> stream) => new ListTag(stream);

    //CreateByteList重写返回ByteArrayTag对齐原版NbtOps
    public Tag CreateByteList(IEnumerable<byte> stream) => new ByteArrayTag(stream.ToArray());

    //CreateIntList重写返回IntArrayTag对齐原版NbtOps
    public Tag CreateIntList(IEnumerable<int> stream) => new IntArrayTag(stream.ToArray());

    //CreateLongList重写返回LongArrayTag对齐原版NbtOps
    public Tag CreateLongList(IEnumerable<long> stream) => new LongArrayTag(stream.ToArray());

    //创建CompoundTag要求key必须是StringTag否则抛
    public Tag CreateMap(IEnumerable<Pair<Tag, Tag>> map)
    {
        var tag = new CompoundTag();
        foreach (var entry in map)
        {
            if (entry.First is not StringTag stringKey)
                throw new NotSupportedException($"Cannot create map with non-string key: {entry.First}");
            tag.Put(stringKey.Value, entry.Second);
        }
        return tag;
    }

    //取数值统一转double原版返回Number最小集用double
    public DataResult<double> GetNumberValue(Tag input)
        => input.AsNumber() is { } number
            ? DataResult<double>.Success(number.DoubleValue())
            : DataResult<double>.Error(() => $"Not a number: {input}");

    //长整数走精确路径Number内部是double直接转换会舍入掉超过53位的位模式
    public DataResult<long> GetLongValue(Tag input)
        => input is LongTag longTag
            ? DataResult<long>.Success(longTag.Value)
            : GetNumberValue(input).Map(v => (long)v);

    public DataResult<string> GetStringValue(Tag input)
        => input.AsString() is { } value
            ? DataResult<string>.Success(value)
            : DataResult<string>.Error(() => $"Not a string: {input}");

    public DataResult<bool> GetBooleanValue(Tag input)
        => GetNumberValue(input).Map(v => v != 0.0);

    //合并单个value到list失败返回DataResult.Error对齐原版try-catch
    public DataResult<Tag> MergeToList(Tag list, Tag value)
    {
        try
        {
            var collector = CreateCollector(list);
            if (collector is null)
                return DataResult<Tag>.Error(() => $"mergeToList called with not a list: {list}", list);
            return DataResult<Tag>.Success(collector.Accept(value).Result());
        }
        catch (Exception ex)
        {
            return DataResult<Tag>.Error(() => $"Failed to append to list: {ex.Message}", list);
        }
    }

    public DataResult<Tag> MergeToList(Tag list, IReadOnlyList<Tag> values)
    {
        try
        {
            var collector = CreateCollector(list);
            if (collector is null)
                return DataResult<Tag>.Error(() => $"mergeToList called with not a list: {list}", list);
            var c = collector;
            foreach (var v in values) c = c.Accept(v);
            return DataResult<Tag>.Success(c.Result());
        }
        catch (Exception ex)
        {
            return DataResult<Tag>.Error(() => $"Failed to append to list: {ex.Message}", list);
        }
    }

    //合并key-value到map要求key是StringTag
    public DataResult<Tag> MergeToMap(Tag map, Tag key, Tag value)
    {
        if (map is not CompoundTag and not EndTag)
            return DataResult<Tag>.Error(() => $"mergeToMap called with not a map: {map}", map);
        if (key is not StringTag stringKey)
            return DataResult<Tag>.Error(() => $"key is not a string: {key}", map);

        var output = map is CompoundTag compound ? compound.ShallowCopy() : new CompoundTag();
        output.Put(stringKey.Value, value);
        return DataResult<Tag>.Success(output);
    }

    public DataResult<Tag> MergeToMap(Tag map, MapLike<Tag> values)
    {
        if (map is not CompoundTag and not EndTag)
            return DataResult<Tag>.Error(() => $"mergeToMap called with not a map: {map}", map);

        var output = map is CompoundTag compound ? compound.ShallowCopy() : new CompoundTag();
        var missed = new List<Tag>();
        foreach (var (key, value) in values.Entries())
        {
            if (key is not StringTag stringKey)
            {
                missed.Add(key);
                continue;
            }
            output.Put(stringKey.Value, value);
        }
        if (missed.Count > 0)
            return DataResult<Tag>.Error(() => $"some keys are not strings: {string.Join(", ", missed)}", output);
        return DataResult<Tag>.Success(output);
    }

    public DataResult<Tag> MergeToMap(Tag map, IReadOnlyDictionary<Tag, Tag> values)
    {
        if (map is not CompoundTag and not EndTag)
            return DataResult<Tag>.Error(() => $"mergeToMap called with not a map: {map}", map);

        var output = map is CompoundTag compound ? compound.ShallowCopy() : new CompoundTag();
        var missed = new List<Tag>();
        foreach (var (key, value) in values)
        {
            if (key is not StringTag stringKey)
            {
                missed.Add(key);
                continue;
            }
            output.Put(stringKey.Value, value);
        }
        if (missed.Count > 0)
            return DataResult<Tag>.Error(() => $"some keys are not strings: {string.Join(", ", missed)}", output);
        return DataResult<Tag>.Success(output);
    }

    //CompoundTag转MapLike其他类型返回错误
    public DataResult<MapLike<Tag>> GetMap(Tag input)
    {
        if (input is CompoundTag compound)
            return DataResult<MapLike<Tag>>.Success(new CompoundMapLike(compound));
        return DataResult<MapLike<Tag>>.Error(() => $"Not a map: {input}");
    }

    public DataResult<IEnumerable<Pair<Tag, Tag>>> GetMapValues(Tag input)
    {
        if (input is CompoundTag compound)
        {
            var entries = compound.Select(e => new Pair<Tag, Tag>(CreateString(e.Key), e.Value)).ToList();
            return DataResult<IEnumerable<Pair<Tag, Tag>>>.Success(entries);
        }
        return DataResult<IEnumerable<Pair<Tag, Tag>>>.Error(() => $"Not a map: {input}");
    }

    //ListTag/ByteArray/IntArray/LongArray作为stream返回
    public DataResult<IEnumerable<Tag>> GetStream(Tag input)
    {
        return input switch
        {
            ListTag list => DataResult<IEnumerable<Tag>>.Success(list),
            ByteArrayTag bytes => DataResult<IEnumerable<Tag>>.Success(bytes.Value.Select(b => (Tag)ByteTag.ValueOf(b))),
            IntArrayTag ints => DataResult<IEnumerable<Tag>>.Success(ints.Value.Select(i => (Tag)IntTag.ValueOf(i))),
            LongArrayTag longs => DataResult<IEnumerable<Tag>>.Success(longs.Value.Select(l => (Tag)LongTag.ValueOf(l))),
            _ => DataResult<IEnumerable<Tag>>.Error(() => $"Not a list: {input}")
        };
    }

    //删除key返回浅拷贝CompoundTag其他类型原样返回
    public Tag Remove(Tag input, string key)
    {
        if (input is CompoundTag compound)
        {
            var result = compound.ShallowCopy();
            result.Remove(key);
            return result;
        }
        return input;
    }

    //把当前Tag转换到目标ops最小集标量按值传递复合递归
    public U ConvertTo<U>(DynamicOps<U> ops, Tag tag)
    {
        return tag switch
        {
            EndTag => ops.Empty(),
            ByteTag b => ops.CreateByte(b.Value),
            ShortTag s => ops.CreateShort(s.Value),
            IntTag i => ops.CreateInt(i.Value),
            LongTag l => ops.CreateLong(l.Value),
            FloatTag f => ops.CreateFloat(f.Value),
            DoubleTag d => ops.CreateDouble(d.Value),
            ByteArrayTag bytes => ops.CreateList(bytes.Value.Select(b => ops.CreateByte(b))),
            StringTag s => ops.CreateString(s.Value),
            ListTag list => ConvertList(ops, list),
            CompoundTag compound => ConvertMap(ops, compound),
            IntArrayTag ints => ops.CreateList(ints.Value.Select(i => ops.CreateInt(i))),
            LongArrayTag longs => ops.CreateList(longs.Value.Select(l => ops.CreateLong(l))),
            _ => throw new InvalidOperationException($"Unknown tag type: {tag}")
        };
    }

    private U ConvertList<U>(DynamicOps<U> ops, ListTag list)
        => ops.CreateList(list.Select(t => ConvertTo(ops, t)));

    private U ConvertMap<U>(DynamicOps<U> ops, CompoundTag compound)
        => ops.CreateMap(compound.Select(e => new Pair<U, U>(ops.CreateString(e.Key), ConvertTo(ops, e.Value))));

    //创建list collector用于mergeToList
    //按初始tag类型选紧凑数组collector全Byte/Int/Long用对应紧凑数组其他用ListTag
    private ListCollector? CreateCollector(Tag tag)
    {
        if (tag is EndTag) return new GenericListCollector();
        if (tag is ListTag list)
        {
            if (list.IsEmpty) return new GenericListCollector();
            var elementType = list.ElementType;
            if (elementType == Tag.TagByte) return new ByteCollector(list.Cast<ByteTag>().Select(t => t.Value));
            if (elementType == Tag.TagInt) return new IntCollector(list.Cast<IntTag>().Select(t => t.Value));
            if (elementType == Tag.TagLong) return new LongCollector(list.Cast<LongTag>().Select(t => t.Value));
            return new GenericListCollector(list);
        }
        if (tag is ByteArrayTag bytes) return new ByteCollector(bytes.Value);
        if (tag is IntArrayTag ints) return new IntCollector(ints.Value);
        if (tag is LongArrayTag longs) return new LongCollector(longs.Value);
        return null;
    }

    //返回record builder用于MapCodec.Encoder
    public RecordBuilder<Tag> MapBuilder() => new NbtRecordBuilder(this);
    private sealed class CompoundMapLike : MapLike<Tag>
    {
        private readonly CompoundTag _tag;

        public CompoundMapLike(CompoundTag tag) { _tag = tag; }

        public Optional<Tag> Get(Tag key)
        {
            if (key is not StringTag stringKey)
                throw new NotSupportedException($"Cannot get map entry with non-string key: {key}");
            return Optional<Tag>.OfNullable(_tag[stringKey.Value]);
        }

        public Optional<Tag> Get(string key)
            => Optional<Tag>.OfNullable(_tag[key]);

        public IEnumerable<Pair<Tag, Tag>> Entries()
            => _tag.Select(e => new Pair<Tag, Tag>(StringTag.ValueOf(e.Key), e.Value));
    }

    //list collector接口对应原版ListCollector
    private interface ListCollector
    {
        ListCollector Accept(Tag tag);

        Tag Result();
    }

    //通用ListTag collector不优化紧凑数组遇到任意类型都加入ListTag
    private sealed class GenericListCollector : ListCollector
    {
        private readonly ListTag _result = new();

        public GenericListCollector() { }

        public GenericListCollector(IEnumerable<Tag> initial)
        {
            foreach (var tag in initial) _result.Add(tag);
        }

        public ListCollector Accept(Tag tag)
        {
            _result.Add(tag);
            return this;
        }

        public Tag Result() => _result;
    }

    //ByteCollector累积ByteTag到byte[]结果ByteArrayTag遇非ByteTag降级GenericListCollector
    private sealed class ByteCollector : ListCollector
    {
        private readonly List<byte> _bytes = new();

        public ByteCollector(IEnumerable<byte> initial)
        {
            foreach (var b in initial) _bytes.Add(b);
        }

        public ListCollector Accept(Tag tag)
        {
            if (tag is ByteTag b)
            {
                _bytes.Add(b.Value);
                return this;
            }
            //降级为GenericListCollector把已累积byte转回ByteTag再追加新tag
            var generic = new GenericListCollector(_bytes.Select(bv => (Tag)ByteTag.ValueOf(bv)));
            return generic.Accept(tag);
        }

        public Tag Result() => new ByteArrayTag(_bytes.ToArray());
    }

    //IntCollector累积IntTag到int[]结果IntArrayTag遇非IntTag降级GenericListCollector
    private sealed class IntCollector : ListCollector
    {
        private readonly List<int> _ints = new();

        public IntCollector(IEnumerable<int> initial)
        {
            foreach (var i in initial) _ints.Add(i);
        }

        public ListCollector Accept(Tag tag)
        {
            if (tag is IntTag i)
            {
                _ints.Add(i.Value);
                return this;
            }
            var generic = new GenericListCollector(_ints.Select(iv => (Tag)IntTag.ValueOf(iv)));
            return generic.Accept(tag);
        }

        public Tag Result() => new IntArrayTag(_ints.ToArray());
    }

    //LongCollector累积LongTag到long[]结果LongArrayTag遇非LongTag降级GenericListCollector
    private sealed class LongCollector : ListCollector
    {
        private readonly List<long> _longs = new();

        public LongCollector(IEnumerable<long> initial)
        {
            foreach (var l in initial) _longs.Add(l);
        }

        public ListCollector Accept(Tag tag)
        {
            if (tag is LongTag l)
            {
                _longs.Add(l.Value);
                return this;
            }
            var generic = new GenericListCollector(_longs.Select(lv => (Tag)LongTag.ValueOf(lv)));
            return generic.Accept(tag);
        }

        public Tag Result() => new LongArrayTag(_longs.ToArray());
    }
}

//NBT的RecordBuilder实现对应原版NbtRecordBuilder
//累积字段到CompoundTag并支持prefix合并
public sealed class NbtRecordBuilder : AbstractRecordBuilder<Tag>
{
    public NbtRecordBuilder(NbtOps ops) : base(ops) { }

    protected override Tag InitBuilder() => new CompoundTag();

    protected override Tag Append(string key, Tag value, Tag builder)
    {
        if (builder is CompoundTag compound)
            compound.Put(key, value);
        return builder;
    }

    protected override DataResult<Tag> Build(IReadOnlyList<KeyValuePair<string, Tag>> entries, Tag builder, Tag prefix)
    {
        if (prefix is EndTag or null)
            return DataResult<Tag>.Success(builder);
        if (prefix is CompoundTag existing && builder is CompoundTag built)
        {
            var result = existing.ShallowCopy();
            foreach (var (key, value) in built) result.Put(key, value);
            return DataResult<Tag>.Success(result);
        }
        return DataResult<Tag>.Error(() => $"mergeToMap called with not a map: {prefix}", prefix);
    }
}
