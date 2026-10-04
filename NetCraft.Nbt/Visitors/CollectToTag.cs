using NetCraft.Nbt;

namespace NetCraft.Nbt.Visitors;

//流式访问者，将 NBT 流构建为完整 Tag 树。对应原版 net.minecraft.nbt.visitors.CollectToTag。
//子类 SkipFields / CollectFields 在其基础上增加字段过滤逻辑。
public class CollectToTag : StreamTagVisitor
{
    private readonly Stack<ContainerBuilder> _containerStack = new();

    public CollectToTag()
    {
        _containerStack.Push(new RootBuilder());
    }

    //获取构建结果（最外层 Tag）。
    public Tag GetResult() => _containerStack.First().Build()!;

    //当前容器嵌套深度（栈大小 - 1）。
    protected int Depth => _containerStack.Count - 1;

    private void AppendEntry(Tag instance)
    {
        _containerStack.Peek().AcceptValue(instance);
    }

    // ============ 标量值 ============

    public virtual StreamTagVisitor.ValueResult VisitEnd()
    {
        AppendEntry(EndTag.Instance);
        return StreamTagVisitor.ValueResult.Continue;
    }

    public virtual StreamTagVisitor.ValueResult VisitString(string value)
    {
        AppendEntry(StringTag.ValueOf(value));
        return StreamTagVisitor.ValueResult.Continue;
    }

    public virtual StreamTagVisitor.ValueResult VisitByte(byte value)
    {
        AppendEntry(ByteTag.ValueOf(value));
        return StreamTagVisitor.ValueResult.Continue;
    }

    public virtual StreamTagVisitor.ValueResult VisitShort(short value)
    {
        AppendEntry(ShortTag.ValueOf(value));
        return StreamTagVisitor.ValueResult.Continue;
    }

    public virtual StreamTagVisitor.ValueResult VisitInt(int value)
    {
        AppendEntry(IntTag.ValueOf(value));
        return StreamTagVisitor.ValueResult.Continue;
    }

    public virtual StreamTagVisitor.ValueResult VisitLong(long value)
    {
        AppendEntry(LongTag.ValueOf(value));
        return StreamTagVisitor.ValueResult.Continue;
    }

    public virtual StreamTagVisitor.ValueResult VisitFloat(float value)
    {
        AppendEntry(FloatTag.ValueOf(value));
        return StreamTagVisitor.ValueResult.Continue;
    }

    public virtual StreamTagVisitor.ValueResult VisitDouble(double value)
    {
        AppendEntry(DoubleTag.ValueOf(value));
        return StreamTagVisitor.ValueResult.Continue;
    }

    public virtual StreamTagVisitor.ValueResult VisitByteArray(ReadOnlySpan<byte> value)
    {
        AppendEntry(new ByteArrayTag(value.ToArray()));
        return StreamTagVisitor.ValueResult.Continue;
    }

    public virtual StreamTagVisitor.ValueResult VisitIntArray(ReadOnlySpan<int> value)
    {
        AppendEntry(new IntArrayTag(value.ToArray()));
        return StreamTagVisitor.ValueResult.Continue;
    }

    public virtual StreamTagVisitor.ValueResult VisitLongArray(ReadOnlySpan<long> value)
    {
        AppendEntry(new LongArrayTag(value.ToArray()));
        return StreamTagVisitor.ValueResult.Continue;
    }

    // ============ 容器 ============

    public virtual StreamTagVisitor.ValueResult VisitList(TagType elementType, int size)
        => StreamTagVisitor.ValueResult.Continue;

    public virtual StreamTagVisitor.EntryResult VisitElement(TagType type, int index)
    {
        EnterContainerIfNeeded(type);
        return StreamTagVisitor.EntryResult.Enter;
    }

    public virtual StreamTagVisitor.EntryResult VisitEntry(TagType type)
        => StreamTagVisitor.EntryResult.Enter;

    public virtual StreamTagVisitor.EntryResult VisitEntry(TagType type, string id)
    {
        _containerStack.Peek().AcceptKey(id);
        EnterContainerIfNeeded(type);
        return StreamTagVisitor.EntryResult.Enter;
    }

    private void EnterContainerIfNeeded(TagType type)
    {
        if (type == ListTag.ListTagType.Instance)
            _containerStack.Push(new ListBuilder());
        else if (type == CompoundTag.CompoundTagType.Instance)
            _containerStack.Push(new CompoundBuilder());
    }

    public virtual StreamTagVisitor.ValueResult VisitContainerEnd()
    {
        var container = _containerStack.Pop();
        var tag = container.Build();
        if (tag != null)
            _containerStack.Peek().AcceptValue(tag);
        return StreamTagVisitor.ValueResult.Continue;
    }

    public virtual StreamTagVisitor.ValueResult VisitRootEntry(TagType type)
    {
        EnterContainerIfNeeded(type);
        return StreamTagVisitor.ValueResult.Continue;
    }

    // ============ 容器构建器 ============

    private interface ContainerBuilder
    {
        void AcceptValue(Tag tag);
        Tag? Build();
        void AcceptKey(string id) { }
    }

    private sealed class RootBuilder : ContainerBuilder
    {
        private Tag? _result;

        public void AcceptValue(Tag tag) => _result = tag;
        public Tag? Build() => _result;
    }

    private sealed class CompoundBuilder : ContainerBuilder
    {
        private readonly CompoundTag _compound = new();
        private string _lastId = "";

        public void AcceptKey(string id) => _lastId = id;
        public void AcceptValue(Tag tag) => _compound.Put(_lastId, tag);
        public Tag Build() => _compound;
    }

    private sealed class ListBuilder : ContainerBuilder
    {
        private readonly ListTag _list = new();

        public void AcceptValue(Tag tag) => _list.AddAndUnwrap(tag);
        public Tag Build() => _list;
    }
}

