namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;

//PStores容器存放Mu标记避免泛型嵌套
public static class PStores
{
    //一元HKT标记I/J为pos/peek类型
    public sealed class Mu<I, J> : K1 { }
}

//PStore存储容器对应原版com.mojang.datafixers.optics.PStore
//peek按J取X值pos返回I位置Functor实例基于此
public interface PStore<I, J, X> : App<PStores.Mu<I, J>, X>
{
    //peek按J索引取X
    X Peek(J j);
    //pos返回当前位置I
    I Pos();

    //还原类型应用为PStore<I,J,X>
    static PStore<I, J, X> Unbox(App<PStores.Mu<I, J>, X> box)
        => (PStore<I, J, X>)(object)box!;
}

//PStore具体实现持有peek和pos委托
internal sealed class PStoreImpl<I, J, X> : PStore<I, J, X>
{
    private readonly Func<J, X> _peek;
    private readonly Func<I> _pos;
    internal PStoreImpl(Func<J, X> peek, Func<I> pos)
    {
        _peek = peek;
        _pos = pos;
    }
    public X Peek(J j) => _peek(j);
    public I Pos() => _pos();
}

//PStoreInstance作为Functor实例
//map用func.compose(peek)组合peek保留pos
public sealed class PStoreInstance<I, J> : Functor<PStores.Mu<I, J>, PStoreInstance<I, J>.Mu>
{
    public sealed class Mu : IFunctorMu { }
    public static readonly PStoreInstance<I, J> InstanceOf = new();
    private PStoreInstance() { }

    //map用func组合原peek的输出pos保留
    public App<PStores.Mu<I, J>, R> Map<T, R>(Func<T, R> func, App<PStores.Mu<I, J>, T> ts)
    {
        var input = PStore<I, J, T>.Unbox(ts);
        return Optics.PStore<I, J, R>(j => func(input.Peek(j)), input.Pos);
    }
}
