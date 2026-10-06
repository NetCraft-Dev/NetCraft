namespace NetCraft.Storage;

//Bit storage interface, maps to vanilla net.minecraft.util.BitStorage
//Densely packed int array, each element occupies a fixed number of bits
public interface BitStorage
{
    //Return the old value and set the new value
    int GetAndSet(int index, int value);

    void Set(int index, int value);

    int Get(int index);

    //Raw long array, maps to vanilla getRaw
    long[] GetRaw();

    //Element count, maps to vanilla getSize
    int Size { get; }

    //Bits per element, maps to vanilla getBits
    int Bits { get; }

    //Iterate all elements, maps to vanilla getAll
    void GetAll(Action<int> output);

    //Unpack into an int array, maps to vanilla unpack
    void Unpack(int[] output);

    BitStorage Copy();
}
