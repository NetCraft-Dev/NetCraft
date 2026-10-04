namespace NetCraft.Gpu;

//Divisor 均分工具对标原版 com.mojang.math.Divisor
//把 total 均分为 parts 份余数分到前几个每次 NextInt 返回一份
//GridLayout 把跨多行多列的元素高度/宽度均分到各行列用
public struct Divisor
{
    private readonly int _total;
    private readonly int _parts;
    private int _given;

    public Divisor(int total, int parts)
    {
        _total = total;
        _parts = parts;
        _given = 0;
    }

    //NextInt 返回下一份 baseShare=total/parts 余数分到前 remainder 份
    //总 parts 次调用之和等于 total
    public int NextInt()
    {
        if (_given >= _parts) return 0;
        int baseShare = _total / _parts;
        int remainder = _total % _parts;
        int result = baseShare + (_given < remainder ? 1 : 0);
        _given++;
        return result;
    }
}
