namespace NetCraft.DataFixer.Functions;

using System;
using NetCraft.Codec;
using T = NetCraft.DataFixer.Types;

//PointFree无点函数对应原版com.mojang.datafixers.functions.PointFree
//表示可缓存的优化函数包装eval惰性求值
public abstract class PointFree<T2>
{
    private volatile bool _initialized;
    private Func<DynamicOps<object>, T2>? _value;

    //evalCached惰性求值并缓存结果线程安全双重检查
    public Func<DynamicOps<object>, T2> EvalCached()
    {
        if (!_initialized)
        {
            lock (this)
            {
                if (!_initialized)
                {
                    _value = Eval();
                    _initialized = true;
                }
            }
        }
        return _value!;
    }

    //type返回此PointFree的输出类型
    public abstract T.Type<T2> Type();

    //eval子类提供求值逻辑
    public abstract Func<DynamicOps<object>, T2> Eval();

    //all对所有子项应用规则默认返回自身
    public virtual Optional<PointFree<T2>> All(PointFreeRule rule)
        => Optional<PointFree<T2>>.Of(this);

    //one对唯一子项应用规则默认空
    public virtual Optional<PointFree<T2>> One(PointFreeRule rule)
        => Optional<PointFree<T2>>.Empty();

    //toString带缩进级别
    public abstract string ToString(int level);

    //toString最终委托到带级别版本
    public override string ToString() => ToString(0);

    //indent生成指定级别缩进
    public static string Indent(int level) => new(' ', level);
}
