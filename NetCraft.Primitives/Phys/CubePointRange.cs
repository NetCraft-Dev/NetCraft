namespace NetCraft.Primitives.Phys;

//CubePointRange 等分点序列 对应原版 CubePointRange
//把单位长度切成 parts 份给出 parts+1 个切分点 离散立方网格的坐标来源
public sealed class CubePointRange : AbstractDoubleList
{
    private readonly int _parts;

    public CubePointRange(int parts)
    {
        if (parts <= 0) throw new ArgumentException("至少需要 1 份", nameof(parts));
        _parts = parts;
    }

    //必须是浮点除 反编译产物把 double 转换丢了 写成整数除会全落成 0
    public override double this[int index] => (double)index / _parts;

    public override int Count => _parts + 1;
}
