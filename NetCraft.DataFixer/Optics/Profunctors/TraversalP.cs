namespace NetCraft.DataFixer.Optics.Profunctors;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics;
using NetCraft.DataFixer.Util;

//TraversalP遍历profunctor对应原版com.mojang.datafixers.optics.profunctors.TraversalP
//聚合AffineP并扩展WanderTraversal基于此
//提供traverse默认方法基于wanderFirst/Left默认方法基于traverse+dimap
public interface TraversalP<P, TMu> : AffineP<P, TMu> where P : K2 where TMu : ITraversalPMu
{
    //还原类型应用为TraversalP
    //FunctionTypeInstance只实现TraversalP<FunctionTypes.Mu,FunctionTypeInstance.Mu>
    //调用方传ITraversalPMu作为TMu2强转失败用Unsafe.As绕过运行时类型检查对齐Java类型擦除
    static TraversalP<P2, TMu2> Unbox<P2, TMu2>(App<TMu2, P2> proofBox) where P2 : K2 where TMu2 : ITraversalPMu
    {
        var obj = (object)proofBox;
        return System.Runtime.CompilerServices.Unsafe.As<object, TraversalP<P2, TMu2>>(ref obj);
    }

    //wander用Wander策略把A->B扩展为S->T
    App2<P, S, T> Wander<S, T, A, B>(Wander<S, T, A, B> wander, App2<P, A, B> input);

    //traverse用Traversable把A->B扩展到App<T,A>->App<T,B>基于wander+匿名Wander策略
    //TMu3显式声明Traversable的标记类型因为C#无Java通配符
    App2<P, App<T, A>, App<T, B>> Traverse<T, TMu3, A, B>(Traversable<T, TMu3> traversable, App2<P, A, B> input) where T : K1 where TMu3 : ITraversableMu
        => Wander<App<T, A>, App<T, B>, A, B>(new TraverseWander<T, TMu3, A, B>(traversable), input);

    //first用Pair的Traversable扩展到Pair第一分量基于traverse+dimap恒等
    public App2<P, Pair<A, C>, Pair<B, C>> First<A, B, C>(App2<P, A, B> input)
        => Dimap<App<Pairs.Mu<C>, A>, App<Pairs.Mu<C>, B>, Pair<A, C>, Pair<B, C>>(
            Traverse(new PairInstance<C>(), input),
            pair => (App<Pairs.Mu<C>, A>)(object)pair!,
            app => Pair<B, C>.Unbox(app)
        );

    //left用Either的Traversable扩展到Either左分支基于traverse+dimap恒等
    public App2<P, Either<A, C>, Either<B, C>> Left<A, B, C>(App2<P, A, B> input)
        => Dimap<App<Eithers.Mu<C>, A>, App<Eithers.Mu<C>, B>, Either<A, C>, Either<B, C>>(
            Traverse(new EitherInstance<C>(), input),
            either => (App<Eithers.Mu<C>, A>)(object)either!,
            app => Either<B, C>.Unbox(app)
        );
}

//traverse用的Wander策略实现持有Traversable委托把App<T,A>用traversable.traverse处理
internal sealed class TraverseWander<T, TMu3, A, B> : Wander<App<T, A>, App<T, B>, A, B> where T : K1 where TMu3 : ITraversableMu
{
    private readonly Traversable<T, TMu3> _traversable;
    internal TraverseWander(Traversable<T, TMu3> traversable) => _traversable = traversable;

    public Func<App<T, A>, App<F, App<T, B>>> Wander<F, TMu2>(Applicative<F, TMu2> applicative, Func<A, App<F, B>> input) where F : K1 where TMu2 : IApplicativeMu
        => ta => _traversable.Traverse(applicative, input, ta);
}
