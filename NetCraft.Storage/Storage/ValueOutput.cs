using NetCraft.Codec;

namespace NetCraft.Storage;

//ValueOutput NBT 写入抽象对应原版 net.minecraft.world.level.storage.ValueOutput
//提供按字段名写标量与子节点与列表的入口
public interface ValueOutput
{
    //按 Codec 把值序列化到字段
    void Store<T>(string name, Codec<T> codec, T value);

    //按 Codec 把可空值序列化到字段 null 跳过
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

    //创建子节点输出
    ValueOutput Child(string name);

    //创建子节点列表输出
    ValueOutputList ChildrenList(string name);

    //按 Codec 创建类型化列表输出
    TypedOutputList<T> List<T>(string name, Codec<T> codec);

    //丢弃指定字段
    void Discard(string name);

    bool IsEmpty();

    //类型化列表输出对应原版 ValueOutput.TypedOutputList
    public interface TypedOutputList<T>
    {
        void Add(T value);
        bool IsEmpty();
    }

    //子节点列表输出对应原版 ValueOutput.ValueOutputList
    public interface ValueOutputList
    {
        ValueOutput AddChild();
        void DiscardLast();
        bool IsEmpty();
    }
}
