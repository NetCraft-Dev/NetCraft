using NetCraft.Nbt;

namespace NetCraft.Nbt.Visitors;

//跳过所有内容的 StreamTagVisitor。对应原版 net.minecraft.nbt.visitors.SkipAll。
//所有标量值访问返回 Continue；所有容器条目访问返回 Skip（不进入子项，但消耗字节由调用方处理）。
//用于快速跳过 NBT 数据而不构建任何 Tag 对象。
//C# 中接口 default method 默认就是 virtual，无需显式 virtual 修饰符。
public interface SkipAll : StreamTagVisitor
{
    //单例实例。
    public static readonly SkipAll Instance = new SkipAllVisitor();

    StreamTagVisitor.ValueResult StreamTagVisitor.VisitEnd() => StreamTagVisitor.ValueResult.Continue;

    StreamTagVisitor.ValueResult StreamTagVisitor.VisitString(string value) => StreamTagVisitor.ValueResult.Continue;

    StreamTagVisitor.ValueResult StreamTagVisitor.VisitByte(byte value) => StreamTagVisitor.ValueResult.Continue;

    StreamTagVisitor.ValueResult StreamTagVisitor.VisitShort(short value) => StreamTagVisitor.ValueResult.Continue;

    StreamTagVisitor.ValueResult StreamTagVisitor.VisitInt(int value) => StreamTagVisitor.ValueResult.Continue;

    StreamTagVisitor.ValueResult StreamTagVisitor.VisitLong(long value) => StreamTagVisitor.ValueResult.Continue;

    StreamTagVisitor.ValueResult StreamTagVisitor.VisitFloat(float value) => StreamTagVisitor.ValueResult.Continue;

    StreamTagVisitor.ValueResult StreamTagVisitor.VisitDouble(double value) => StreamTagVisitor.ValueResult.Continue;

    StreamTagVisitor.ValueResult StreamTagVisitor.VisitByteArray(ReadOnlySpan<byte> value) => StreamTagVisitor.ValueResult.Continue;

    StreamTagVisitor.ValueResult StreamTagVisitor.VisitIntArray(ReadOnlySpan<int> value) => StreamTagVisitor.ValueResult.Continue;

    StreamTagVisitor.ValueResult StreamTagVisitor.VisitLongArray(ReadOnlySpan<long> value) => StreamTagVisitor.ValueResult.Continue;

    StreamTagVisitor.ValueResult StreamTagVisitor.VisitList(TagType elementType, int size) => StreamTagVisitor.ValueResult.Continue;

    StreamTagVisitor.EntryResult StreamTagVisitor.VisitElement(TagType type, int index) => StreamTagVisitor.EntryResult.Skip;

    StreamTagVisitor.EntryResult StreamTagVisitor.VisitEntry(TagType type) => StreamTagVisitor.EntryResult.Skip;

    StreamTagVisitor.EntryResult StreamTagVisitor.VisitEntry(TagType type, string id) => StreamTagVisitor.EntryResult.Skip;

    StreamTagVisitor.ValueResult StreamTagVisitor.VisitContainerEnd() => StreamTagVisitor.ValueResult.Continue;

    StreamTagVisitor.ValueResult StreamTagVisitor.VisitRootEntry(TagType type) => StreamTagVisitor.ValueResult.Continue;
}

//SkipAll 的具体实现类（用于提供单例实例）。
//对应原版 SkipAll 接口的匿名实现类 SkipAll.1。
internal sealed class SkipAllVisitor : SkipAll
{
}

