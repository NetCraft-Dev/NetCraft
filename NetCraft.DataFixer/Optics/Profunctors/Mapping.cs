namespace NetCraft.DataFixer.Optics.Profunctors;

using NetCraft.DataFixer.Kinds;

//Mapping mapping profunctor maps to vanilla com.mojang.datafixers.optics.profunctors.Mapping
//provides mapping to lift A->B over any Functor container; inherits TraversalP
public interface Mapping<P, TMu> : TraversalP<P, TMu> where P : K2 where TMu : IMappingMu
{
    //mapping uses Functor.map to lift App2<P,A,B> to App2<P,App<F,A>,App<F,B>>
    //TMu2 explicitly declares the Functor marker type because C# has no Java wildcards
    App2<P, App<F, A>, App<F, B>> Mapping<A, B, F, TMu2>(Functor<F, TMu2> functor, App2<P, A, B> input) where F : K1 where TMu2 : IFunctorMu;
}
