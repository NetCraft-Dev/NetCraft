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

//TypedOptic optic with type information maps to vanilla com.mojang.datafixers.TypedOptic
//records the S->T->A->B four-way types and the proof bounds
//static factory methods moved to the non-generic TypedOptics class, avoiding the type parameters needed to call static methods on a generic class
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

    //SType outermost source type; Unsafe.As bypasses generic invariance, aligning with Java type erasure
    public Type<S> SType() => AsElement<S, T, object, object>(Elements[0]).SType;
    //TType outermost target type
    public Type<T> TType() => AsElement<S, T, object, object>(Elements[0]).TType;
    //AType innermost focus source type
    public Type<A> AType() => AsElement<object, object, A, B>(Elements[^1]).AType;
    //BType innermost focus target type
    public Type<B> BType() => AsElement<object, object, A, B>(Elements[^1]).BType;

    //AsElement uses Unsafe.As to bypass generic invariance, aligning with Java type erasure and singleton sharing
    private static Element<ES, ET, EA, EB> AsElement<ES, ET, EA, EB>(object element)
        => System.Runtime.CompilerServices.Unsafe.As<object, Element<ES, ET, EA, EB>>(ref element);

    //compose merges the outer and inner optics, concatenating bounds and elements
    public TypedOptic<S, T, A1, B1> Compose<A1, B1>(TypedOptic<A, B, A1, B1> other)
    {
        var bounds = new HashSet<object>(Bounds);
        bounds.UnionWith(other.Bounds);
        var elements = new List<object>(Elements);
        elements.AddRange(other.Elements);
        return new TypedOptic<S, T, A1, B1>(bounds, elements);
    }

    //upCast verifies that bounds contains proof; a single element is returned directly, multiple elements are combined into a CompositionOptic
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
            //the multi-element combination uses CompositionOptic to implement Eval, aligning with vanilla Optics.CompositionOptic
            //Proof is fixed to IProfunctorMu to satisfy the K1 constraint; the caller reflectively invokes Eval without checking the concrete Proof type
            var compositionOptic = new CompositionOptic<IProfunctorMu, S, T, A, B>(optics);
            return Optional<object>.Of(compositionOptic);
        }
        return Optional<object>.Empty();
    }

    //outermost returns the Optic of the outermost Element, maps to vanilla outermost
    //used by Optics.IsProj1/2/IsInj1/2 to determine the outermost type
    //use Unsafe.As to bypass the strict generic cast of Element<ES,ET,EA,EB>, aligning with Java type erasure
    public object Outermost()
    {
        var elemObj = (object?)Elements[0];
        var elemCast = System.Runtime.CompilerServices.Unsafe.As<object, Element<object, object, object, object>>(ref elemObj!);
        return elemCast.Optic;
    }

    //castOuter changes the outer type unchecked, using AsElement to bypass generic invariance
    public TypedOptic<S2, T2, A, B> CastOuterUnchecked<S2, T2>(Type<S2> sType, Type<T2> tType)
    {
        var newElements = new List<object>(Elements);
        newElements[0] = AsElement<S, T, A, B>(newElements[0]).CastOuterUnchecked(sType, tType);
        return new TypedOptic<S2, T2, A, B>(Bounds, newElements);
    }

    //castOuter changes the outer type unchecked
    public TypedOptic<S, T, A, B> CastOuter(Type<S> sType, Type<T> tType)
        => CastOuterUnchecked(sType, tType);

    //CastOuterUncheckedObject is a non-generic version using Unsafe.As to bypass compile-time type checking
    //used by reflective calls such as PointFreeRule.SortProj, aligning with Java type erasure semantics
    public TypedOptic<object, object, A, B> CastOuterUncheckedObject(object sType, object tType)
    {
        var sObj = sType;
        var tObj = tType;
        var sCast = System.Runtime.CompilerServices.Unsafe.As<object, Type<object>>(ref sObj);
        var tCast = System.Runtime.CompilerServices.Unsafe.As<object, Type<object>>(ref tObj);
        return CastOuterUnchecked(sCast, tCast);
    }

    //apply applies the profunctor proof to input and returns the result, maps to vanilla TypedOptic.apply
    //internally calls Optic.Eval or chains the optics of multiple Elements
    //C# compiles a delegate cache with reflection, aligning with Java type erasure semantics and avoiding reflective Invoke overhead each time
    public App2<P, S, T> Apply<P>(object proofInstance, App2<P, A, B> input) where P : K2
    {
        if (Elements.Count == 1)
        {
            var optic = ((Element<S, T, A, B>)Elements[0]).Optic;
            var func = InvokeEval<P>(optic, proofInstance);
            return ((System.Func<App2<P, A, B>, App2<P, S, T>>)(object)func).Invoke(input);
        }
        //multiple elements apply each Element's optic in a chain from right to left
        object current = input;
        for (int i = Elements.Count - 1; i >= 0; i--)
        {
            var optic = ((Element<object, object, object, object>)Elements[i]).Optic;
            current = InvokeEvalChain<P>(optic, proofInstance, current);
        }
        return (App2<P, S, T>)(object)current;
    }

    //InvokeEval uses an expression-tree compiled delegate cache keyed by (opticType, pType), avoiding reflective Invoke each time
    //maps to vanilla Java's direct virtual dispatch after type erasure; C# emulates it with expression trees
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

    //InvokeEvalChain chains intermediate steps, erasing to object and reusing _evalCache
    private static object InvokeEvalChain<P>(object optic, object proofInstance, object input) where P : K2
    {
        var func = InvokeEval<P>(optic, proofInstance);
        return ((System.Func<object, object>)(object)func!).Invoke(input);
    }

    //Element single Optic element recording the four-way types and the concrete Optic
    public sealed record Element<ES, ET, EA, EB>(Type<ES> SType, Type<ET> TType, Type<EA> AType, Type<EB> BType, object Optic)
    {
        public Element<ES2, ET2, EA, EB> CastOuterUnchecked<ES2, ET2>(Type<ES2> sType, Type<ET2> tType)
            => new(sType, tType, AType, BType, Optic);
    }
}

//CompositionOpticAdapter multi-optic composition adapter staging the optics list until stage B wires up real composition
internal sealed class CompositionOpticAdapter<S, T, A, B>
{
    private readonly List<object> _optics;
    public CompositionOpticAdapter(List<object> optics) => _optics = optics;
    public List<object> Optics => _optics;
}
