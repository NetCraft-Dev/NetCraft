using NetCraft.Codec;

namespace NetCraft.Storage;

//ValueOutput, NBT write abstraction, maps to vanilla net.minecraft.world.level.storage.ValueOutput
//Provides entry points to write scalars, children and lists by field name
public interface ValueOutput
{
    //Serialize a value into a field with a Codec
    void Store<T>(string name, Codec<T> codec, T value);

    //Serialize a nullable value into a field with a Codec; null is skipped
    void StoreNullable<T>(string name, Codec<T> codec, T? value) where T : class;

    void PutBoolean(string name, bool value);
    void PutByte(string name, byte value);
    void PutShort(string name, short value);
    void PutInt(string name, int value);
    void PutLong(string name, long value);
    void PutFloat(string name, float value);
    void PutDouble(string name, double value);
    void PutString(string name, string value);
    void PutIntArray(string name, int[] value);

    //Create a child node output
    ValueOutput Child(string name);

    //Create a child node list output
    ValueOutputList ChildrenList(string name);

    //Create a typed list output with a Codec
    TypedOutputList<T> List<T>(string name, Codec<T> codec);

    //Discard the given field
    void Discard(string name);

    bool IsEmpty();

    //Typed list output, maps to vanilla ValueOutput.TypedOutputList
    public interface TypedOutputList<T>
    {
        void Add(T value);
        bool IsEmpty();
    }

    //Child node list output, maps to vanilla ValueOutput.ValueOutputList
    public interface ValueOutputList
    {
        ValueOutput AddChild();
        void DiscardLast();
        bool IsEmpty();
    }
}
