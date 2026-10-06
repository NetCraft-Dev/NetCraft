namespace NetCraft.DataFixer.Kinds;

using NetCraft.DataFixer.Util;

//cocartesian typeclass maps to vanilla com.mojang.datafixers.kinds.CocartesianLike
public interface CocartesianLike<T, C, TMu> : Functor<T, TMu>, Traversable<T, TMu> where T : K1 where TMu : ICocartesianLikeMu
{
    //marker ICocartesianLikeMu chain-inherits ITraversableMu and K1
    interface Mu : ICocartesianLikeMu { }

    static CocartesianLike<T2, C2, TMu2> Unbox<T2, C2, TMu2>(App<TMu2, T2> proofBox) where T2 : K1 where TMu2 : ICocartesianLikeMu
        => (CocartesianLike<T2, C2, TMu2>)(object)proofBox;

    //convert the container to an Either representation
    App<Eithers.Mu<C>, A> To<A>(App<T, A> input);
    //convert an Either representation back to the container
    App<T, A> From<A>(App<Eithers.Mu<C>, A> input);
}
