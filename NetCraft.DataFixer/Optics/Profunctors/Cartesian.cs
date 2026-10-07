namespace NetCraft.DataFixer.Optics.Profunctors;

using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Util;

//Cartesian profunctor maps to vanilla com.mojang.datafixers.optics.profunctors.Cartesian
//provides first/second operations on Pair; Lens is based on this
public interface Cartesian<P, TMu> : Profunctor<P, TMu> where P : K2 where TMu : ICartesianMu
{
    //recover the type application as Cartesian
    //use Unsafe.As to bypass the TMu type parameter mismatch, aligning with Java type erasure semantics
    //at runtime proofBox implements Cartesian<P,concrete Mu> but Unbox expects the Cartesian<P,ICartesianMu> interface
    static Cartesian<P2, TMu2> Unbox<P2, TMu2>(App<TMu2, P2> proofBox) where P2 : K2 where TMu2 : ICartesianMu
    {
        var obj = (object)proofBox;
        return System.Runtime.CompilerServices.Unsafe.As<object, Cartesian<P2, TMu2>>(ref obj);
    }

    //first extends A->B to Pair<A,C>->Pair<B,C>, attaching the C component
    App2<P, Pair<A, C>, Pair<B, C>> First<A, B, C>(App2<P, A, B> input);

    //second extends A->B to Pair<C,A>->Pair<C,B>, attaching the C component; implemented via swap to first
    //Dimap type parameters are specified explicitly to avoid C# lambda inference failures
    App2<P, Pair<C, A>, Pair<C, B>> Second<A, B, C>(App2<P, A, B> input)
        => Dimap<Pair<A, C>, Pair<B, C>, Pair<C, A>, Pair<C, B>>(
            First<A, B, C>(input),
            p => p.Swap(),
            p => p.Swap()
        );
}
