namespace NetCraft.DataFixer;

using System;
using System.Collections.Generic;
using System.Linq;
using NetCraft.Codec;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics;
using NetCraft.DataFixer.Optics.Profunctors;
using NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Templates;
using NetCraft.DataFixer.Util;
using OpticsClass = NetCraft.DataFixer.Optics.Optics;

//TypedOptic带类型信息的Optic对应原版com.mojang.datafixers.TypedOptic
//记录S->T->A->B四元类型与边界proof bounds
//静态工厂方法已迁移到TypedOptics非泛型类避免泛型类静态方法调用需带类型参数
public sealed record TypedOptic<S, T, A, B>(HashSet<object> Bounds, List<object> Elements)
{
    public TypedOptic(object proofBound, Type<S> sType, Type<T> tType, Type<A> aType, Type<B> bType, object optic)
        : this(new HashSet<object> { proofBound }, new List<object> { new Element<S, T, A, B>(sType, tType, aType, bType, optic) })
    {
    }

    public TypedOptic(IEnumerable<object> proofBounds, Type<S> sType, Type<T> tType, Type<A> aType, Type<B> bType, object optic)
        : this(new HashSet<object>(proofBounds), new List<object> { new Element<S, T, A, B>(sType, tType, aType, bType, optic) })
    {
    }

    //SType最外层源类型Unsafe.As绕过泛型不变量对齐Java类型擦除
    public Type<S> SType() => AsElement<S, T, object, object>(Elements[0]).SType;
    //TType最外层目标类型
    public Type<T> TType() => AsElement<S, T, object, object>(Elements[0]).TType;
    //AType最内层焦点源类型
    public Type<A> AType() => AsElement<object, object, A, B>(Elements[^1]).AType;
    //BType最内层焦点目标类型
    public Type<B> BType() => AsElement<object, object, A, B>(Elements[^1]).BType;

    //AsElement用Unsafe.As绕过泛型不变量对齐Java类型擦除单例共享
    private static Element<ES, ET, EA, EB> AsElement<ES, ET, EA, EB>(object element)
        => System.Runtime.CompilerServices.Unsafe.As<object, Element<ES, ET, EA, EB>>(ref element);

    //compose合并外层与内层optic bounds与elements拼接
    public TypedOptic<S, T, A1, B1> Compose<A1, B1>(TypedOptic<A, B, A1, B1> other)
    {
        var bounds = new HashSet<object>(Bounds);
        bounds.UnionWith(other.Bounds);
        var elements = new List<object>(Elements);
        elements.AddRange(other.Elements);
        return new TypedOptic<S, T, A1, B1>(bounds, elements);
    }

    //upCast检查bounds包含proof后单元素直接返回多元素组合为CompositionOptic
    public Optional<object> UpCast(object proof)
    {
        if (TypedOptics.InstanceOf(Bounds, proof))
        {
            if (Elements.Count == 1)
            {
                return Optional<object>.Of(((Element<S, T, A, B>)Elements[0]).Optic);
            }
            var optics = Elements.Select(e =>
            {
                var elemObj = (object)e;
                var elemCast = System.Runtime.CompilerServices.Unsafe.As<object, Element<object, object, object, object>>(ref elemObj);
                return elemCast.Optic;
            }).ToList();
            //多元素组合用CompositionOptic实现Eval对齐原版Optics.CompositionOptic
            //Proof固定IProfunctorMu满足K1约束调用方反射调Eval不检查Proof具体类型
            var compositionOptic = new CompositionOptic<IProfunctorMu, S, T, A, B>(optics);
            return Optional<object>.Of(compositionOptic);
        }
        return Optional<object>.Empty();
    }

    //outermost返回最外层Element的Optic对应原版outermost
    //供Optics.IsProj1/2/IsInj1/2判断最外层类型
    //用Unsafe.As绕过Element<ES,ET,EA,EB>严格泛型强转对齐Java类型擦除
    public object Outermost()
    {
        var elemObj = (object?)Elements[0];
        var elemCast = System.Runtime.CompilerServices.Unsafe.As<object, Element<object, object, object, object>>(ref elemObj!);
        return elemCast.Optic;
    }

    //castOuter改外层类型不检查用AsElement绕过泛型不变量
    public TypedOptic<S2, T2, A, B> CastOuterUnchecked<S2, T2>(Type<S2> sType, Type<T2> tType)
    {
        var newElements = new List<object>(Elements);
        newElements[0] = AsElement<S, T, A, B>(newElements[0]).CastOuterUnchecked(sType, tType);
        return new TypedOptic<S2, T2, A, B>(Bounds, newElements);
    }

    //castOuter改外层类型不检查
    public TypedOptic<S, T, A, B> CastOuter(Type<S> sType, Type<T> tType)
        => CastOuterUnchecked(sType, tType);

    //CastOuterUncheckedObject非泛型版用Unsafe.As绕过编译期类型检查
    //供PointFreeRule.SortProj等反射调用对齐Java类型擦除语义
    public TypedOptic<object, object, A, B> CastOuterUncheckedObject(object sType, object tType)
    {
        var sObj = sType;
        var tObj = tType;
        var sCast = System.Runtime.CompilerServices.Unsafe.As<object, Type<object>>(ref sObj);
        var tCast = System.Runtime.CompilerServices.Unsafe.As<object, Type<object>>(ref tObj);
        return CastOuterUnchecked(sCast, tCast);
    }

    //apply应用profunctor证明到input返回结果对应原版TypedOptic.apply
    //内部调Optic.Eval或链式组合多个Element的optic
    //C#用反射编译为委托缓存对齐Java类型擦除后语义避免每次反射Invoke开销
    public App2<P, S, T> Apply<P>(object proofInstance, App2<P, A, B> input) where P : K2
    {
        if (Elements.Count == 1)
        {
            var optic = ((Element<S, T, A, B>)Elements[0]).Optic;
            var func = InvokeEval<P>(optic, proofInstance);
            return ((System.Func<App2<P, A, B>, App2<P, S, T>>)(object)func).Invoke(input);
        }
        //多元素从右到左链式应用每个Element的optic
        object current = input;
        for (int i = Elements.Count - 1; i >= 0; i--)
        {
            var optic = ((Element<object, object, object, object>)Elements[i]).Optic;
            current = InvokeEvalChain<P>(optic, proofInstance, current);
        }
        return (App2<P, S, T>)(object)current;
    }

    //InvokeEval用表达式树编译委托缓存按(opticType, pType)键查表避免每次反射Invoke
    //对应原版Java类型擦除后直接虚方法分派C#用表达式树模拟
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(Type opticType, Type pType), System.Func<object, object, object>> _evalCache = new();

    private static object InvokeEval<P>(object optic, object proofInstance) where P : K2
    {
        var opticType = optic.GetType();
        var pType = typeof(P);
        var func = _evalCache.GetOrAdd((opticType, pType), key =>
        {
            var method = key.opticType.GetMethod("Eval")!.MakeGenericMethod(key.pType);
            var opticParam = System.Linq.Expressions.Expression.Parameter(typeof(object), "optic");
            var proofParam = System.Linq.Expressions.Expression.Parameter(typeof(object), "proof");
            var call = System.Linq.Expressions.Expression.Call(
                System.Linq.Expressions.Expression.Convert(opticParam, key.opticType),
                method,
                System.Linq.Expressions.Expression.Convert(proofParam, typeof(object)));
            return System.Linq.Expressions.Expression.Lambda<System.Func<object, object, object>>(call, opticParam, proofParam).Compile();
        });
        return func(optic, proofInstance!);
    }

    //InvokeEvalChain链式中间步骤类型擦除为object复用_evalCache
    private static object InvokeEvalChain<P>(object optic, object proofInstance, object input) where P : K2
    {
        var func = InvokeEval<P>(optic, proofInstance);
        return ((System.Func<object, object>)(object)func!).Invoke(input);
    }

    //Element单个Optic元素记录四元类型与具体Optic
    public sealed record Element<ES, ET, EA, EB>(Type<ES> SType, Type<ET> TType, Type<EA> AType, Type<EB> BType, object Optic)
    {
        public Element<ES2, ET2, EA, EB> CastOuterUnchecked<ES2, ET2>(Type<ES2> sType, Type<ET2> tType)
            => new(sType, tType, AType, BType, Optic);
    }
}

//CompositionOpticAdapter多optic组合适配器暂存optics列表阶段B接通真实组合
internal sealed class CompositionOpticAdapter<S, T, A, B>
{
    private readonly List<object> _optics;
    public CompositionOpticAdapter(List<object> optics) => _optics = optics;
    public List<object> Optics => _optics;
}
