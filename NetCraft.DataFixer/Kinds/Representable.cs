namespace NetCraft.DataFixer.Kinds;

using NetCraft.DataFixer;

//Representable functor maps to vanilla com.mojang.datafixers.kinds.Representable
//provides to/from conversions between the container and Reader functions; FunctionType.ReaderInstance implements this
public interface Representable<T, C, TMu> : Functor<T, TMu> where T : K1 where TMu : IRepresentableMu
{
    //marker IRepresentableMu chain-inherits IFunctorMu and K1
    interface Mu : IRepresentableMu { }

    static Representable<T2, C2, TMu2> Unbox<T2, C2, TMu2>(App<TMu2, T2> proofBox) where T2 : K1 where TMu2 : IRepresentableMu
        => (Representable<T2, C2, TMu2>)(object)proofBox;

    //convert the container to the Reader function space
    App<FunctionTypes.ReaderMu<C>, A> To<A>(App<T, A> input);
    //convert the Reader function space back to the container
    App<T, A> From<A>(App<FunctionTypes.ReaderMu<C>, A> input);
}
