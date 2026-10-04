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

//TypedOptics非泛型静态工厂类对应原版TypedOptic静态方法
//原TypedOptic<S,T,A,B>泛型record上静态方法C#调用时需带4类型参数无法使用
//迁移到非泛型类按OFAP静态工厂移至非泛型类惯例
public static class TypedOptics
{
    //adapter构造恒等adapter bounds为IProfunctorMu
    public static TypedOptic<S, T, S, T> Adapter<S, T>(Type<S> sType, Type<T> tType)
        => new(typeof(IProfunctorMu), sType, tType, sType, tType, OpticsClass.Id<S, T>());

    //proj1构造Pair第一分量投射 bounds为ICartesianMu
    public static TypedOptic<NetCraft.DataFixer.Util.Pair<F, G>, NetCraft.DataFixer.Util.Pair<F2, G>, F, F2> Proj1<F, G, F2>(
        Type<F> fType, Type<G> gType, Type<F2> newType)
        => new(typeof(ICartesianMu),
            DSL.And(fType, gType),
            DSL.And(newType, gType),
            fType,
            newType,
            OpticsClass.Proj1<F, G, F2>());

    //proj2构造Pair第二分量投射
    public static TypedOptic<NetCraft.DataFixer.Util.Pair<F, G>, NetCraft.DataFixer.Util.Pair<F, G2>, G, G2> Proj2<F, G, G2>(
        Type<F> fType, Type<G> gType, Type<G2> newType)
        => new(typeof(ICartesianMu),
            DSL.And(fType, gType),
            DSL.And(fType, newType),
            gType,
            newType,
            OpticsClass.Proj2<F, G, G2>());

    //inj1构造Either左注入
    public static TypedOptic<Either<F, G>, Either<F2, G>, F, F2> Inj1<F, G, F2>(
        Type<F> fType, Type<G> gType, Type<F2> newType)
        => new(typeof(ICocartesianMu),
            DSL.Or(fType, gType),
            DSL.Or(newType, gType),
            fType,
            newType,
            OpticsClass.Inj1<F, G, F2>());

    //inj2构造Either右注入
    public static TypedOptic<Either<F, G>, Either<F, G2>, G, G2> Inj2<F, G, G2>(
        Type<F> fType, Type<G> gType, Type<G2> newType)
        => new(typeof(ICocartesianMu),
            DSL.Or(fType, gType),
            DSL.Or(fType, newType),
            gType,
            newType,
            OpticsClass.Inj2<F, G, G2>());

    //compoundListKeys构造复合列表key遍历组合listTraversal+proj1
    public static TypedOptic<List<NetCraft.DataFixer.Util.Pair<K, V>>, List<NetCraft.DataFixer.Util.Pair<K2, V>>, K, K2> CompoundListKeys<K, V, K2>(
        Type<K> aType, Type<K2> bType, Type<V> valueType)
        => new TypedOptic<List<NetCraft.DataFixer.Util.Pair<K, V>>, List<NetCraft.DataFixer.Util.Pair<K2, V>>, NetCraft.DataFixer.Util.Pair<K, V>, NetCraft.DataFixer.Util.Pair<K2, V>>(
            typeof(ITraversalPMu),
            DSL.CompoundList(aType, valueType),
            DSL.CompoundList(bType, valueType),
            DSL.And(aType, valueType),
            DSL.And(bType, valueType),
            OpticsClass.ListTraversal<NetCraft.DataFixer.Util.Pair<K, V>, NetCraft.DataFixer.Util.Pair<K2, V>>())
            .Compose(new TypedOptic<NetCraft.DataFixer.Util.Pair<K, V>, NetCraft.DataFixer.Util.Pair<K2, V>, K, K2>(
                typeof(ITraversalPMu),
                DSL.And(aType, valueType),
                DSL.And(bType, valueType),
                aType,
                bType,
                OpticsClass.Proj1<K, V, K2>()));

    //compoundListElements构造复合列表value遍历组合listTraversal+proj2
    public static TypedOptic<List<NetCraft.DataFixer.Util.Pair<K, V>>, List<NetCraft.DataFixer.Util.Pair<K, V2>>, V, V2> CompoundListElements<K, V, V2>(
        Type<K> keyType, Type<V> aType, Type<V2> bType)
        => new TypedOptic<List<NetCraft.DataFixer.Util.Pair<K, V>>, List<NetCraft.DataFixer.Util.Pair<K, V2>>, NetCraft.DataFixer.Util.Pair<K, V>, NetCraft.DataFixer.Util.Pair<K, V2>>(
            typeof(ITraversalPMu),
            DSL.CompoundList(keyType, aType),
            DSL.CompoundList(keyType, bType),
            DSL.And(keyType, aType),
            DSL.And(keyType, bType),
            OpticsClass.ListTraversal<NetCraft.DataFixer.Util.Pair<K, V>, NetCraft.DataFixer.Util.Pair<K, V2>>())
            .Compose(new TypedOptic<NetCraft.DataFixer.Util.Pair<K, V>, NetCraft.DataFixer.Util.Pair<K, V2>, V, V2>(
                typeof(ITraversalPMu),
                DSL.And(keyType, aType),
                DSL.And(keyType, bType),
                aType,
                bType,
                OpticsClass.Proj2<K, V, V2>()));

    //list构造列表遍历
    public static TypedOptic<List<A>, List<B>, A, B> List<A, B>(Type<A> aType, Type<B> bType)
        => new(typeof(ITraversalPMu),
            DSL.List(aType),
            DSL.List(bType),
            aType,
            bType,
            OpticsClass.ListTraversal<A, B>());

    //tagged构造TaggedChoice按key选择 bounds为ICocartesianMu
    public static TypedOptic<NetCraft.DataFixer.Util.Pair<K, object>, NetCraft.DataFixer.Util.Pair<K, object>, A, B> Tagged<K, A, B>(
        TaggedChoice<K>.TaggedChoiceType<K> sType, K key, Type<A> aType, Type<B> bType)
    {
        //aType/bType编译时Type<A>/Type<B>运行时可能是ProductType<object,object>继承Type<Pair<object,object>>
        //Element record构造时强转Type<A>/Type<B>失败用Unsafe.As绕过运行时类型检查对齐Java类型擦除
        var aObj = (object)aType;
        var aCast = System.Runtime.CompilerServices.Unsafe.As<object, Type<A>>(ref aObj);
        var bObj = (object)bType;
        var bCast = System.Runtime.CompilerServices.Unsafe.As<object, Type<B>>(ref bObj);
        return new(typeof(ICocartesianMu),
            sType,
            ReplaceTagged(sType, key, aCast, bCast),
            aCast,
            bCast,
            new InjTagged<K, A, B>(key));
    }

    //replaceTagged用bType替换sType中key对应类型构造新TaggedChoiceType
    internal static Type<NetCraft.DataFixer.Util.Pair<K, object>> ReplaceTagged<K, A, B>(TaggedChoice<K>.TaggedChoiceType<K> sType, K key, Type<A> aType, Type<B> bType)
    {
        if (Equals(aType, bType)) return sType;
        if (!Equals(sType.Types()[key], aType)) throw new ArgumentException("Focused type doesn't match.");
        var newTypes = new Dictionary<K, Type<object>>(sType.Types());
        //bType实际可能是ProductType<object,object>强转Type<object>失败用Unsafe.As绕过
        var bObj = (object)bType!;
        newTypes[key] = System.Runtime.CompilerServices.Unsafe.As<object, Type<object>>(ref bObj);
        return DSL.TaggedChoiceType(sType.GetName(), sType.GetKeyType(), newTypes);
    }

    //InstanceOf检查bounds全部是proof的超类型
    public static bool InstanceOf(IEnumerable<object> bounds, object proof)
        => bounds.All(b => b is Type bt && proof is Type pt && bt.IsAssignableFrom(pt));
}
