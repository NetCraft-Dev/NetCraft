namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics.Profunctors;
using NetCraft.DataFixer.Util;

//Traversal容器存放Mu标记避免泛型嵌套
public static class Traversals
{
    //二元HKT标记A/B为焦点/新值类型
    public sealed class Mu<A, B> : K2 { }

    //还原类型应用为Traversal<S,T,A,B>
    //box运行时实际是DimapTraversal<...>等具体类型继承Traversal<具体S,T,A,B>
    //castclass在跨泛型实例化下失败用Unsafe.As绕过对齐Java类型擦除语义
    public static Traversal<S, T, A, B> Unbox<S, T, A, B>(App2<Mu<A, B>, S, T> box)
    {
        var obj = (object)box!;
        return System.Runtime.CompilerServices.Unsafe.As<object, Traversal<S, T, A, B>>(ref obj);
    }
}

//Traversal遍历光学对应原版com.mojang.datafixers.optics.Traversal
//继承Wander策略基于TraversalP遍历任意Applicative容器
public interface Traversal<S, T, A, B> : Wander<S, T, A, B>, App2<Traversals.Mu<A, B>, S, T>, Optic<ITraversalPMu, S, T, A, B>
{
    //eval用TraversalP.wander把Wander策略应用到input完成A->B扩展为S->T
    Func<App2<P, A, B>, App2<P, S, T>> Optic<ITraversalPMu, S, T, A, B>.Eval<P>(App<ITraversalPMu, P> proof)
    {
        var traversalP = TraversalP<P, ITraversalPMu>.Unbox(proof);
        return input => traversalP.Wander<S, T, A, B>(this, input);
    }
}

//TraversalInstance作为TraversalP实例对应原版Traversal.Instance
//TMu直接用ITraversalPMu使实例可作为App<ITraversalPMu,Traversals.Mu<A2,B2>>传入Optic.Eval
//First/Left用TraversalP接口默认实现基于Traverse+Dimap只需实现Dimap+Wander
//额外实现Cartesian/Cocartesian/Profunctor的ICartesianMu/ICocartesianMu/IAffinePMu/ITraversalPMu变体接口
//让Lens.Eval等通过Cartesian<Traversals.Mu,ICartesianMu>引用调用Dimap/First命中方法表入口对齐Java类型擦除
public sealed class TraversalInstance<A2, B2> :
    TraversalP<Traversals.Mu<A2, B2>, ITraversalPMu>,
    App<ITraversalPMu, Traversals.Mu<A2, B2>>,
    NetCraft.DataFixer.Optics.Profunctors.Profunctor<Traversals.Mu<A2, B2>, IProfunctorMu>,
    NetCraft.DataFixer.Optics.Profunctors.Cartesian<Traversals.Mu<A2, B2>, ICartesianMu>,
    NetCraft.DataFixer.Optics.Profunctors.Cocartesian<Traversals.Mu<A2, B2>, ICocartesianMu>,
    NetCraft.DataFixer.Optics.Profunctors.Profunctor<Traversals.Mu<A2, B2>, ICartesianMu>,
    NetCraft.DataFixer.Optics.Profunctors.Profunctor<Traversals.Mu<A2, B2>, ICocartesianMu>,
    NetCraft.DataFixer.Optics.Profunctors.AffineP<Traversals.Mu<A2, B2>, IAffinePMu>,
    NetCraft.DataFixer.Optics.Profunctors.Cartesian<Traversals.Mu<A2, B2>, IAffinePMu>,
    NetCraft.DataFixer.Optics.Profunctors.Cocartesian<Traversals.Mu<A2, B2>, IAffinePMu>,
    NetCraft.DataFixer.Optics.Profunctors.Profunctor<Traversals.Mu<A2, B2>, IAffinePMu>,
    NetCraft.DataFixer.Optics.Profunctors.Profunctor<Traversals.Mu<A2, B2>, ITraversalPMu>,
    NetCraft.DataFixer.Optics.Profunctors.Cartesian<Traversals.Mu<A2, B2>, ITraversalPMu>,
    NetCraft.DataFixer.Optics.Profunctors.Cocartesian<Traversals.Mu<A2, B2>, ITraversalPMu>,
    App<ICartesianMu, Traversals.Mu<A2, B2>>,
    App<ICocartesianMu, Traversals.Mu<A2, B2>>,
    App<IAffinePMu, Traversals.Mu<A2, B2>>,
    App<IProfunctorMu, Traversals.Mu<A2, B2>>
{
    public static readonly TraversalInstance<A2, B2> InstanceOf = new();
    private TraversalInstance() { }

    //dimap用DimapTraversal包装inner的wander用g前处理输入h后处理输出
    public Func<App2<Traversals.Mu<A2, B2>, A, B>, App2<Traversals.Mu<A2, B2>, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => input => new DimapTraversal<A2, B2, A, B, C, D>(g, h, Traversals.Unbox<A, B, A2, B2>(input));

    //wander用WanderTraversal组合外层wander与inner的wander
    public App2<Traversals.Mu<A2, B2>, S, T> Wander<S, T, A, B>(Wander<S, T, A, B> wander, App2<Traversals.Mu<A2, B2>, A, B> input)
        => new WanderTraversal<A2, B2, S, T, A, B>(wander, Traversals.Unbox<A, B, A2, B2>(input));

    //first用Pair的Traversable扩展到Pair第一分量基于Wander+TraverseWander+Dimap
    //显式实现因C#接口默认方法不自动满足Cartesian抽象方法
    public App2<Traversals.Mu<A2, B2>, Pair<A, C>, Pair<B, C>> First<A, B, C>(App2<Traversals.Mu<A2, B2>, A, B> input)
    {
        var traversed = Wander<App<Pairs.Mu<C>, A>, App<Pairs.Mu<C>, B>, A, B>(
            new TraverseWander<Pairs.Mu<C>, PairInstance<C>.Mu, A, B>(new PairInstance<C>()), input);
        return Dimap<App<Pairs.Mu<C>, A>, App<Pairs.Mu<C>, B>, Pair<A, C>, Pair<B, C>>(
            pair => (App<Pairs.Mu<C>, A>)(object)pair!,
            app => Pair<B, C>.Unbox(app)
        ).Invoke(traversed);
    }

    //left用Either的Traversable扩展到Either左分支基于Wander+TraverseWander+Dimap
    public App2<Traversals.Mu<A2, B2>, Either<A, C>, Either<B, C>> Left<A, B, C>(App2<Traversals.Mu<A2, B2>, A, B> input)
    {
        var traversed = Wander<App<Eithers.Mu<C>, A>, App<Eithers.Mu<C>, B>, A, B>(
            new TraverseWander<Eithers.Mu<C>, EitherInstance<C>.Mu, A, B>(new EitherInstance<C>()), input);
        return Dimap<App<Eithers.Mu<C>, A>, App<Eithers.Mu<C>, B>, Either<A, C>, Either<B, C>>(
            either => (App<Eithers.Mu<C>, A>)(object)either!,
            app => Either<B, C>.Unbox(app)
        ).Invoke(traversed);
    }

    //显式实现Profunctor<Traversals.Mu<A2,B2>,IProfunctorMu>.Dimap让通过该接口引用调用命中方法表入口
    //对齐Java类型擦除后虚方法分派语义
    Func<App2<Traversals.Mu<A2, B2>, A, B>, App2<Traversals.Mu<A2, B2>, C, D>> NetCraft.DataFixer.Optics.Profunctors.Profunctor<Traversals.Mu<A2, B2>, IProfunctorMu>.Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => Dimap<A, B, C, D>(g, h);

    //显式实现Profunctor<Traversals.Mu<A2,B2>,ICartesianMu>.Dimap让Lens.Eval里cartesian.Dimap命中入口
    Func<App2<Traversals.Mu<A2, B2>, A, B>, App2<Traversals.Mu<A2, B2>, C, D>> NetCraft.DataFixer.Optics.Profunctors.Profunctor<Traversals.Mu<A2, B2>, ICartesianMu>.Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => Dimap<A, B, C, D>(g, h);

    //显式实现Profunctor<Traversals.Mu<A2,B2>,ICocartesianMu>.Dimap
    Func<App2<Traversals.Mu<A2, B2>, A, B>, App2<Traversals.Mu<A2, B2>, C, D>> NetCraft.DataFixer.Optics.Profunctors.Profunctor<Traversals.Mu<A2, B2>, ICocartesianMu>.Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => Dimap<A, B, C, D>(g, h);

    //显式实现Profunctor<Traversals.Mu<A2,B2>,IAffinePMu>.Dimap
    Func<App2<Traversals.Mu<A2, B2>, A, B>, App2<Traversals.Mu<A2, B2>, C, D>> NetCraft.DataFixer.Optics.Profunctors.Profunctor<Traversals.Mu<A2, B2>, IAffinePMu>.Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => Dimap<A, B, C, D>(g, h);

    //显式实现Profunctor<Traversals.Mu<A2,B2>,ITraversalPMu>.Dimap
    Func<App2<Traversals.Mu<A2, B2>, A, B>, App2<Traversals.Mu<A2, B2>, C, D>> NetCraft.DataFixer.Optics.Profunctors.Profunctor<Traversals.Mu<A2, B2>, ITraversalPMu>.Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => Dimap<A, B, C, D>(g, h);

    //显式实现Cartesian<Traversals.Mu<A2,B2>,ICartesianMu>.First让Lens.Eval里cartesian.First命中入口
    App2<Traversals.Mu<A2, B2>, Pair<A, C>, Pair<B, C>> NetCraft.DataFixer.Optics.Profunctors.Cartesian<Traversals.Mu<A2, B2>, ICartesianMu>.First<A, B, C>(App2<Traversals.Mu<A2, B2>, A, B> input)
        => First<A, B, C>(input);

    //显式实现Cartesian<Traversals.Mu<A2,B2>,IAffinePMu>.First
    App2<Traversals.Mu<A2, B2>, Pair<A, C>, Pair<B, C>> NetCraft.DataFixer.Optics.Profunctors.Cartesian<Traversals.Mu<A2, B2>, IAffinePMu>.First<A, B, C>(App2<Traversals.Mu<A2, B2>, A, B> input)
        => First<A, B, C>(input);

    //显式实现Cartesian<Traversals.Mu<A2,B2>,ITraversalPMu>.First
    App2<Traversals.Mu<A2, B2>, Pair<A, C>, Pair<B, C>> NetCraft.DataFixer.Optics.Profunctors.Cartesian<Traversals.Mu<A2, B2>, ITraversalPMu>.First<A, B, C>(App2<Traversals.Mu<A2, B2>, A, B> input)
        => First<A, B, C>(input);

    //显式实现Cocartesian<Traversals.Mu<A2,B2>,ICocartesianMu>.Left
    App2<Traversals.Mu<A2, B2>, Either<A, C>, Either<B, C>> NetCraft.DataFixer.Optics.Profunctors.Cocartesian<Traversals.Mu<A2, B2>, ICocartesianMu>.Left<A, B, C>(App2<Traversals.Mu<A2, B2>, A, B> input)
        => Left<A, B, C>(input);

    //显式实现Cocartesian<Traversals.Mu<A2,B2>,IAffinePMu>.Left
    App2<Traversals.Mu<A2, B2>, Either<A, C>, Either<B, C>> NetCraft.DataFixer.Optics.Profunctors.Cocartesian<Traversals.Mu<A2, B2>, IAffinePMu>.Left<A, B, C>(App2<Traversals.Mu<A2, B2>, A, B> input)
        => Left<A, B, C>(input);

    //显式实现Cocartesian<Traversals.Mu<A2,B2>,ITraversalPMu>.Left
    App2<Traversals.Mu<A2, B2>, Either<A, C>, Either<B, C>> NetCraft.DataFixer.Optics.Profunctors.Cocartesian<Traversals.Mu<A2, B2>, ITraversalPMu>.Left<A, B, C>(App2<Traversals.Mu<A2, B2>, A, B> input)
        => Left<A, B, C>(input);
}

//DimapTraversal用g前处理输入h后处理输出委托inner的wander
//对应原版Traversal.Instance.dimap的匿名Traversal实现
internal sealed class DimapTraversal<A2, B2, A, B, C, D> : Traversal<C, D, A2, B2>
{
    private readonly Func<C, A> _g;
    private readonly Func<B, D> _h;
    private readonly Traversal<A, B, A2, B2> _inner;
    internal DimapTraversal(Func<C, A> g, Func<B, D> h, Traversal<A, B, A2, B2> inner)
    {
        _g = g;
        _h = h;
        _inner = inner;
    }
    public Func<C, App<F, D>> Wander<F, TMu2>(Applicative<F, TMu2> applicative, Func<A2, App<F, B2>> input) where F : K1 where TMu2 : IApplicativeMu
        => c =>
        {
            //_inner运行时是Traversal<具体A,B,A2,B2>被Unsafe.As cast为Traversal<A,B,A2,B2>
            //C#严格泛型不变量下两封闭类型方法表入口不共享直接调Wander抛EntryPointNotFoundException
            //用WanderInvokerCache.GetWanderFunc反射委托缓存调用Wander对齐Java类型擦除后虚方法分派语义
            var wanderFuncObj = WanderInvokerCache.GetWanderFunc<A2, B2, F, TMu2>((object)_inner!, (object)applicative!, input);
            var resultObj = WanderInvokerCache.InvokeWanderFunc(wanderFuncObj, (object)_g(c)!);
            var resultApp = System.Runtime.CompilerServices.Unsafe.As<object, App<F, B>>(ref resultObj!);
            return applicative.Map<B, D>(_h, resultApp);
        };
}

//WanderTraversal组合外层wander与inner的wander
//对应原版Traversal.Instance.wander的匿名Traversal实现
internal sealed class WanderTraversal<A2, B2, S, T, A, B> : Traversal<S, T, A2, B2>
{
    private readonly Wander<S, T, A, B> _wander;
    private readonly Traversal<A, B, A2, B2> _inner;
    internal WanderTraversal(Wander<S, T, A, B> wander, Traversal<A, B, A2, B2> inner)
    {
        _wander = wander;
        _inner = inner;
    }
    public Func<S, App<F, T>> Wander<F, TMu2>(Applicative<F, TMu2> applicative, Func<A2, App<F, B2>> function) where F : K1 where TMu2 : IApplicativeMu
    {
        //_inner运行时是Traversal<具体A,B,A2,B2>被Unsafe.As cast为Traversal<A,B,A2,B2>
        //C#严格泛型不变量下两封闭类型方法表入口不共享直接调Wander抛EntryPointNotFoundException
        //用WanderInvokerCache.GetWanderFunc反射委托缓存调用Wander对齐Java类型擦除后虚方法分派语义
        var innerWanderFuncObj = WanderInvokerCache.GetWanderFunc<A2, B2, F, TMu2>((object)_inner!, (object)applicative!, function);
        //GetWanderFunc返回object实际是Func<A,App<F,B>>用Unsafe.As转回强类型对齐Java类型擦除语义
        var innerWanderFunc = System.Runtime.CompilerServices.Unsafe.As<object, Func<A, App<F, B>>>(ref innerWanderFuncObj!);
        //_wander运行时是Wander<具体S,T,A,B>接口实例直接调Wander抛EntryPointNotFoundException
        //用GetWanderFuncForWander委托缓存绕过方法表入口检查对齐Java类型擦除后虚方法分派语义
        return WanderInvokerCache.GetWanderFuncForWander<S, T, F, TMu2, A, B>((object)_wander!, (object)applicative!, innerWanderFunc);
    }
}
