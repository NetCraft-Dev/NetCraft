namespace NetCraft.Nbt;

//NBT visitor interface. Mirrors vanilla net.minecraft.nbt.TagVisitor.
//Unlike StreamTagVisitor, TagVisitor visits an already-built Tag object tree.
public interface TagVisitor
{
    void VisitByte(ByteTag tag);
    void VisitShort(ShortTag tag);
    void VisitInt(IntTag tag);
    void VisitLong(LongTag tag);
    void VisitFloat(FloatTag tag);
    void VisitDouble(DoubleTag tag);
    void VisitByteArray(ByteArrayTag tag);
    void VisitString(StringTag tag);
    void VisitList(ListTag tag);
    void VisitCompound(CompoundTag tag);
    void VisitIntArray(IntArrayTag tag);
    void VisitLongArray(LongArrayTag tag);
    void VisitEnd(EndTag tag);
}

