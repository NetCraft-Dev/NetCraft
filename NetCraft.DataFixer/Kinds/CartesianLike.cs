namespace NetCraft.DataFixer.Kinds;

using NetCraft.DataFixer.Util;

//Cartesian typeclass maps to vanilla com.mojang.datafixers.kinds.CartesianLike
public interface CartesianLike<T, C, TMu> : Functor<T, TMu>, Traversable<T, TMu> where T : K1 where TMu : ICartesianLikeMu
{
    //marker ICartesianLikeMu chain-inherits ITraversableMu and K1
    interface Mu : ICartesianLikeMu { }

    static CartesianLike<T2, C2, TMu2> Unbox<T2, C2, TMu2>(App<TMu2, T2> proofBox) where T2 : K1 where TMu2 : ICartesianLikeMu
        => (CartesianLike<T2, C2, TMu2>)(object)proofBox;

    //convert the container to a Pair representation
    App<Pairs.Mu<C>, A> To<A>(App<T, A> input);
    //convert a Pair representation back to the container
    App<T, A> From<A>(App<Pairs.Mu<C>, A> input);
}
