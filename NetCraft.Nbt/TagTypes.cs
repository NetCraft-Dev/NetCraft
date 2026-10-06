namespace NetCraft.Nbt;

//NBT Tag type registry. Mirrors vanilla net.minecraft.nbt.TagTypes.
//Indexes Tag IDs (0-12) to the matching TagType.
//The order matches vanilla exactly, affecting Tag ID assignment and network/save byte compatibility.
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

    //Get the type description by Tag ID. Invalid IDs return InvalidTagType.
    public static TagType GetType(byte id)
    {
        if (id >= 0 && id < Types.Length)
            return Types[id];
        return TagType.CreateInvalid(id);
    }

    //Get the type description by Tag ID (int overload).
    public static TagType GetType(int id) => GetType((byte)id);

    //Number of supported Tag types.
    public static int Count => Types.Length;
}

