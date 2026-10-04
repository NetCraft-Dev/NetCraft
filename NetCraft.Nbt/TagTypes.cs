namespace NetCraft.Nbt;

//NBT Tag 类型注册表。对应原版 net.minecraft.nbt.TagTypes。
//按 Tag ID（0-12）索引到对应的 TagType。
//顺序严格对应原版，影响 Tag ID 分配和网络/存档字节兼容性。
public static class TagTypes
{
    private static readonly TagType[] Types =
    {
        EndTag.EndTagType.Instance,             // 0  TAG_End
        ByteTag.ByteTagType.Instance,           // 1  TAG_Byte
        ShortTag.ShortTagType.Instance,         // 2  TAG_Short
        IntTag.IntTagType.Instance,             // 3  TAG_Int
        LongTag.LongTagType.Instance,           // 4  TAG_Long
        FloatTag.FloatTagType.Instance,         // 5  TAG_Float
        DoubleTag.DoubleTagType.Instance,       // 6  TAG_Double
        ByteArrayTag.ByteArrayTagType.Instance,  // 7  TAG_Byte_Array
        StringTag.StringTagType.Instance,       // 8  TAG_String
        ListTag.ListTagType.Instance,           // 9  TAG_List
        CompoundTag.CompoundTagType.Instance,   // 10 TAG_Compound
        IntArrayTag.IntArrayTagType.Instance,   // 11 TAG_Int_Array
        LongArrayTag.LongArrayTagType.Instance, // 12 TAG_Long_Array
    };

    //按 Tag ID 获取类型描述。无效 ID 返回 InvalidTagType。
    public static TagType GetType(byte id)
    {
        if (id >= 0 && id < Types.Length)
            return Types[id];
        return TagType.CreateInvalid(id);
    }

    //按 Tag ID 获取类型描述（int 重载）。
    public static TagType GetType(int id) => GetType((byte)id);

    //所有支持的 Tag 类型数量。
    public static int Count => Types.Length;
}

