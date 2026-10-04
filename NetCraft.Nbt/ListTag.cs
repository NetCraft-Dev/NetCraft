using System.Collections;
using System.Text;

namespace NetCraft.Nbt;

//ListTag（TAG_List，ID=9）。对应原版 net.minecraft.nbt.ListTag。
//存储同类型 Tag 列表。二进制格式：
//  [1 byte: 元素类型 ID][4 bytes: 长度][... 元素数据（无类型前缀，无名字）]
//内部允许异构元素对应原版ListTag直接存List<Tag>
//写出时动态判定类型混合时统一包装为单字段"" CompoundTag读入时AddAndUnwrap解包
public sealed class ListTag : Tag, IEnumerable<Tag>
{
    private List<Tag> _list = new();

    public byte Id => Tag.TagList;
    public TagType Type => ListTagType.Instance;

    public int Count => _list.Count;
    public byte ElementType => IdentifyRawElementType();
    public bool IsEmpty => _list.Count == 0;

    public Tag this[int index]
    {
        get => _list[index];
        set => _list[index] = value;
    }

    public ListTag() { }

    public ListTag(IEnumerable<Tag> tags)
    {
        foreach (var tag in tags)
            Add(tag);
    }

    public void Add(Tag tag) => _list.Add(tag);

    //AddAndUnwrap添加元素并尝试解包单字段"" CompoundTag
    //对应原版ListTag.addAndUnwrap tag是CompoundTag时tryUnwrap取内部值
    public void AddAndUnwrap(Tag tag)
    {
        if (tag is CompoundTag compound)
        {
            Add(TryUnwrap(compound));
        }
        else
        {
            Add(tag);
        }
    }

    //TryUnwrap只含单字段""时返回内部值否则原样返回
    private static Tag TryUnwrap(CompoundTag tag)
    {
        if (tag.Count == 1 && tag.TryGetTag("", out var inner))
        {
            return inner;
        }
        return tag;
    }

    public void Clear()
    {
        _list.Clear();
    }

    //RemoveLast 移除末尾元素对应原版 ListTag.removeLast
    public void RemoveLast()
    {
        if (_list.Count > 0) _list.RemoveAt(_list.Count - 1);
    }

    //TryInsert 在指定位置插入元素 越界不动作返回 false 对应原版 ListTag.addTag
    //NbtPath 的 insert 要按位置插入且以越界返回 false 表示未改动
    public bool TryInsert(int index, Tag tag)
    {
        if (index < 0 || index > _list.Count) return false;
        _list.Insert(index, tag);
        return true;
    }

    //TrySet 替换指定位置元素 越界不动作返回 false 对应原版 ListTag.setTag
    public bool TrySet(int index, Tag tag)
    {
        if (index < 0 || index >= _list.Count) return false;
        _list[index] = tag;
        return true;
    }

    //TryRemoveAt 移除指定位置元素 越界不动作返回 false 对应原版 ListTag.remove
    public bool TryRemoveAt(int index)
    {
        if (index < 0 || index >= _list.Count) return false;
        _list.RemoveAt(index);
        return true;
    }

    //写出时动态判定元素类型混合列表统一包装为单字段"" CompoundTag
    //对应原版ListTag.write的identifyRawElementType+wrapIfNeeded
    public void Write(INbtWriter output)
    {
        var elementType = IdentifyRawElementType();
        output.WriteByte(elementType);
        output.WriteInt(_list.Count);
        foreach (var tag in _list)
            WrapIfNeeded(elementType, tag).Write(output);
    }

    //全同类型返回该类型混合返回CompoundTag(10)空列表返回EndTag(0)
    private byte IdentifyRawElementType()
    {
        var homogeneousType = Tag.TagEnd;
        foreach (var element in _list)
        {
            var elementType = element.Id;
            if (homogeneousType == Tag.TagEnd)
                homogeneousType = elementType;
            else if (homogeneousType != elementType)
                return Tag.TagCompound;
        }
        return homogeneousType;
    }

    //元素类型与目标不一致时包装为{"":element}已是包装结构的不重复包
    private static Tag WrapIfNeeded(byte elementType, Tag tag)
    {
        if (elementType != Tag.TagCompound)
            return tag;
        if (tag is CompoundTag compound && !IsWrapper(compound))
            return compound;
        var wrapped = new CompoundTag();
        wrapped.Put("", tag);
        return wrapped;
    }

    //单字段""视为包装结构
    private static bool IsWrapper(CompoundTag tag)
        => tag.Count == 1 && tag.Contains("");

    //判等按元素顺序逐个比较 对应原版 ListTag.equals
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj)) return true;
        if (obj is not ListTag other || other._list.Count != _list.Count) return false;
        for (var i = 0; i < _list.Count; i++)
            if (!_list[i].Equals(other._list[i])) return false;
        return true;
    }

    //哈希与判等一致 按顺序聚合元素哈希 对应原版 List.hashCode
    public override int GetHashCode()
    {
        var hash = 1;
        foreach (var tag in _list) hash = hash * 31 + tag.GetHashCode();
        return hash;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append('[');
        for (var i = 0; i < _list.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(_list[i]);
        }
        sb.Append(']');
        return sb.ToString();
    }

    public Tag Copy()
    {
        var copy = new ListTag();
        foreach (var tag in _list)
            copy.Add(tag.Copy());
        return copy;
    }

    public int SizeInBytes() => Tag.ArrayHeader + _list.Sum(t => t.SizeInBytes());

    public void Accept(TagVisitor visitor) => visitor.VisitList(this);

    public StreamTagVisitor.ValueResult Accept(StreamTagVisitor visitor)
    {
        var listResult = visitor.VisitList(TagTypes.GetType(ElementType), _list.Count);
        if (listResult == StreamTagVisitor.ValueResult.Halt)
            return StreamTagVisitor.ValueResult.Halt;
        if (listResult == StreamTagVisitor.ValueResult.Break)
            return visitor.VisitContainerEnd();

        for (var i = 0; i < _list.Count; i++)
        {
            var tag = _list[i];
            var elementResult = visitor.VisitElement(tag.Type, i);
            if (elementResult == StreamTagVisitor.EntryResult.Halt)
                return StreamTagVisitor.ValueResult.Halt;
            if (elementResult == StreamTagVisitor.EntryResult.Break)
                return visitor.VisitContainerEnd();
            if (elementResult == StreamTagVisitor.EntryResult.Skip)
                continue;
            // Enter
            var valueResult = tag.Accept(visitor);
            if (valueResult == StreamTagVisitor.ValueResult.Halt)
                return StreamTagVisitor.ValueResult.Halt;
            if (valueResult == StreamTagVisitor.ValueResult.Break)
                return visitor.VisitContainerEnd();
        }
        return visitor.VisitContainerEnd();
    }

    public new ListTag? AsList() => this;

    //按索引枚举元素。对应原版 ListTag 继承 java.util.AbstractList 的迭代能力。
    public IEnumerator<Tag> GetEnumerator() => _list.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ============ 类型化访问辅助 ============

    public ByteTag? GetByte(int i) => _list[i] as ByteTag;
    public ShortTag? GetShort(int i) => _list[i] as ShortTag;
    public IntTag? GetInt(int i) => _list[i] as IntTag;
    public LongTag? GetLong(int i) => _list[i] as LongTag;
    public FloatTag? GetFloat(int i) => _list[i] as FloatTag;
    public DoubleTag? GetDouble(int i) => _list[i] as DoubleTag;
    public StringTag? GetString(int i) => _list[i] as StringTag;
    public CompoundTag? GetCompound(int i) => _list[i] as CompoundTag;
    public ListTag? GetList(int i) => _list[i] as ListTag;
    public ByteArrayTag? GetByteArray(int i) => _list[i] as ByteArrayTag;
    public IntArrayTag? GetIntArray(int i) => _list[i] as IntArrayTag;
    public LongArrayTag? GetLongArray(int i) => _list[i] as LongArrayTag;

    public sealed class ListTagType : TagType.VariableSize
    {
        public static readonly ListTagType Instance = new();

        public Tag Load(INbtReader input, NbtAccounter accounter)
        {
            accounter.PushDepth();
            try
            {
                accounter.AccountBytes(Tag.ArrayHeader);
                var elementType = input.ReadByte();
                var length = input.ReadInt();
                if (elementType == Tag.TagEnd && length > 0)
                    throw new NbtFormatException("Missing type on ListTag");
                if (length < 0)
                    throw new NbtFormatException("ListTag length cannot be negative: " + length);

                accounter.AccountBytes(4L * length);
                var list = new ListTag();
                var type = TagTypes.GetType(elementType);
                for (var i = 0; i < length; i++)
                {
                    list.AddAndUnwrap(type.Load(input, accounter));
                }
                return list;
            }
            finally
            {
                accounter.PopDepth();
            }
        }

        public StreamTagVisitor.ValueResult Parse(INbtReader input, StreamTagVisitor output, NbtAccounter accounter)
        {
            accounter.PushDepth();
            try
            {
                accounter.AccountBytes(Tag.ArrayHeader);
                var elementType = input.ReadByte();
                var length = input.ReadInt();
                if (length < 0)
                    throw new NbtFormatException("ListTag length cannot be negative: " + length);
                var type = TagTypes.GetType(elementType);

                var listResult = output.VisitList(type, length);
                if (listResult == StreamTagVisitor.ValueResult.Halt)
                    return StreamTagVisitor.ValueResult.Halt;
                if (listResult == StreamTagVisitor.ValueResult.Break)
                {
                    type.Skip(input, length, accounter);
                    return output.VisitContainerEnd();
                }
                // Continue: 逐元素访问
                accounter.AccountBytes(4L * length);

                var i = 0;
                var exit = false;
                while (i < length)
                {
                    var elementResult = output.VisitElement(type, i);
                    if (elementResult == StreamTagVisitor.EntryResult.Halt)
                        return StreamTagVisitor.ValueResult.Halt;
                    if (elementResult == StreamTagVisitor.EntryResult.Break)
                    {
                        type.Skip(input, accounter);
                        exit = true;
                    }
                    else if (elementResult == StreamTagVisitor.EntryResult.Skip)
                    {
                        type.Skip(input, accounter);
                        i++;
                    }
                    else
                    {
                        // Enter
                        var valueResult = type.Parse(input, output, accounter);
                        if (valueResult == StreamTagVisitor.ValueResult.Halt)
                            return StreamTagVisitor.ValueResult.Halt;
                        if (valueResult == StreamTagVisitor.ValueResult.Break)
                            exit = true;
                        else
                            i++;
                    }
                    if (exit) break;
                }
                // 跳过剩余未访问的元素（对齐读取位置）
                var amountToSkip = (length - 1) - i;
                if (amountToSkip > 0)
                    type.Skip(input, amountToSkip, accounter);
                return output.VisitContainerEnd();
            }
            finally
            {
                accounter.PopDepth();
            }
        }

        public void Skip(INbtReader input, NbtAccounter accounter)
        {
            var elementType = input.ReadByte();
            var length = input.ReadInt();
            accounter.AccountBytes(Tag.ArrayHeader);
            var type = TagTypes.GetType(elementType);
            for (var i = 0; i < length; i++)
                type.Skip(input, accounter);
        }

        public string Name => "TAG_List";
        public string PrettyName => "TAG_List";
    }
}

