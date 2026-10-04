using System.Collections;

namespace NetCraft.Primitives.Phys;

//AbstractDoubleList 只读 double 序列基类 对应原版 AbstractDoubleList
//子类只给索引器与 Count 枚举按索引顺序遍历
public abstract class AbstractDoubleList : IReadOnlyList<double>
{
    public abstract double this[int index] { get; }

    public abstract int Count { get; }

    public IEnumerator<double> GetEnumerator()
    {
        for (var i = 0; i < Count; i++) yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
