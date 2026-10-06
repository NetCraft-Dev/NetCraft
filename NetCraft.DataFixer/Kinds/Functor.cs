namespace NetCraft.DataFixer.Kinds;

//functor typeclass providing the Map operation
public interface Functor<TF, TMu> : Kind1<TF, TMu> where TF : K1 where TMu : IFunctorMu
{
    //marker IFunctorMu chain-inherits IKind1Mu and K1
    interface Mu : IFunctorMu { }

    static Functor<TF2, TMu2> Unbox<TF2, TMu2>(App<TMu2, TF2> proofBox) where TF2 : K1 where TMu2 : IFunctorMu
        => (Functor<TF2, TMu2>)(object)proofBox;

    //map a function over the elements inside the container
    App<TF, R> Map<T, R>(Func<T, R> func, App<TF, T> ts);
}
