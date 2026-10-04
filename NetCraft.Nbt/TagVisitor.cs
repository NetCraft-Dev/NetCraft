namespace NetCraft.Nbt;

//NBT 访问者接口。对应原版 net.minecraft.nbt.TagVisitor。
//与 StreamTagVisitor 不同，TagVisitor 访问已构建的 Tag 对象树。
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

