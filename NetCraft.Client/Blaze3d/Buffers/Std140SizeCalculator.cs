namespace NetCraft.Client.Blaze3d.Buffers;

//Std140SizeCalculator computes the byte size of a std140 uniform block, aligns with vanilla Std140SizeCalculator
//Mirror Std140Builder put-order to get the matching size
public sealed class Std140SizeCalculator
{
    public int Size { get; private set; }

    public Std140SizeCalculator Align(int alignment)
    {
        Size = RoundToward(Size, alignment);
        return this;
    }

    public Std140SizeCalculator PutFloat() { Align(4); Size += 4; return this; }
    public Std140SizeCalculator PutInt() { Align(4); Size += 4; return this; }
    public Std140SizeCalculator PutVec2() { Align(8); Size += 8; return this; }
    public Std140SizeCalculator PutIVec2() { Align(8); Size += 8; return this; }
    public Std140SizeCalculator PutVec3() { Align(16); Size += 16; return this; }
    public Std140SizeCalculator PutIVec3() { Align(16); Size += 16; return this; }
    public Std140SizeCalculator PutVec4() { Align(16); Size += 16; return this; }
    public Std140SizeCalculator PutIVec4() { Align(16); Size += 16; return this; }
    public Std140SizeCalculator PutMat4f() { Align(16); Size += 64; return this; }

    private static int RoundToward(int value, int divisor) => (value + divisor - 1) / divisor * divisor;
}
