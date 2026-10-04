namespace NetCraft.DataFixer.Optics.Profunctors;

using NetCraft.DataFixer.Kinds;

//Mapping映射profunctor对应原版com.mojang.datafixers.optics.profunctors.Mapping
//提供mapping把A->B提升到任意Functor容器上继承TraversalP
public interface Mapping<P, TMu> : TraversalP<P, TMu> where P : K2 where TMu : IMappingMu
{
    //mapping用Functor.map把App2<P,A,B>提升为App2<P,App<F,A>,App<F,B>>
    //TMu2显式声明Functor的标记类型因为C#无Java通配符
    App2<P, App<F, A>, App<F, B>> Mapping<A, B, F, TMu2>(Functor<F, TMu2> functor, App2<P, A, B> input) where F : K1 where TMu2 : IFunctorMu;
}
