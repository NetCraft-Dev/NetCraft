namespace NetCraft.DataFixer.Optics.Profunctors;

using NetCraft.DataFixer.Kinds;

//FunctorProfunctor functor profunctor maps to vanilla com.mojang.datafixers.optics.profunctors.FunctorProfunctor
//provides distribute to distribute a profunctor over any Functor container
//T is the Functor proof type; the toFP family of Cartesian/Cocartesian/TraversalP returns this type
public interface FunctorProfunctor<T, P, TMu> : Kind2<P, TMu> where T : K1 where P : K2 where TMu : IFunctorProfunctorMu
{
    //distribute distributes App2<P,A,B> to App<F,A>->App<F,B>
    App2<P, App<F, A>, App<F, B>> Distribute<A, B, F>(App<T, F> proof, App2<P, A, B> input) where F : K1;
}
