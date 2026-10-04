namespace NetCraft.Nbt;

//流式 NBT 访问者。对应原版 net.minecraft.nbt.StreamTagVisitor。
//与 TagVisitor 不同，流式访问不构建完整 Tag 对象，直接处理原始数据。
//用于大 NBT 的高效解析（如区块数据）。
//与原版接口完全对齐：
//<item>ValueResult 三态：Continue / Break / Halt（无 Skip）。</item>
//<item>EntryResult 四态：Enter / Skip / Break / Halt。</item>
//<item>VisitList 返回 ValueResult（无单独 NestedResult）。</item>
//<item>VisitEnd / VisitContainerEnd 返回 ValueResult。</item>
//<item>列表元素访问通过 VisitElement 单独回调（与原版一致）。</item>
//数组访问接受 ReadOnlySpan&lt;T&gt;（优于原版 byte[]，避免数组分配），
//需要数组的调用方（如 CollectToTag）用 System.MemoryExtensions.ToArray 转换。
public interface StreamTagVisitor
{
    //容器条目访问结果。
    public enum EntryResult
    {
        //进入当前条目，正常递归访问其子值。
        Enter,

        //跳过当前条目数据（由调用方负责 skip 字节），继续访问兄弟条目。
        Skip,

        //停止访问当前容器（跳出当前层），但容器本身正常结束（触发 VisitContainerEnd）。
        Break,

        //立即停止整个解析过程（不触发后续回调）。
        Halt,
    }

    //值访问结果。
    public enum ValueResult
    {
        //继续访问。
        Continue,

        //结束当前容器并触发 VisitContainerEnd，但继续外层访问。
        Break,

        //立即停止整个解析（不触发 VisitContainerEnd 等后续回调）。
        Halt,
    }

    // ============ 标量值访问 ============

    ValueResult VisitEnd();

    ValueResult VisitString(string value);

    ValueResult VisitByte(byte value);

    ValueResult VisitShort(short value);

    ValueResult VisitInt(int value);

    ValueResult VisitLong(long value);

    ValueResult VisitFloat(float value);

    ValueResult VisitDouble(double value);

    // ============ 数组访问 ============

    ValueResult VisitByteArray(ReadOnlySpan<byte> value);

    ValueResult VisitIntArray(ReadOnlySpan<int> value);

    ValueResult VisitLongArray(ReadOnlySpan<long> value);

    // ============ 容器访问 ============

    //开始访问列表。elementType 为元素类型，length 为元素数。
    ValueResult VisitList(TagType elementType, int length);

    //开始访问无名容器条目（ListTag 元素 / TagVisitor 路径）。
    EntryResult VisitEntry(TagType type);

    //开始访问复合标签的有名字段。
    EntryResult VisitEntry(TagType type, string name);

    //访问列表元素（index 为下标）。原版用于区分 CompoundTag 字段与 ListTag 元素。
    EntryResult VisitElement(TagType type, int index);

    //容器结束。原版 visitContainerEnd，返回 ValueResult。
    ValueResult VisitContainerEnd();

    //访问根条目（最外层 Tag 的类型声明）。
    ValueResult VisitRootEntry(TagType type);
}

//简单的 StreamTagVisitor 基类，所有方法默认返回 Continue/Enter。
//子类只需重写关心的方法。对应原版 interface default method 行为。
public abstract class StreamTagVisitorBase : StreamTagVisitor
{
    public virtual StreamTagVisitor.ValueResult VisitRootEntry(TagType type) => StreamTagVisitor.ValueResult.Continue;
    public virtual StreamTagVisitor.ValueResult VisitEnd() => StreamTagVisitor.ValueResult.Continue;
    public virtual StreamTagVisitor.ValueResult VisitString(string value) => StreamTagVisitor.ValueResult.Continue;
    public virtual StreamTagVisitor.ValueResult VisitByte(byte value) => StreamTagVisitor.ValueResult.Continue;
    public virtual StreamTagVisitor.ValueResult VisitShort(short value) => StreamTagVisitor.ValueResult.Continue;
    public virtual StreamTagVisitor.ValueResult VisitInt(int value) => StreamTagVisitor.ValueResult.Continue;
    public virtual StreamTagVisitor.ValueResult VisitLong(long value) => StreamTagVisitor.ValueResult.Continue;
    public virtual StreamTagVisitor.ValueResult VisitFloat(float value) => StreamTagVisitor.ValueResult.Continue;
    public virtual StreamTagVisitor.ValueResult VisitDouble(double value) => StreamTagVisitor.ValueResult.Continue;
    public virtual StreamTagVisitor.ValueResult VisitByteArray(ReadOnlySpan<byte> value) => StreamTagVisitor.ValueResult.Continue;
    public virtual StreamTagVisitor.ValueResult VisitIntArray(ReadOnlySpan<int> value) => StreamTagVisitor.ValueResult.Continue;
    public virtual StreamTagVisitor.ValueResult VisitLongArray(ReadOnlySpan<long> value) => StreamTagVisitor.ValueResult.Continue;
    public virtual StreamTagVisitor.ValueResult VisitList(TagType elementType, int length) => StreamTagVisitor.ValueResult.Continue;
    public virtual StreamTagVisitor.EntryResult VisitEntry(TagType type) => StreamTagVisitor.EntryResult.Enter;
    public virtual StreamTagVisitor.EntryResult VisitEntry(TagType type, string name) => StreamTagVisitor.EntryResult.Enter;
    public virtual StreamTagVisitor.EntryResult VisitElement(TagType type, int index) => StreamTagVisitor.EntryResult.Enter;
    public virtual StreamTagVisitor.ValueResult VisitContainerEnd() => StreamTagVisitor.ValueResult.Continue;
}

