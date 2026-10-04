namespace NetCraft.DataFixer.Optics;

using System;
using System.Collections.Generic;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics.Profunctors;
using NetCraft.DataFixer.Util;

//Optics工具类对应原版com.mojang.datafixers.optics.Optics
//提供各optic的工厂方法与转换方法
public static class Optics
{
    //adapter工厂from/to直接转换
    public static Adapter<S, T, A, B> Adapter<S, T, A, B>(Func<S, A> from, Func<B, T> to)
        => new AdapterImpl<S, T, A, B>(from, to);

    //lens工厂view取焦点update替换焦点
    public static Lens<S, T, A, B> Lens<S, T, A, B>(Func<S, A> view, Func<B, S, T> update)
        => new LensImpl<S, T, A, B>(view, update);

    //prism工厂match尝试分解build从B构造T
    public static Prism<S, T, A, B> Prism<S, T, A, B>(Func<S, Either<T, A>> match, Func<B, T> build)
        => new PrismImpl<S, T, A, B>(match, build);

    //affine工厂preview尝试取焦点set替换焦点
    public static Affine<S, T, A, B> Affine<S, T, A, B>(Func<S, Either<T, A>> preview, Func<B, S, T> set)
        => new AffineImpl<S, T, A, B>(preview, set);

    //getter工厂get取值
    public static Getter<S, T, A, B> Getter<S, T, A, B>(Func<S, A> get)
        => new GetterImpl<S, T, A, B>(get);

    //grate工厂grate函数接收(Func<S,A>)->B的函数返回T
    public static Grate<S, T, A, B> Grate<S, T, A, B>(Func<Func<Func<S, A>, B>, T> grate)
        => new GrateImpl<S, T, A, B>(grate);

    //forget工厂A->R的求值器
    public static Forget<R, A, B> Forget<R, A, B>(Func<A, R> function)
        => new ForgetImpl<R, A, B>(function);

    //forgetOpt工厂A->Optional<R>的求值器
    public static ForgetOpt<R, A, B> ForgetOpt<R, A, B>(Func<A, NetCraft.Codec.Optional<R>> function)
        => new ForgetOptImpl<R, A, B>(function);

    //forgetE工厂A->Either<B,R>的求值器
    public static ForgetE<R, A, B> ForgetE<R, A, B>(Func<A, Either<B, R>> function)
        => new ForgetEImpl<R, A, B>(function);

    //reForget工厂R->B的求值器
    public static ReForget<R, A, B> ReForget<R, A, B>(Func<R, B> function)
        => new ReForgetImpl<R, A, B>(function);

    //reForgetE工厂Either<A,R>->B的求值器
    public static ReForgetE<R, A, B> ReForgetE<R, A, B>(string name, Func<Either<A, R>, B> function)
        => new ReForgetEImpl<R, A, B>(name, function);

    //reForgetEP工厂Either<A,Pair<A,R>>->B的求值器
    public static ReForgetEP<R, A, B> ReForgetEP<R, A, B>(string name, Func<Either<A, Pair<A, R>>, B> function)
        => new ReForgetEPImpl<R, A, B>(name, function);

    //reForgetP工厂(A,R)->B的求值器
    public static ReForgetP<R, A, B> ReForgetP<R, A, B>(string name, Func<A, R, B> function)
        => new ReForgetPImpl<R, A, B>(name, function);

    //reForgetC工厂Either<Func<R,B>,Func<A,R,B>>的实现
    public static ReForgetC<R, A, B> ReForgetC<R, A, B>(string name, Either<Func<R, B>, Func<A, R, B>> impl)
        => new ReForgetCImpl<R, A, B>(name, impl);

    //pStore工厂peek函数和pos函数
    public static PStore<I, J, X> PStore<I, J, X>(Func<J, X> peek, Func<I> pos)
        => new PStoreImpl<I, J, X>(peek, pos);

    //getFunc还原FunctionType为Func
    public static Func<A, B> GetFunc<A, B>(App2<FunctionTypes.Mu, A, B> box)
        => FunctionType<A, B>.GetFunc(box);

    //merge把两个Lens合并为一个Lens组合view得到Pair
    //update只用lens.Update因为getter只读
    public static Lens<S, T, Pair<F, A>, B> Merge<S, T, A, B, F>(Lens<S, object, F, object> getter, Lens<S, T, A, B> lens)
        => Lens<S, T, Pair<F, A>, B>(
            s => Pair<F, A>.Of(getter.View(s), lens.View(s)),
            lens.Update
        );

    //id恒等适配器复用IdAdapter单例Unsafe.As绕过泛型不变量检查对齐Java类型擦除
    public static Adapter<S, T, S, T> Id<S, T>() => UnsafeCast<Adapter<S, T, S, T>>(IdAdapter<object, object>.Instance);

    //isId检查optic是否为IdAdapter单例
    public static bool IsId(object optic)
        => ReferenceEquals(optic, IdAdapter<object, object>.Instance);

    //proj1返回Proj1单例Unsafe.As绕过泛型不变量检查
    //全限定类名避免与Optics.Proj1方法组冲突CS0119
    public static Proj1<F, G, F2> Proj1<F, G, F2>() => UnsafeCast<Proj1<F, G, F2>>(global::NetCraft.DataFixer.Optics.Proj1<object, object, object>.Instance);
    //isProj1检查optic是否为Proj1单例
    public static bool IsProj1(object optic) => ReferenceEquals(optic, global::NetCraft.DataFixer.Optics.Proj1<object, object, object>.Instance);

    //proj2返回Proj2单例Unsafe.As绕过泛型不变量检查
    public static Proj2<F, G, G2> Proj2<F, G, G2>() => UnsafeCast<Proj2<F, G, G2>>(global::NetCraft.DataFixer.Optics.Proj2<object, object, object>.Instance);
    //isProj2检查optic是否为Proj2单例
    public static bool IsProj2(object optic) => ReferenceEquals(optic, global::NetCraft.DataFixer.Optics.Proj2<object, object, object>.Instance);

    //inj1返回Inj1单例Unsafe.As绕过泛型不变量检查
    public static Inj1<F, G, F2> Inj1<F, G, F2>() => UnsafeCast<Inj1<F, G, F2>>(global::NetCraft.DataFixer.Optics.Inj1<object, object, object>.Instance);
    //isInj1检查optic是否为Inj1单例
    public static bool IsInj1(object optic) => ReferenceEquals(optic, global::NetCraft.DataFixer.Optics.Inj1<object, object, object>.Instance);

    //inj2返回Inj2单例Unsafe.As绕过泛型不变量检查
    public static Inj2<F, G, G2> Inj2<F, G, G2>() => UnsafeCast<Inj2<F, G, G2>>(global::NetCraft.DataFixer.Optics.Inj2<object, object, object>.Instance);
    //isInj2检查optic是否为Inj2单例
    public static bool IsInj2(object optic) => ReferenceEquals(optic, global::NetCraft.DataFixer.Optics.Inj2<object, object, object>.Instance);

    //listTraversal返回ListTraversal单例Unsafe.As绕过泛型不变量检查
    public static ListTraversal<A, B> ListTraversal<A, B>() => UnsafeCast<ListTraversal<A, B>>(global::NetCraft.DataFixer.Optics.ListTraversal<object, object>.Instance);

    //UnsafeCast用Unsafe.As绕过C#引用类型泛型不变量检查对齐Java类型擦除单例共享
    private static T UnsafeCast<T>(object instance)
        => System.Runtime.CompilerServices.Unsafe.As<object, T>(ref instance);

    //toAdapter把optic转为Adapter用AdapterInstance作为Profunctor证明
    public static Adapter<S, T, A, B> ToAdapter<S, T, A, B>(Optic<IProfunctorMu, S, T, A, B> optic)
    {
        var instance = new AdapterInstance<A, B>();
        var func = optic.Eval<Adapters.Mu<A, B>>(instance);
        return Adapters.Unbox<S, T, A, B>(func.Invoke(Adapter<A, B, A, B>(x => x, x => x)));
    }

    //toLens把optic转为Lens用LensInstance作为Cartesian证明
    public static Lens<S, T, A, B> ToLens<S, T, A, B>(Optic<ICartesianMu, S, T, A, B> optic)
    {
        var instance = new LensInstance<A, B>();
        var func = optic.Eval<Lenses.Mu<A, B>>(instance);
        return Lenses.Unbox<S, T, A, B>(func.Invoke(Lens<A, B, A, B>(x => x, (b, s) => b)));
    }

    //toPrism把optic转为Prism用PrismInstance作为Cocartesian证明
    public static Prism<S, T, A, B> ToPrism<S, T, A, B>(Optic<ICocartesianMu, S, T, A, B> optic)
    {
        var instance = new PrismInstance<A, B>();
        var func = optic.Eval<Prisms.Mu<A, B>>(instance);
        return Prisms.Unbox<S, T, A, B>(func.Invoke(Prism<A, B, A, B>(a => Either<B, A>.Right(a), b => b)));
    }

    //toAffine把optic转为Affine用AffineInstance作为AffineP证明
    public static Affine<S, T, A, B> ToAffine<S, T, A, B>(Optic<IAffinePMu, S, T, A, B> optic)
    {
        var instance = new AffineInstance<A, B>();
        var func = optic.Eval<Affines.Mu<A, B>>(instance);
        return Affines.Unbox<S, T, A, B>(func.Invoke(Affine<A, B, A, B>(a => Either<B, A>.Right(a), (b, s) => b)));
    }

    //toGetter把optic转为Getter用GetterInstance作为GetterP证明
    public static Getter<S, T, A, B> ToGetter<S, T, A, B>(Optic<IGetterPMu, S, T, A, B> optic)
    {
        var instance = new GetterInstance<A, B>();
        var func = optic.Eval<Getters.Mu<A, B>>(instance);
        return Getters.Unbox<S, T, A, B>(func.Invoke(Getter<A, B, A, B>(x => x)));
    }

    //toTraversal把optic转为Traversal用TraversalInstance作为TraversalP证明
    //TraversalInstance延后实现toTraversal会抛NotSupportedException
    //optic运行时是Proj2/InjTagged等具体Optic<其他Proof>虚方法槽位与Optic<ITraversalPMu,...>不匹配
    //直接调optic.Eval抛EntryPointNotFoundException改用EvalCacheHelper.InvokeEval反射+表达式树编译委托
    //委托invoke参数严格类型检查下IdAdapter A/B=object与期望A/B=具体类型不匹配
    //用FuncInvokerHelper缓存反射Invoke绕过委托invoke类型检查对齐Java类型擦除
    public static Traversal<S, T, A, B> ToTraversal<S, T, A, B>(Optic<ITraversalPMu, S, T, A, B> optic)
    {
        var instance = TraversalInstance<A, B>.InstanceOf;
        var funcObj = EvalCacheHelper.InvokeEval<Traversals.Mu<A, B>>((object)optic!, (object)instance!);
        var identity = new IdentityTraversal<A, B>();
        var result = FuncInvokerHelper.Invoke(funcObj!, identity);
        //result是object实际是App2<Mu<A,B>,S,T>用Unsafe.As绕过运行时类型检查对齐Java类型擦除
        var resultApp = System.Runtime.CompilerServices.Unsafe.As<object, App2<Traversals.Mu<A, B>, S, T>>(ref result!);
        return Traversals.Unbox<S, T, A, B>(resultApp);
    }

    //eitherLens把两个Lens合并为Either上的Lens分支处理左右值
    public static Lens<Either<F, G>, Either<F2, G2>, A, B> EitherLens<F, G, F2, G2, A, B>(Lens<F, F2, A, B> fLens, Lens<G, G2, A, B> gLens)
        => Lens<Either<F, G>, Either<F2, G2>, A, B>(
            either => either.Map(f => fLens.View(f), g => gLens.View(g)),
            (b, either) => either.MapBoth(f => fLens.Update(b, f), g => gLens.Update(b, g))
        );

    //eitherAffine把两个Affine合并为Either上的Affine分支处理左右值
    public static Affine<Either<F, G>, Either<F2, G2>, A, B> EitherAffine<F, G, F2, G2, A, B>(Affine<F, F2, A, B> fAffine, Affine<G, G2, A, B> gAffine)
        => Affine<Either<F, G>, Either<F2, G2>, A, B>(
            either => either.Map(
                f => fAffine.Preview(f).MapLeft(Either<F2, G2>.Left),
                g => gAffine.Preview(g).MapLeft(Either<F2, G2>.Right)
            ),
            (b, either) => either.MapBoth(f => fAffine.Set(b, f), g => gAffine.Set(b, g))
        );

    //eitherTraversal把两个Traversal合并为Either上的Traversal分支处理左右值
    public static Traversal<Either<F, G>, Either<F2, G2>, A, B> EitherTraversal<F, G, F2, G2, A, B>(Traversal<F, F2, A, B> fOptic, Traversal<G, G2, A, B> gOptic)
        => new EitherTraversalImpl<F, G, F2, G2, A, B>(fOptic, gOptic);
}

//IdentityTraversal恒等Traversal用作ToTraversal的种子wander直接返回原函数
internal sealed class IdentityTraversal<A, B> : Traversal<A, B, A, B>
{
    public Func<A, App<F, B>> Wander<F, TMu2>(Applicative<F, TMu2> applicative, Func<A, App<F, B>> input) where F : K1 where TMu2 : IApplicativeMu
        => input;
}

//EitherTraversal合并两个Traversal到Either分支wander按分支委托
internal sealed class EitherTraversalImpl<F, G, F2, G2, A, B> : Traversal<Either<F, G>, Either<F2, G2>, A, B>
{
    private readonly Traversal<F, F2, A, B> _fOptic;
    private readonly Traversal<G, G2, A, B> _gOptic;
    internal EitherTraversalImpl(Traversal<F, F2, A, B> fOptic, Traversal<G, G2, A, B> gOptic)
    {
        _fOptic = fOptic;
        _gOptic = gOptic;
    }

    public Func<Either<F, G>, App<FT, Either<F2, G2>>> Wander<FT, TMu2>(Applicative<FT, TMu2> applicative, Func<A, App<FT, B>> input) where FT : K1 where TMu2 : IApplicativeMu
        => e => e.Map(
            l => applicative.Ap(Either<F2, G2>.Left, _fOptic.Wander(applicative, input).Invoke(l)),
            r => applicative.Ap(Either<F2, G2>.Right, _gOptic.Wander(applicative, input).Invoke(r))
        );
}
