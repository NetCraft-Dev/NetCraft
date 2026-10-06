using NetCraft.Nbt;

namespace NetCraft.Nbt.Visitors;

//StreamTagVisitor that skips everything. Mirrors vanilla net.minecraft.nbt.visitors.SkipAll.
//Every scalar value visit returns Continue; every container entry visit returns Skip (no descent into children, the caller consumes the bytes).
//Used to skip NBT data quickly without building any Tag object.
//In C# interface default methods are already virtual, so no explicit virtual modifier is needed.
public interface SkipAll : StreamTagVisitor
{
    //Singleton instance.
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

//Concrete implementation class of SkipAll (it provides the singleton instance).
//Mirrors SkipAll.1, the anonymous implementation of the vanilla SkipAll interface.
internal sealed class SkipAllVisitor : SkipAll
{
}

