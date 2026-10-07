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

//TypedOptics non-generic static factory class maps to vanilla TypedOptic static methods
//static methods on the generic record TypedOptic<S,T,A,B> would require 4 type parameters when called in C#, so they are unusable
//moved to a non-generic class, following the OFAP convention of moving static factories to non-generic classes
public static class TypedOptics
{
    //adapter builds an identity adapter; bounds is IProfunctorMu
    public static TypedOptic<S, T, S, T> Adapter<S, T>(Type<S> sType, Type<T> tType)
        => new(typeof(IProfunctorMu), sType, tType, sType, tType, OpticsClass.Id<S, T>());

    //proj1 builds the first-component projection of Pair; bounds is ICartesianMu
    public static TypedOptic<NetCraft.DataFixer.Util.Pair<F, G>, NetCraft.DataFixer.Util.Pair<F2, G>, F, F2> Proj1<F, G, F2>(
        Type<F> fType, Type<G> gType, Type<F2> newType)
        => new(typeof(ICartesianMu),
            DSL.And(fType, gType),
            DSL.And(newType, gType),
            fType,
            newType,
            OpticsClass.Proj1<F, G, F2>());

    //proj2 builds the second-component projection of Pair
    public static TypedOptic<NetCraft.DataFixer.Util.Pair<F, G>, NetCraft.DataFixer.Util.Pair<F, G2>, G, G2> Proj2<F, G, G2>(
        Type<F> fType, Type<G> gType, Type<G2> newType)
        => new(typeof(ICartesianMu),
            DSL.And(fType, gType),
            DSL.And(fType, newType),
            gType,
            newType,
            OpticsClass.Proj2<F, G, G2>());

    //inj1 builds the Either left injection
    public static TypedOptic<Either<F, G>, Either<F2, G>, F, F2> Inj1<F, G, F2>(
        Type<F> fType, Type<G> gType, Type<F2> newType)
        => new(typeof(ICocartesianMu),
            DSL.Or(fType, gType),
            DSL.Or(newType, gType),
            fType,
            newType,
            OpticsClass.Inj1<F, G, F2>());

    //inj2 builds the Either right injection
    public static TypedOptic<Either<F, G>, Either<F, G2>, G, G2> Inj2<F, G, G2>(
        Type<F> fType, Type<G> gType, Type<G2> newType)
        => new(typeof(ICocartesianMu),
            DSL.Or(fType, gType),
            DSL.Or(fType, newType),
            gType,
            newType,
            OpticsClass.Inj2<F, G, G2>());

    //compoundListKeys builds a compound list key traversal, combining listTraversal+proj1
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

    //compoundListElements builds a compound list value traversal, combining listTraversal+proj2
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

    //list builds a list traversal
    public static TypedOptic<List<A>, List<B>, A, B> List<A, B>(Type<A> aType, Type<B> bType)
        => new(typeof(ITraversalPMu),
            DSL.List(aType),
            DSL.List(bType),
            aType,
            bType,
            OpticsClass.ListTraversal<A, B>());

    //tagged builds a TaggedChoice selected by key; bounds is ICocartesianMu
    public static TypedOptic<NetCraft.DataFixer.Util.Pair<K, object>, NetCraft.DataFixer.Util.Pair<K, object>, A, B> Tagged<K, A, B>(
        TaggedChoice<K>.TaggedChoiceType<K> sType, K key, Type<A> aType, Type<B> bType)
    {
        //aType/bType are Type<A>/Type<B> at compile time but may be ProductType<object,object> at runtime, inheriting Type<Pair<object,object>>
        //casting to Type<A>/Type<B> fails when constructing the Element record; use Unsafe.As to bypass the runtime type check, aligning with Java type erasure
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

    //replaceTagged replaces the type corresponding to key in sType with bType to build a new TaggedChoiceType
    internal static Type<NetCraft.DataFixer.Util.Pair<K, object>> ReplaceTagged<K, A, B>(TaggedChoice<K>.TaggedChoiceType<K> sType, K key, Type<A> aType, Type<B> bType)
    {
        if (Equals(aType, bType)) return sType;
        if (!Equals(sType.Types()[key], aType)) throw new ArgumentException("Focused type doesn't match.");
        var newTypes = new Dictionary<K, Type<object>>(sType.Types());
        //bType may actually be ProductType<object,object>; casting to Type<object> fails, so use Unsafe.As to bypass
        var bObj = (object)bType!;
        newTypes[key] = System.Runtime.CompilerServices.Unsafe.As<object, Type<object>>(ref bObj);
        return DSL.TaggedChoiceType(sType.GetName(), sType.GetKeyType(), newTypes);
    }

    //InstanceOf checks that all bounds are supertypes of proof
    public static bool InstanceOf(IEnumerable<object> bounds, object proof)
        => bounds.All(b => b is Type bt && proof is Type pt && bt.IsAssignableFrom(pt));
}
