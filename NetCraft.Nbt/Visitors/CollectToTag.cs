using NetCraft.Nbt;

namespace NetCraft.Nbt.Visitors;

//Streaming visitor that builds an NBT stream into a full Tag tree. Mirrors vanilla net.minecraft.nbt.visitors.CollectToTag.
//Subclasses SkipFields and CollectFields add field filtering on top of it.
public class CollectToTag : StreamTagVisitor
{
    private readonly Stack<ContainerBuilder> _containerStack = new();

    public CollectToTag()
    {
        _containerStack.Push(new RootBuilder());
    }

    //Get the build result (the outermost Tag).
    public Tag GetResult() => _containerStack.First().Build()!;

    //Current container nesting depth (stack size - 1).
    protected int Depth => _containerStack.Count - 1;

    private void AppendEntry(Tag instance)
    {
        _containerStack.Peek().AcceptValue(instance);
    }

    // ============ scalar values ============

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

    // ============ containers ============

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

    // ============ container builders ============

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

