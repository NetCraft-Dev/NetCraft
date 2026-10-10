namespace NetCraft.Client.Blaze3d;

//IndexType index buffer element type, aligns with vanilla com.mojang.blaze3d.IndexType
public enum IndexType
{
    Short = 2,
    Int = 4
}

public static class IndexTypeExtensions
{
    //Bytes the element size in bytes
    public static int Bytes(this IndexType type) => (int)type;

    //Least the smallest index type that can address length elements, maps to vanilla IndexType.least
    public static IndexType Least(int length) => (length & unchecked((int)0xFFFF0000)) != 0 ? IndexType.Int : IndexType.Short;
}
