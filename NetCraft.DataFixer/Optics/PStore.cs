namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;

//PStores container holding the Mu marker, avoiding generic nesting
public static class PStores
{
    //unary HKT marker; I/J are the pos/peek types
    public sealed class Mu<I, J> : K1 { }
}

//PStore store container maps to vanilla com.mojang.datafixers.optics.PStore
//peek takes an X value by J and pos returns the position I; the Functor instance is based on this
public interface PStore<I, J, X> : App<PStores.Mu<I, J>, X>
{
    //peek takes X by indexing with J
    X Peek(J j);
    //pos returns the current position I
    I Pos();

    //recover the type application as PStore<I,J,X>
    static PStore<I, J, X> Unbox(App<PStores.Mu<I, J>, X> box)
        => (PStore<I, J, X>)(object)box!;
}

//PStore concrete implementation holding the peek and pos delegates
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

//PStoreInstance as the Functor instance
//map composes peek with func.compose(peek), preserving pos
public sealed class PStoreInstance<I, J> : Functor<PStores.Mu<I, J>, PStoreInstance<I, J>.Mu>
{
    public sealed class Mu : IFunctorMu { }
    public static readonly PStoreInstance<I, J> InstanceOf = new();
    private PStoreInstance() { }

    //map combines func with the original peek's output, preserving pos
    public App<PStores.Mu<I, J>, R> Map<T, R>(Func<T, R> func, App<PStores.Mu<I, J>, T> ts)
    {
        var input = PStore<I, J, T>.Unbox(ts);
        return Optics.PStore<I, J, R>(j => func(input.Peek(j)), input.Pos);
    }
}
