namespace NetCraft.DataFixer.Optics.Profunctors;

using NetCraft.DataFixer.Kinds;

//FunctorProfunctor函子profunctor对应原版com.mojang.datafixers.optics.profunctors.FunctorProfunctor
//提供distribute把profunctor分布到任意Functor容器上
//T是Functor的证明类型Cartesian/Cocartesian/TraversalP的toFP系列返回此类型
public interface FunctorProfunctor<T, P, TMu> : Kind2<P, TMu> where T : K1 where P : K2 where TMu : IFunctorProfunctorMu
{
    //distribute把App2<P,A,B>分布到App<F,A>->App<F,B>
    App2<P, App<F, A>, App<F, B>> Distribute<A, B, F>(App<T, F> proof, App2<P, A, B> input) where F : K1;
}
