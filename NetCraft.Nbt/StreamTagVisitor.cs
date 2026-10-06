namespace NetCraft.Nbt;

//Streaming NBT visitor. Mirrors vanilla net.minecraft.nbt.StreamTagVisitor.
//Unlike TagVisitor, streaming access does not build full Tag objects and works on raw data.
//Used for efficient parsing of large NBT (chunk data, for example).
//Fully aligned with the vanilla interface:
//<item>ValueResult has three states: Continue / Break / Halt (no Skip).</item>
//<item>EntryResult has four states: Enter / Skip / Break / Halt.</item>
//<item>VisitList returns ValueResult (no separate NestedResult).</item>
//<item>VisitEnd / VisitContainerEnd return ValueResult.</item>
//<item>List elements are visited through a separate VisitElement callback (same as vanilla).</item>
//Array access takes ReadOnlySpan&lt;T&gt; (better than vanilla byte[], no array allocation),
//callers that need an array (such as CollectToTag) convert with System.MemoryExtensions.ToArray.
public interface StreamTagVisitor
{
    //Result of visiting a container entry.
    public enum EntryResult
    {
        //Enter the current entry and visit its children normally.
        Enter,

        //Skip the current entry's data (the caller skips the bytes) and continue with sibling entries.
        Skip,

        //Stop visiting the current container (leave this level) but end it normally (VisitContainerEnd fires).
        Break,

        //Stop the whole parse immediately (no further callbacks).
        Halt,
    }

    //Result of visiting a value.
    public enum ValueResult
    {
        //Continue visiting.
        Continue,

        //End the current container and fire VisitContainerEnd, then continue with the outer level.
        Break,

        //Stop the whole parse immediately (VisitContainerEnd and later callbacks do not fire).
        Halt,
    }

    // ============ scalar value visitors ============

    ValueResult VisitEnd();

    ValueResult VisitString(string value);

    ValueResult VisitByte(byte value);

    ValueResult VisitShort(short value);

    ValueResult VisitInt(int value);

    ValueResult VisitLong(long value);

    ValueResult VisitFloat(float value);

    ValueResult VisitDouble(double value);

    // ============ array visitors ============

    ValueResult VisitByteArray(ReadOnlySpan<byte> value);

    ValueResult VisitIntArray(ReadOnlySpan<int> value);

    ValueResult VisitLongArray(ReadOnlySpan<long> value);

    // ============ container visitors ============

    //Start visiting a list. elementType is the element type and length the element count.
    ValueResult VisitList(TagType elementType, int length);

    //Start visiting an unnamed container entry (ListTag element / TagVisitor path).
    EntryResult VisitEntry(TagType type);

    //Start visiting a named field of a compound tag.
    EntryResult VisitEntry(TagType type, string name);

    //Visit a list element (index is the position). Vanilla uses this to tell CompoundTag fields from ListTag elements.
    EntryResult VisitElement(TagType type, int index);

    //End of a container. Vanilla visitContainerEnd, returns ValueResult.
    ValueResult VisitContainerEnd();

    //Visit the root entry (the type declaration of the outermost Tag).
    ValueResult VisitRootEntry(TagType type);
}

//Simple StreamTagVisitor base class; every method returns Continue/Enter by default.
//Subclasses only override the methods they care about. Mirrors vanilla interface default method behavior.
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

