namespace NetCraft.DataFixer.Kinds;

//traversable typeclass providing Traverse and Flip
public interface Traversable<TT, TMu> : Functor<TT, TMu> where TT : K1 where TMu : ITraversableMu
{
    //marker ITraversableMu chain-inherits IFunctorMu and K1
    interface Mu : ITraversableMu { }

    static Traversable<TT2, TMu2> Unbox<TT2, TMu2>(App<TMu2, TT2> proofBox) where TT2 : K1 where TMu2 : ITraversableMu
        => (Traversable<TT2, TMu2>)(object)proofBox;

    //traverse the container elements, accumulating results with an Applicative
    App<TF2, App<TT, B>> Traverse<TF2, TMu2, A, B>(Applicative<TF2, TMu2> applicative, Func<A, App<TF2, B>> function, App<TT, A> input) where TF2 : K1 where TMu2 : IApplicativeMu;

    //flip nested containers App<T,App<F,A>> to App<F,App<T,A>>
    App<TF2, App<TT, A>> Flip<TF2, TMu2, A>(Applicative<TF2, TMu2> applicative, App<TT, App<TF2, A>> input) where TF2 : K1 where TMu2 : IApplicativeMu
        => Traverse<TF2, TMu2, App<TF2, A>, A>(applicative, x => x, input);
}
