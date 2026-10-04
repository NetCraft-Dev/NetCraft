namespace NetCraft.Storage;

//位存储接口对应原版net.minecraft.util.BitStorage
//紧凑存储int数组每元素占固定bit数
public interface BitStorage
{
    //返回旧值并设置新值
    int GetAndSet(int index, int value);

    void Set(int index, int value);

    int Get(int index);

    //原始long数组对应原版getRaw
    long[] GetRaw();

    //元素数量对应原版getSize
    int Size { get; }

    //每元素bit数对应原版getBits
    int Bits { get; }

    //遍历所有元素对应原版getAll
    void GetAll(Action<int> output);

    //解包到int数组对应原版unpack
    void Unpack(int[] output);

    BitStorage Copy();
}
