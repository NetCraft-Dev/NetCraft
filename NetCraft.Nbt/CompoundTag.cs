using System.Collections;
using System.Text;
using NetCraft.Codec;
using NetCraft.Logging;

namespace NetCraft.Nbt;

//CompoundTag (TAG_Compound, ID=10). Mirrors vanilla net.minecraft.nbt.CompoundTag.
//Stores a set of key-value fields. Binary format:
//  [... fields: [1 byte type][2-byte name length][name][value]][1 byte TAG_End]
public sealed class CompoundTag : Tag, IEnumerable<KeyValuePair<string, Tag>>
{
    private readonly Dictionary<string, Tag> _tags = new();

    //Codec for CompoundTag, mirroring vanilla CompoundTag.CODEC
    //Under NbtOps only: EncodeStart returns the Tag as-is and Parse verifies it is a CompoundTag
    public static readonly Codec<CompoundTag> Codec = new CompoundTagCodec();

    //Internal constructor used by ShallowCopy
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

    //Merges the fields of other into this CompoundTag, mirroring vanilla merge
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

    // ============ typed getters (convenience access) ============

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

    // ============ typed put (convenience writes) ============

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

    // ============ scalar convenience getters (no cast needed) ============

    public byte GetByteValue(string key) => GetByte(key)?.Value ?? (byte)0;
    public short GetShortValue(string key) => GetShort(key)?.Value ?? (short)0;
    public int GetIntValue(string key) => GetInt(key)?.Value ?? 0;
    public long GetLongValue(string key) => GetLong(key)?.Value ?? 0L;
    public float GetFloatValue(string key) => GetFloat(key)?.Value ?? 0f;
    public double GetDoubleValue(string key) => GetDouble(key)?.Value ?? 0d;
    public string GetStringValue(string key) => GetString(key)?.Value ?? "";

    //Convenience getters with a default value, mirroring vanilla getIntOr/getLongOr and friends
    public byte GetByteOr(string key, byte defaultValue) => GetByte(key)?.Value ?? defaultValue;
    public int GetIntOr(string key, int defaultValue) => GetInt(key)?.Value ?? defaultValue;
    public long GetLongOr(string key, long defaultValue) => GetLong(key)?.Value ?? defaultValue;
    public bool GetBooleanOr(string key, bool defaultValue)
        => TryGetTag(key, out var tag) ? tag is ByteTag b && b.Value != 0 : defaultValue;

    //Empty container fallbacks, mirroring vanilla getCompoundOrEmpty/getListOrEmpty
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

    //Equality uses key-value set semantics, independent of key order and comparing values recursively. Mirrors vanilla CompoundTag.equals
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj)) return true;
        if (obj is not CompoundTag other || other._tags.Count != _tags.Count) return false;
        foreach (var (key, tag) in _tags)
            if (!other._tags.TryGetValue(key, out var otherTag) || !tag.Equals(otherTag)) return false;
        return true;
    }

    //Hash matches equality: per-entry XOR then sum, so it is independent of key order. Mirrors vanilla Map.hashCode
    public override int GetHashCode()
    {
        var hash = 0;
        foreach (var (key, tag) in _tags)
            hash += key.GetHashCode() ^ tag.GetHashCode();
        return hash;
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

    //Shallow copy sharing Tag references, used for prefix merging in NbtOps.MergeToMap
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
            // Two-phase visit: type first, then name (same as vanilla)
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

    //Serialize value into a Tag with Codec and store it under the name field
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

    //Encode value with MapCodec and merge it into this CompoundTag
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
                // Phase 1: visit the type (no name)
                var r1 = output.VisitEntry(type);
                if (r1 == StreamTagVisitor.EntryResult.Halt)
                    return StreamTagVisitor.ValueResult.Halt;
                if (r1 == StreamTagVisitor.EntryResult.Break)
                {
                    // Skip the current name+value, then jump to the end of the container
                    StringTag.SkipString(input);
                    type.Skip(input, accounter);
                    SkipToEndOfCompound(input, accounter);
                    return output.VisitContainerEnd();
                }
                if (r1 == StreamTagVisitor.EntryResult.Skip)
                {
                    // Skip the current name+value and read the next field
                    StringTag.SkipString(input);
                    type.Skip(input, accounter);
                    continue;
                }

                // Phase 2: read the name and visit it (named)
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

                // Phase 3: parse the value recursively
                accounter.AccountBytes(36);
                var r3 = type.Parse(input, output, accounter);
                if (r3 == StreamTagVisitor.ValueResult.Halt)
                    return StreamTagVisitor.ValueResult.Halt;
                if (r3 == StreamTagVisitor.ValueResult.Break)
                {
                    SkipToEndOfCompound(input, accounter);
                    return output.VisitContainerEnd();
                }
                // r3 == Continue: read the next field
            }
        }

        //Skip all remaining fields of this compound tag up to TAG_End. Used after BREAK/Halt to realign the read position.
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

//Codec implementation for CompoundTag, mirroring vanilla Codec.PASSTHROUGH.comapFlatMap
//EncodeStart passes the CompoundTag through as a Tag; valid under NbtOps only
//Parse verifies the Tag is a CompoundTag and errors otherwise
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

