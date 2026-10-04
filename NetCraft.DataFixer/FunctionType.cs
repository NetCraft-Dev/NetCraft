namespace NetCraft.DataFixer;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics;
using NetCraft.DataFixer.Optics.Profunctors;
using NetCraft.DataFixer.Util;

//FunctionTypes容器存放Mu与ReaderMu标记避免泛型嵌套
public static class FunctionTypes
{
    //二元HKT标记函数类型构造器
    public sealed class Mu : K2 { }

    //一元Reader HKT标记R为环境类型
    public sealed class ReaderMu<R> : K1 { }
}

//函数类型对应原版com.mojang.datafixers.FunctionType
//把Func<A,B>包装为HKT使其可作为profunctor处理
public interface FunctionType<A, B> : App2<FunctionTypes.Mu, A, B>, App<FunctionTypes.ReaderMu<A>, B>
{
    //应用函数返回B
    B Apply(A a);

    //还原二元类型应用为FunctionType
    //用Unsafe.As绕过运行时类型检查对齐Java类型擦除
    //实际实例可能是FunctionTypeImpl<object,object>强转为FunctionType<A2,B2>需绕过C#严格泛型不变量
    static FunctionType<A2, B2> Unbox<A2, B2>(App2<FunctionTypes.Mu, A2, B2> box)
    {
        var boxObj = (object)box!;
        return System.Runtime.CompilerServices.Unsafe.As<object, FunctionType<A2, B2>>(ref boxObj);
    }

    //还原一元Reader类型应用为FunctionType
    //用Unsafe.As绕过运行时类型检查对齐Java类型擦除
    static FunctionType<A2, B2> UnboxReader<A2, B2>(App<FunctionTypes.ReaderMu<A2>, B2> box)
    {
        var boxObj = (object)box!;
        return System.Runtime.CompilerServices.Unsafe.As<object, FunctionType<A2, B2>>(ref boxObj);
    }

    //工厂方法从Func构造FunctionType
    static FunctionType<A, B> Create(Func<A, B> function) => new FunctionTypeImpl<A, B>(function);

    //还原为Func
    //box运行时可能是FunctionTypeImpl<X,Y>但编译期声明是App2<Mu,A,B>
    //X/Y与A/B因Java类型擦除语义不同但运行时同一实例
    //直接Unbox(box).Apply会因CLR接口分派按实例类型FunctionTypeImpl<X,Y>查找
    //FunctionType<A,B>.Apply入口失败抛EntryPointNotFoundException
    //委托variance也不允许Func<X,Y>强转Func<A,B>因参数逆变严格检查
    //用表达式树编译调用委托按funcType缓存对齐Java类型擦除
    static Func<A, B> GetFunc<A, B>(App2<FunctionTypes.Mu, A, B> box)
    {
        var boxObj = (object)box!;
        var boxType = boxObj.GetType();
        if (boxType.IsGenericType && boxType.GetGenericTypeDefinition() == typeof(FunctionTypeImpl<,>))
        {
            var fieldInfo = FunctionTypeImplFieldCache.GetField(boxType);
            var funcObj = fieldInfo.GetValue(boxObj)!;
            var invoker = FunctionTypeInvokerCache.GetInvoker(funcObj.GetType());
            //invoker返回funcObj实际返回类型Pair<string,object>等与B=Pair<object,object>不一致
            //C#严格泛型不变量下Pair<string,object>不能强转Pair<object,object>
            //用Unsafe.As绕过运行时类型检查对齐Java类型擦除语义
            return a =>
            {
                var result = invoker(funcObj, a!);
                var obj = (object)result;
                return System.Runtime.CompilerServices.Unsafe.As<object, B>(ref obj);
            };
        }
        return Unbox(box).Apply;
    }
}

//FunctionTypeInvoker按funcType缓存编译后的调用委托
//表达式树把Func<X,Y>.Invoke(object,object)->object编译为强类型委托
//argParam到目标参数类型转换用CastTo<T>包装Unsafe.As绕过castclass运行时检查
//否则Pair<string,object>无法castclass到Pair<object,object>对齐C#严格泛型不变量
internal static class FunctionTypeInvokerCache
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, System.Func<object, object, object>> _cache = new();

    public static System.Func<object, object, object> GetInvoker(Type funcType)
        => _cache.GetOrAdd(funcType, t =>
        {
            var invokeMethod = t.GetMethod("Invoke")!;
            var paramType = invokeMethod.GetParameters()[0].ParameterType;
            var funcParam = System.Linq.Expressions.Expression.Parameter(typeof(object), "func");
            var argParam = System.Linq.Expressions.Expression.Parameter(typeof(object), "arg");
            //CastTo<object,Pair<string,object>>(arg)用Unsafe.As绕过castclass
            var castMethod = typeof(FunctionTypeInvokerCache)
                .GetMethod(nameof(CastTo), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(paramType);
            var castArg = System.Linq.Expressions.Expression.Call(castMethod, argParam);
            var call = System.Linq.Expressions.Expression.Call(
                System.Linq.Expressions.Expression.Convert(funcParam, t),
                invokeMethod,
                castArg);
            return System.Linq.Expressions.Expression.Lambda<System.Func<object, object, object>>(
                System.Linq.Expressions.Expression.Convert(call, typeof(object)),
                funcParam, argParam).Compile();
        });

    //CastTo用Unsafe.As绕过C#严格泛型不变量让object转任意类型T
    //去掉class约束让Pair<object,object>等struct类型参数也能MakeGenericMethod
    //对齐Java类型擦除语义避免castclass运行时检查失败
    private static T CastTo<T>(object obj)
    {
        var local = obj;
        return System.Runtime.CompilerServices.Unsafe.As<object, T>(ref local);
    }
}

//FunctionTypeImpl字段反射缓存避免每次GetFunc反射查找
//按运行时类型缓存_functionFieldInfo按boxType查表
internal static class FunctionTypeImplFieldCache
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, System.Reflection.FieldInfo> _cache = new();

    public static System.Reflection.FieldInfo GetField(Type implType)
        => _cache.GetOrAdd(implType, t => t.GetField("_function", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!);
}

//FunctionType具体实现持有Func委托
internal sealed class FunctionTypeImpl<A, B> : FunctionType<A, B>
{
    private readonly Func<A, B> _function;
    internal FunctionTypeImpl(Func<A, B> function) => _function = function;
    public B Apply(A a) => _function(a);
}

//FunctionTypeInstance作为TraversalP+Monoidal+Mapping+MonoidProfunctor的实例
//所有方法基于Func组合实现
//额外实现App<IProfunctorMu,FunctionTypes.Mu>对齐Java类型擦除语义
//ProfunctorTransformer.Eval反射调用IdAdapter.Eval时proof参数类型App<Proof,P>
//C#严格泛型不变量下App<FunctionTypeInstance.Mu,P>与App<IProfunctorMu,P>不同封闭类型
//Java类型擦除下Proof被擦除运行时等同App<Object,Object>任意App实例均可
//加App<IProfunctorMu,P>接口实现让反射类型检查通过对齐Java虚方法分派语义
//额外实现Profunctor<FunctionTypes.Mu,IProfunctorMu>接口让Profunctor.Unbox返回的引用
//能虚方法分派到Dimap对应Profunctor<FunctionTypes.Mu,IProfunctorMu>方法表入口
//额外实现Cartesian<FunctionTypes.Mu,ICartesianMu>接口让Cartesian.Unbox返回的引用
//通过Cartesian<FunctionTypes.Mu,ICartesianMu>方法表调用Dimap命中入口对齐Java类型擦除
public sealed class FunctionTypeInstance :
    TraversalP<FunctionTypes.Mu, FunctionTypeInstance.Mu>,
    Monoidal<FunctionTypes.Mu, FunctionTypeInstance.Mu>,
    Mapping<FunctionTypes.Mu, FunctionTypeInstance.Mu>,
    MonoidProfunctor<FunctionTypes.Mu, FunctionTypeInstance.Mu>,
    App<FunctionTypeInstance.Mu, FunctionTypes.Mu>,
    App<IProfunctorMu, FunctionTypes.Mu>,
    NetCraft.DataFixer.Optics.Profunctors.Profunctor<FunctionTypes.Mu, IProfunctorMu>,
    NetCraft.DataFixer.Optics.Profunctors.Cartesian<FunctionTypes.Mu, ICartesianMu>,
    NetCraft.DataFixer.Optics.Profunctors.Cocartesian<FunctionTypes.Mu, ICocartesianMu>,
    NetCraft.DataFixer.Optics.Profunctors.Profunctor<FunctionTypes.Mu, ICartesianMu>,
    NetCraft.DataFixer.Optics.Profunctors.Profunctor<FunctionTypes.Mu, ICocartesianMu>,
    //额外实现ITraversalPMu变体让Traversal.Eval调用traversalP.Wander命中方法表入口
    //TraversalP.Unbox用Unsafe.As绕过TMu检查后调Wander需要变体接口方法表存在
    NetCraft.DataFixer.Optics.Profunctors.TraversalP<FunctionTypes.Mu, ITraversalPMu>,
    NetCraft.DataFixer.Optics.Profunctors.AffineP<FunctionTypes.Mu, IAffinePMu>,
    NetCraft.DataFixer.Optics.Profunctors.Cartesian<FunctionTypes.Mu, ITraversalPMu>,
    NetCraft.DataFixer.Optics.Profunctors.Cocartesian<FunctionTypes.Mu, ITraversalPMu>,
    NetCraft.DataFixer.Optics.Profunctors.Profunctor<FunctionTypes.Mu, ITraversalPMu>,
    App<ITraversalPMu, FunctionTypes.Mu>,
    App<IAffinePMu, FunctionTypes.Mu>
{
    public sealed class Mu : ITraversalPMu, IMonoidalMu, IMappingMu, IMonoidProfunctorMu { }
    public static readonly FunctionTypeInstance InstanceOf = new();
    private FunctionTypeInstance() { }

    //dimap用g前处理输入h后处理输出组合原函数
    public Func<App2<FunctionTypes.Mu, A, B>, App2<FunctionTypes.Mu, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => f => FunctionType<C, D>.Create(a => h(FunctionType<A, B>.GetFunc(f)(g(a))));

    //显式实现Profunctor<FunctionTypes.Mu,IProfunctorMu>.Dimap委托到原Dimap
    //让通过Profunctor<P,IProfunctorMu>引用调用Dimap能命中方法表入口
    Func<App2<FunctionTypes.Mu, A, B>, App2<FunctionTypes.Mu, C, D>> NetCraft.DataFixer.Optics.Profunctors.Profunctor<FunctionTypes.Mu, IProfunctorMu>.Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => Dimap<A, B, C, D>(g, h);

    //显式实现Profunctor<FunctionTypes.Mu,ICartesianMu>.Dimap让Cartesian.Unbox返回的引用
    //通过Cartesian<FunctionTypes.Mu,ICartesianMu>方法表调用Dimap命中入口对齐Java类型擦除
    Func<App2<FunctionTypes.Mu, A, B>, App2<FunctionTypes.Mu, C, D>> NetCraft.DataFixer.Optics.Profunctors.Profunctor<FunctionTypes.Mu, ICartesianMu>.Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => Dimap<A, B, C, D>(g, h);

    //显式实现Profunctor<FunctionTypes.Mu,ICocartesianMu>.Dimap
    Func<App2<FunctionTypes.Mu, A, B>, App2<FunctionTypes.Mu, C, D>> NetCraft.DataFixer.Optics.Profunctors.Profunctor<FunctionTypes.Mu, ICocartesianMu>.Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => Dimap<A, B, C, D>(g, h);

    //显式实现Cartesian<FunctionTypes.Mu,ICartesianMu>.First让Lens.Eval里cartesian.First命中入口
    App2<FunctionTypes.Mu, Pair<A, C>, Pair<B, C>> NetCraft.DataFixer.Optics.Profunctors.Cartesian<FunctionTypes.Mu, ICartesianMu>.First<A, B, C>(App2<FunctionTypes.Mu, A, B> input)
        => First<A, B, C>(input);

    //显式实现Cartesian<FunctionTypes.Mu,ICartesianMu>.Second让Cartesian.Unbox引用调用Second命中入口
    App2<FunctionTypes.Mu, Pair<C, A>, Pair<C, B>> NetCraft.DataFixer.Optics.Profunctors.Cartesian<FunctionTypes.Mu, ICartesianMu>.Second<A, B, C>(App2<FunctionTypes.Mu, A, B> input)
        => Second<A, B, C>(input);

    //显式实现Cocartesian<FunctionTypes.Mu,ICocartesianMu>.Left让Prism.Eval里cocartesian.Left命中入口
    App2<FunctionTypes.Mu, Either<A, C>, Either<B, C>> NetCraft.DataFixer.Optics.Profunctors.Cocartesian<FunctionTypes.Mu, ICocartesianMu>.Left<A, B, C>(App2<FunctionTypes.Mu, A, B> input)
        => Left<A, B, C>(input);

    //显式实现Cocartesian<FunctionTypes.Mu,ICocartesianMu>.Right
    App2<FunctionTypes.Mu, Either<C, A>, Either<C, B>> NetCraft.DataFixer.Optics.Profunctors.Cocartesian<FunctionTypes.Mu, ICocartesianMu>.Right<A, B, C>(App2<FunctionTypes.Mu, A, B> input)
        => Right<A, B, C>(input);

    //显式实现TraversalP<FunctionTypes.Mu,ITraversalPMu>.Wander让Traversal.Eval调用traversalP.Wander命中入口
    App2<FunctionTypes.Mu, S, T> NetCraft.DataFixer.Optics.Profunctors.TraversalP<FunctionTypes.Mu, ITraversalPMu>.Wander<S, T, A, B>(Wander<S, T, A, B> wander, App2<FunctionTypes.Mu, A, B> input)
        => Wander<S, T, A, B>(wander, input);

    //显式实现Profunctor<FunctionTypes.Mu,ITraversalPMu>.Dimap委托到原Dimap
    Func<App2<FunctionTypes.Mu, A, B>, App2<FunctionTypes.Mu, C, D>> NetCraft.DataFixer.Optics.Profunctors.Profunctor<FunctionTypes.Mu, ITraversalPMu>.Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => Dimap<A, B, C, D>(g, h);

    //显式实现Cartesian<FunctionTypes.Mu,ITraversalPMu>.First
    App2<FunctionTypes.Mu, Pair<A, C>, Pair<B, C>> NetCraft.DataFixer.Optics.Profunctors.Cartesian<FunctionTypes.Mu, ITraversalPMu>.First<A, B, C>(App2<FunctionTypes.Mu, A, B> input)
        => First<A, B, C>(input);

    //显式实现Cartesian<FunctionTypes.Mu,ITraversalPMu>.Second
    App2<FunctionTypes.Mu, Pair<C, A>, Pair<C, B>> NetCraft.DataFixer.Optics.Profunctors.Cartesian<FunctionTypes.Mu, ITraversalPMu>.Second<A, B, C>(App2<FunctionTypes.Mu, A, B> input)
        => Second<A, B, C>(input);

    //显式实现Cocartesian<FunctionTypes.Mu,ITraversalPMu>.Left
    App2<FunctionTypes.Mu, Either<A, C>, Either<B, C>> NetCraft.DataFixer.Optics.Profunctors.Cocartesian<FunctionTypes.Mu, ITraversalPMu>.Left<A, B, C>(App2<FunctionTypes.Mu, A, B> input)
        => Left<A, B, C>(input);

    //显式实现Cocartesian<FunctionTypes.Mu,ITraversalPMu>.Right
    App2<FunctionTypes.Mu, Either<C, A>, Either<C, B>> NetCraft.DataFixer.Optics.Profunctors.Cocartesian<FunctionTypes.Mu, ITraversalPMu>.Right<A, B, C>(App2<FunctionTypes.Mu, A, B> input)
        => Right<A, B, C>(input);

    //显式实现Profunctor<FunctionTypes.Mu,IAffinePMu>.Dimap
    Func<App2<FunctionTypes.Mu, A, B>, App2<FunctionTypes.Mu, C, D>> NetCraft.DataFixer.Optics.Profunctors.Profunctor<FunctionTypes.Mu, IAffinePMu>.Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => Dimap<A, B, C, D>(g, h);

    //显式实现Cartesian<FunctionTypes.Mu,IAffinePMu>.First
    App2<FunctionTypes.Mu, Pair<A, C>, Pair<B, C>> NetCraft.DataFixer.Optics.Profunctors.Cartesian<FunctionTypes.Mu, IAffinePMu>.First<A, B, C>(App2<FunctionTypes.Mu, A, B> input)
        => First<A, B, C>(input);

    //显式实现Cartesian<FunctionTypes.Mu,IAffinePMu>.Second
    App2<FunctionTypes.Mu, Pair<C, A>, Pair<C, B>> NetCraft.DataFixer.Optics.Profunctors.Cartesian<FunctionTypes.Mu, IAffinePMu>.Second<A, B, C>(App2<FunctionTypes.Mu, A, B> input)
        => Second<A, B, C>(input);

    //显式实现Cocartesian<FunctionTypes.Mu,IAffinePMu>.Left
    App2<FunctionTypes.Mu, Either<A, C>, Either<B, C>> NetCraft.DataFixer.Optics.Profunctors.Cocartesian<FunctionTypes.Mu, IAffinePMu>.Left<A, B, C>(App2<FunctionTypes.Mu, A, B> input)
        => Left<A, B, C>(input);

    //显式实现Cocartesian<FunctionTypes.Mu,IAffinePMu>.Right
    App2<FunctionTypes.Mu, Either<C, A>, Either<C, B>> NetCraft.DataFixer.Optics.Profunctors.Cocartesian<FunctionTypes.Mu, IAffinePMu>.Right<A, B, C>(App2<FunctionTypes.Mu, A, B> input)
        => Right<A, B, C>(input);

    //first把A->B扩展为Pair<A,C>->Pair<B,C>保留C分量
    public App2<FunctionTypes.Mu, Pair<A, C>, Pair<B, C>> First<A, B, C>(App2<FunctionTypes.Mu, A, B> input)
        => FunctionType<Pair<A, C>, Pair<B, C>>.Create(p => Pair<B, C>.Of(FunctionType<A, B>.GetFunc(input)(p.First), p.Second));

    //second把A->B扩展为Pair<C,A>->Pair<C,B>保留C分量
    public new App2<FunctionTypes.Mu, Pair<C, A>, Pair<C, B>> Second<A, B, C>(App2<FunctionTypes.Mu, A, B> input)
        => FunctionType<Pair<C, A>, Pair<C, B>>.Create(p => Pair<C, B>.Of(p.First, FunctionType<A, B>.GetFunc(input)(p.Second)));

    //left把A->B扩展为Either<A,C>->Either<B,C>分支保留C
    public App2<FunctionTypes.Mu, Either<A, C>, Either<B, C>> Left<A, B, C>(App2<FunctionTypes.Mu, A, B> input)
    {
        var func = FunctionType<A, B>.GetFunc(input);
        return FunctionType<Either<A, C>, Either<B, C>>.Create(e => e.MapLeft(func));
    }

    //right把A->B扩展为Either<C,A>->Either<C,B>分支保留C
    public new App2<FunctionTypes.Mu, Either<C, A>, Either<C, B>> Right<A, B, C>(App2<FunctionTypes.Mu, A, B> input)
    {
        var func = FunctionType<A, B>.GetFunc(input);
        return FunctionType<Either<C, A>, Either<C, B>>.Create(e => e.MapRight(func));
    }

    //par并行组合两个函数分别处理Pair的两个分量
    public App2<FunctionTypes.Mu, Pair<A, C>, Pair<B, D>> Par<A, B, C, D>(App2<FunctionTypes.Mu, A, B> first, Func<App2<FunctionTypes.Mu, C, D>> second)
        => FunctionType<Pair<A, C>, Pair<B, D>>.Create(p => Pair<B, D>.Of(FunctionType<A, B>.GetFunc(first)(p.First), FunctionType<C, D>.GetFunc(second())(p.Second)));

    //empty返回Void->Void的identity单位元
    public App2<FunctionTypes.Mu, Unit, Unit> Empty()
        => FunctionType<Unit, Unit>.Create(u => u);

    //wander用IdF作Applicative把A->B扩展为S->T基于Wander策略
    public App2<FunctionTypes.Mu, S, T> Wander<S, T, A, B>(Wander<S, T, A, B> wander, App2<FunctionTypes.Mu, A, B> input)
    {
        var func = FunctionType<A, B>.GetFunc(input);
        return FunctionType<S, T>.Create(s => IdFs.Get(wander.Wander(IdFInstance.InstanceOf, a => IdFs.Create(func(a))).Invoke(s)));
    }

    //mapping用Functor.map把A->B提升到App<F,A>->App<F,B>
    public App2<FunctionTypes.Mu, App<F, A>, App<F, B>> Mapping<A, B, F, TMu2>(Functor<F, TMu2> functor, App2<FunctionTypes.Mu, A, B> input)
        where F : K1 where TMu2 : IFunctorMu
    {
        var func = FunctionType<A, B>.GetFunc(input);
        return FunctionType<App<F, A>, App<F, B>>.Create(fa => functor.Map(func, fa));
    }

    //zero返回func本身单位元
    public App2<FunctionTypes.Mu, A, B> Zero<A, B>(App2<FunctionTypes.Mu, A, B> func) => func;

    //plus用Procompose组合first(A->C)与second(C->B)得A->B
    public App2<FunctionTypes.Mu, A, B> Plus<A, B>(App2<Procomposes.Mu<FunctionTypes.Mu, FunctionTypes.Mu>, A, B> input)
    {
        var cmp = Procompose<FunctionTypes.Mu, FunctionTypes.Mu, A, B, object>.Unbox(input);
        var firstFunc = FunctionType<A, object>.GetFunc(cmp.First().Invoke());
        var secondFunc = FunctionType<object, B>.GetFunc(cmp.Second());
        return FunctionType<A, B>.Create(a => secondFunc(firstFunc(a)));
    }
}
