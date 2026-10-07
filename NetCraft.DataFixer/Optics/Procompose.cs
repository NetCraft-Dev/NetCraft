namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;

//Procompose container holding the Mu marker, avoiding generic nesting
public static class Procomposes
{
    //Mu marker wrapping the two profunctor type constructors F+G
    public sealed class Mu<F, G> : K2 where F : K2 where G : K2 { }
}

//Procompose composes two profunctors, maps to vanilla com.mojang.datafixers.optics.Procompose
//first: lazy supplier of A->C; second: concrete value of C->B; the composition expresses A->B
public sealed class Procompose<F, G, A, B, C> : App2<Procomposes.Mu<F, G>, A, B> where F : K2 where G : K2
{
    private readonly Func<App2<F, A, C>> _first;
    private readonly App2<G, C, B> _second;

    public Procompose(Func<App2<F, A, C>> first, App2<G, C, B> second)
    {
        _first = first;
        _second = second;
    }

    //recover the type application as Procompose; type-erased C uses object as a placeholder
    public static Procompose<F, G, A2, B2, object> Unbox<A2, B2>(App2<Procomposes.Mu<F, G>, A2, B2> box)
        => (Procompose<F, G, A2, B2, object>)(object)box!;

    //first lazily takes the A->C profunctor
    public Func<App2<F, A, C>> First() => _first;
    //second takes the C->B profunctor
    public App2<G, C, B> Second() => _second;
}
