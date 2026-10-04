using System.Collections;
using System.Text;
using NetCraft.Codec;
using NetCraft.Logging;

namespace NetCraft.Nbt;

//CompoundTag（TAG_Compound，ID=10）。对应原版 net.minecraft.nbt.CompoundTag。
//存储 key-value 字段集合。二进制格式：
//  [... 字段: [1 byte 类型][2字节 名字长度][名字][值]][1 byte TAG_End]
public sealed class CompoundTag : Tag, IEnumerable<KeyValuePair<string, Tag>>
{
    private readonly Dictionary<string, Tag> _tags = new();

    //CompoundTag的Codec对应原版CompoundTag.CODEC
    //仅在NbtOps下 EncodeStart返回原Tag Parse验证Tag是CompoundTag
    public static readonly Codec<CompoundTag> Codec = new CompoundTagCodec();

    //内构用于ShallowCopy浅拷贝
    internal CompoundTag(Dictionary<string, Tag> tags) { _tags = tags; }

    public CompoundTag() { }

    public byte Id => Tag.TagCompound;
    public TagType Type => CompoundTagType.Instance;

    public int Count => _tags.Count;
    public bool IsEmpty => _tags.Count == 0;

    public IEnumerable<string> Keys => _tags.Keys;
    public IEnumerable<Tag> Values => _tags.Values;

    public Tag? this[string key]
    {
        get => _tags.TryGetValue(key, out var t) ? t : null;
        set
        {
            if (value == null)
                _tags.Remove(key);
            else
                _tags[key] = value;
        }
    }

    public void Put(string key, Tag tag) => _tags[key] = tag;

    //合并other的字段到当前CompoundTag对应原版merge
    public void Merge(CompoundTag other)
    {
        foreach (var (key, tag) in other._tags)
            _tags[key] = tag;
    }

    public void Remove(string key) => _tags.Remove(key);

    public bool Contains(string key) => _tags.ContainsKey(key);

    public bool TryGetTag(string key, out Tag tag) => _tags.TryGetValue(key, out tag!);

    public T? Get<T>(string key) where T : class, Tag
        => _tags.TryGetValue(key, out var t) ? t as T : null;

    // ============ 类型化 getter（便捷访问） ============

    public ByteTag? GetByte(string key) => Get<ByteTag>(key);
    public ShortTag? GetShort(string key) => Get<ShortTag>(key);
    public IntTag? GetInt(string key) => Get<IntTag>(key);
    public LongTag? GetLong(string key) => Get<LongTag>(key);
    public FloatTag? GetFloat(string key) => Get<FloatTag>(key);
    public DoubleTag? GetDouble(string key) => Get<DoubleTag>(key);
    public StringTag? GetString(string key) => Get<StringTag>(key);
    public ByteArrayTag? GetByteArray(string key) => Get<ByteArrayTag>(key);
    public IntArrayTag? GetIntArray(string key) => Get<IntArrayTag>(key);
    public LongArrayTag? GetLongArray(string key) => Get<LongArrayTag>(key);
    public ListTag? GetList(string key) => Get<ListTag>(key);
    public CompoundTag? GetCompound(string key) => Get<CompoundTag>(key);

    // ============ 类型化 put（便捷写入） ============

    public void PutByte(string key, byte value) => Put(key, new ByteTag(value));
    public void PutShort(string key, short value) => Put(key, new ShortTag(value));
    public void PutInt(string key, int value) => Put(key, new IntTag(value));
    public void PutLong(string key, long value) => Put(key, new LongTag(value));
    public void PutFloat(string key, float value) => Put(key, new FloatTag(value));
    public void PutDouble(string key, double value) => Put(key, new DoubleTag(value));
    public void PutString(string key, string value) => Put(key, new StringTag(value));
    public void PutBoolean(string key, bool value) => PutByte(key, value ? (byte)1 : (byte)0);
    public void PutByteArray(string key, byte[] value) => Put(key, new ByteArrayTag(value));
    public void PutIntArray(string key, int[] value) => Put(key, new IntArrayTag(value));
    public void PutLongArray(string key, long[] value) => Put(key, new LongArrayTag(value));

    // ============ 标量便捷 getter（避免 cast） ============

    public byte GetByteValue(string key) => GetByte(key)?.Value ?? (byte)0;
    public short GetShortValue(string key) => GetShort(key)?.Value ?? (short)0;
    public int GetIntValue(string key) => GetInt(key)?.Value ?? 0;
    public long GetLongValue(string key) => GetLong(key)?.Value ?? 0L;
    public float GetFloatValue(string key) => GetFloat(key)?.Value ?? 0f;
    public double GetDoubleValue(string key) => GetDouble(key)?.Value ?? 0d;
    public string GetStringValue(string key) => GetString(key)?.Value ?? "";

    //带默认值的便捷getter对应原版getIntOr/getLongOr等
    public byte GetByteOr(string key, byte defaultValue) => GetByte(key)?.Value ?? defaultValue;
    public int GetIntOr(string key, int defaultValue) => GetInt(key)?.Value ?? defaultValue;
    public long GetLongOr(string key, long defaultValue) => GetLong(key)?.Value ?? defaultValue;
    public bool GetBooleanOr(string key, bool defaultValue)
        => TryGetTag(key, out var tag) ? tag is ByteTag b && b.Value != 0 : defaultValue;

    //空容器兜底对应原版getCompoundOrEmpty/getListOrEmpty
    public CompoundTag GetCompoundOrEmpty(string key) => GetCompound(key) ?? new CompoundTag();
    public ListTag GetListOrEmpty(string key) => GetList(key) ?? new ListTag();

    public void Write(INbtWriter output)
    {
        foreach (var (key, tag) in _tags)
        {
            output.WriteByte(tag.Id);
            output.WriteUtf(key);
            tag.Write(output);
        }
        output.WriteByte(Tag.TagEnd);
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append('{');
        var first = true;
        foreach (var (key, tag) in _tags)
        {
            if (!first) sb.Append(", ");
            first = false;
            sb.Append(key).Append(": ").Append(tag);
        }
        sb.Append('}');
        return sb.ToString();
    }

    public Tag Copy()
    {
        var copy = new CompoundTag();
        foreach (var (key, tag) in _tags)
            copy._tags[key] = tag.Copy();
        return copy;
    }

    //浅拷贝共享Tag引用用于NbtOps.MergeToMap的prefix处理
    public CompoundTag ShallowCopy() => new(new Dictionary<string, Tag>(_tags));

    public int SizeInBytes()
    {
        var sum = Tag.ObjectHeader;
        foreach (var (key, tag) in _tags)
        {
            sum += 1 + Tag.StringSize + key.Length * 2 + tag.SizeInBytes();
        }
        sum += 1; // TAG_End
        return sum;
    }

    public void Accept(TagVisitor visitor) => visitor.VisitCompound(this);

    public StreamTagVisitor.ValueResult Accept(StreamTagVisitor visitor)
    {
        foreach (var (key, tag) in _tags)
        {
            var type = tag.Type;
            // 两阶段访问：先访问类型，再访问名字（与原版一致）
            var r1 = visitor.VisitEntry(type);
            if (r1 == StreamTagVisitor.EntryResult.Halt)
                return StreamTagVisitor.ValueResult.Halt;
            if (r1 == StreamTagVisitor.EntryResult.Break)
                return visitor.VisitContainerEnd();
            if (r1 == StreamTagVisitor.EntryResult.Skip)
                continue;

            var r2 = visitor.VisitEntry(type, key);
            if (r2 == StreamTagVisitor.EntryResult.Halt)
                return StreamTagVisitor.ValueResult.Halt;
            if (r2 == StreamTagVisitor.EntryResult.Break)
                return visitor.VisitContainerEnd();
            if (r2 == StreamTagVisitor.EntryResult.Skip)
                continue;

            var r3 = tag.Accept(visitor);
            if (r3 == StreamTagVisitor.ValueResult.Halt)
                return StreamTagVisitor.ValueResult.Halt;
            if (r3 == StreamTagVisitor.ValueResult.Break)
                return visitor.VisitContainerEnd();
        }
        return visitor.VisitContainerEnd();
    }

    public new CompoundTag? AsCompound() => this;

    public IEnumerator<KeyValuePair<string, Tag>> GetEnumerator() => _tags.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    //用Codec把value序列化为Tag存入name字段
    public void Store<T>(string name, Codec<T> codec, T value)
        => Store(name, codec, NbtOps.Instance, value);

    public void StoreNullable<T>(string name, Codec<T> codec, T? value) where T : class
    {
        if (value is not null) Store(name, codec, NbtOps.Instance, value);
    }

    public void Store<T>(string name, Codec<T> codec, DynamicOps<Tag> ops, T value)
        => Put(name, codec.EncodeStart(ops, value).GetOrThrow());

    public void StoreNullable<T>(string name, Codec<T> codec, DynamicOps<Tag> ops, T? value) where T : class
    {
        if (value is not null) Store(name, codec, ops, value);
    }

    //用MapCodec编码value并合并到当前CompoundTag
    public void Store<T>(MapCodec<T> codec, DynamicOps<Tag> ops, T value)
        => Merge((CompoundTag)codec.EncodeStart(ops, value).GetOrThrow());

    public Optional<T> Read<T>(string name, Codec<T> codec)
        => Read(name, codec, NbtOps.Instance);

    public Optional<T> Read<T>(string name, Codec<T> codec, DynamicOps<Tag> ops)
    {
        var tag = this[name];
        if (tag is null) return Optional<T>.Empty();
        return codec.Parse(ops, tag).ResultOrPartial(err => Log.Warning($"Failed to read field ({name}={tag}): {err}"));
    }

    public Optional<T> Read<T>(MapCodec<T> codec)
        => Read(codec, NbtOps.Instance);

    public Optional<T> Read<T>(MapCodec<T> codec, DynamicOps<Tag> ops)
        => codec.Decode(ops, ops.GetMap(this).GetOrThrow()).ResultOrPartial(err => Log.Warning($"Failed to read value ({this}): {err}"));

    public sealed class CompoundTagType : TagType.VariableSize
    {
        public static readonly CompoundTagType Instance = new();

        public Tag Load(INbtReader input, NbtAccounter accounter)
        {
            var tag = new CompoundTag();
            byte type;
            while ((type = input.ReadByte()) != Tag.TagEnd)
            {
                var name = input.ReadUtf();
                accounter.AccountBytes(1 + Tag.StringSize + name.Length * 2L);
                var child = TagTypes.GetType(type).Load(input, accounter);
                tag.Put(name, child);
            }
            return tag;
        }

        public StreamTagVisitor.ValueResult Parse(INbtReader input, StreamTagVisitor output, NbtAccounter accounter)
        {
            accounter.AccountBytes(48);
            while (true)
            {
                var tagType = input.ReadByte();
                if (tagType == Tag.TagEnd)
                    return output.VisitContainerEnd();

                var type = TagTypes.GetType(tagType);
                // 第一阶段：访问类型（无名字）
                var r1 = output.VisitEntry(type);
                if (r1 == StreamTagVisitor.EntryResult.Halt)
                    return StreamTagVisitor.ValueResult.Halt;
                if (r1 == StreamTagVisitor.EntryResult.Break)
                {
                    // 跳过当前 name+value，再跳到容器末尾
                    StringTag.SkipString(input);
                    type.Skip(input, accounter);
                    SkipToEndOfCompound(input, accounter);
                    return output.VisitContainerEnd();
                }
                if (r1 == StreamTagVisitor.EntryResult.Skip)
                {
                    // 跳过当前 name+value，继续读下一个字段
                    StringTag.SkipString(input);
                    type.Skip(input, accounter);
                    continue;
                }

                // 第二阶段：读取名字并访问（有名字）
                var name = input.ReadUtf();
                accounter.AccountBytes(Tag.StringSize + name.Length * 2L);
                var r2 = output.VisitEntry(type, name);
                if (r2 == StreamTagVisitor.EntryResult.Halt)
                    return StreamTagVisitor.ValueResult.Halt;
                if (r2 == StreamTagVisitor.EntryResult.Break)
                {
                    type.Skip(input, accounter);
                    SkipToEndOfCompound(input, accounter);
                    return output.VisitContainerEnd();
                }
                if (r2 == StreamTagVisitor.EntryResult.Skip)
                {
                    type.Skip(input, accounter);
                    continue;
                }

                // 第三阶段：递归解析值
                accounter.AccountBytes(36);
                var r3 = type.Parse(input, output, accounter);
                if (r3 == StreamTagVisitor.ValueResult.Halt)
                    return StreamTagVisitor.ValueResult.Halt;
                if (r3 == StreamTagVisitor.ValueResult.Break)
                {
                    SkipToEndOfCompound(input, accounter);
                    return output.VisitContainerEnd();
                }
                // r3 == Continue: 继续读下一个字段
            }
        }

        //跳过当前复合标签剩余的所有字段直到 TAG_End。BREAK/Halt 后用于对齐读取位置。
        private static void SkipToEndOfCompound(INbtReader input, NbtAccounter accounter)
        {
            byte type;
            while ((type = input.ReadByte()) != Tag.TagEnd)
            {
                StringTag.SkipString(input);
                accounter.AccountBytes(1);
                TagTypes.GetType(type).Skip(input, accounter);
            }
        }

        public void Skip(INbtReader input, NbtAccounter accounter)
        {
            byte type;
            while ((type = input.ReadByte()) != Tag.TagEnd)
            {
                StringTag.SkipString(input);
                accounter.AccountBytes(1);
                TagTypes.GetType(type).Skip(input, accounter);
            }
        }

        public string Name => "TAG_Compound";
        public string PrettyName => "TAG_Compound";
    }
}

//CompoundTag的Codec实现对应原版Codec.PASSTHROUGH.comapFlatMap
//EncodeStart把CompoundTag作为Tag透传仅NbtOps下有效
//Parse验证Tag类型是CompoundTag否则错误
internal sealed class CompoundTagCodec : ScalarCodec<CompoundTag>
{
    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, CompoundTag value)
        => DataResult<U>.Success((U)(object)(Tag)value);

    public override DataResult<CompoundTag> Parse<U>(DynamicOps<U> ops, U input)
    {
        if (input is CompoundTag compoundTag)
            return DataResult<CompoundTag>.Success(compoundTag);
        return DataResult<CompoundTag>.Error(() => "Expected compound tag, got " + input);
    }
}

