namespace NetCraft.DataFixer;

using System;
using System.Collections.Generic;
using NetCraft.Codec;
using T = NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics;
using NetCraft.DataFixer.Optics.Profunctors;
using NetCraft.DataFixer.Types.Templates;
using NetCraft.DataFixer.Util;
using OpticsClass = NetCraft.DataFixer.Optics.Optics;

//Typed typed value maps to vanilla com.mojang.datafixers.Typed
//binds Type<A>, ops, and value into an operable Typed instance
public sealed class Typed<A>
{
    //type is the bound type
    public T.Type<A> TypeValue { get; }
    //ops is the bound dynamic ops
    public DynamicOps<object> Ops { get; }
    //value is the actual value
    public A Value { get; }

    public Typed(T.Type<A> type, DynamicOps<object> ops, A value)
    {
        TypeValue = type;
        Ops = ops;
        Value = value;
    }

    public override string ToString() => "Typed[" + Value + "]";

    //get finds the focus value through an optic
    public FT Get<FT>(OpticFinder<FT> optic)
    {
        var findResult = optic.FindType((T.Type<object>)(object)TypeValue!, false);
        if (findResult.IsRight) throw new InvalidOperationException("Field not found: " + findResult.GetRight().Get());
        var field = (TypedOptic<A, object, FT, FT>)(object)findResult.GetLeft().Get();
        var forgetOptic = OpticsClass.Forget<FT, FT, FT>(a => a);
        var boxed = field.Apply<Forgets.Mu<FT>>(ForgetInstance<FT>.InstanceOf, (App2<Forgets.Mu<FT>, FT, FT>)(object)forgetOptic);
        return Forgets.Unbox<FT, A, object>(boxed).Run(Value);
    }

    //getTyped finds a Typed through an optic
    public Typed<FT> GetTyped<FT>(OpticFinder<FT> optic)
    {
        var findResult = optic.FindType((T.Type<object>)(object)TypeValue!, false);
        if (findResult.IsRight) throw new InvalidOperationException("Field not found: " + findResult.GetRight().Get());
        var field = (TypedOptic<A, object, FT, FT>)(object)findResult.GetLeft().Get();
        var forgetOptic = OpticsClass.Forget<FT, FT, FT>(a => a);
        var boxed = field.Apply<Forgets.Mu<FT>>(ForgetInstance<FT>.InstanceOf, (App2<Forgets.Mu<FT>, FT, FT>)(object)forgetOptic);
        var value = Forgets.Unbox<FT, A, object>(boxed).Run(Value);
        return new Typed<FT>(field.AType(), Ops, value);
    }

    //getOptional finds an optional value through an optic
    public Optional<FT> GetOptional<FT>(OpticFinder<FT> optic)
    {
        var findResult = optic.FindType((T.Type<object>)(object)TypeValue!, false);
        if (findResult.IsRight) throw new InvalidOperationException("Field not found: " + findResult.GetRight().Get());
        var field = (TypedOptic<A, object, FT, FT>)(object)findResult.GetLeft().Get();
        var forgetOptic = OpticsClass.ForgetOpt<FT, FT, FT>(a => Optional<FT>.Of(a));
        var boxed = field.Apply<ForgetOpts.Mu<FT>>(ForgetOptInstance<FT>.InstanceOf, (App2<ForgetOpts.Mu<FT>, FT, FT>)(object)forgetOptic);
        return ForgetOpts.Unbox<FT, A, object>(boxed).Run(Value);
    }

    //getOrCreate finds through an optic, using the point default when absent
    public FT GetOrCreate<FT>(OpticFinder<FT> optic)
    {
        var optional = GetOptional(optic);
        var combined = DataFixUtils.Or(optional, () => optic.Type().Point(Ops));
        if (combined.IsPresent) return combined.Get();
        throw new InvalidOperationException("Could not create default value for type: " + optic.Type());
    }

    //getOrDefault finds through an optic, using the default when absent
    public FT GetOrDefault<FT>(OpticFinder<FT> optic, FT def)
    {
        var findResult = optic.FindType((T.Type<object>)(object)TypeValue!, false);
        if (findResult.IsRight) throw new InvalidOperationException("Field not found: " + findResult.GetRight().Get());
        var field = (TypedOptic<A, object, FT, FT>)(object)findResult.GetLeft().Get();
        var forgetOptic = OpticsClass.ForgetOpt<FT, FT, FT>(a => Optional<FT>.Of(a));
        var boxed = field.Apply<ForgetOpts.Mu<FT>>(ForgetOptInstance<FT>.InstanceOf, (App2<ForgetOpts.Mu<FT>, FT, FT>)(object)forgetOptic);
        return ForgetOpts.Unbox<FT, A, object>(boxed).Run(Value).OrElse(def);
    }

    //getOptionalTyped finds an optional Typed through an optic
    public Optional<Typed<FT>> GetOptionalTyped<FT>(OpticFinder<FT> optic)
    {
        var findResult = optic.FindType((T.Type<object>)(object)TypeValue!, false);
        if (findResult.IsRight) throw new InvalidOperationException("Field not found: " + findResult.GetRight().Get());
        var field = (TypedOptic<A, object, FT, FT>)(object)findResult.GetLeft().Get();
        var forgetOptic = OpticsClass.ForgetOpt<FT, FT, FT>(a => Optional<FT>.Of(a));
        var boxed = field.Apply<ForgetOpts.Mu<FT>>(ForgetOptInstance<FT>.InstanceOf, (App2<ForgetOpts.Mu<FT>, FT, FT>)(object)forgetOptic);
        return ForgetOpts.Unbox<FT, A, object>(boxed).Run(Value).Map(v => new Typed<FT>(field.AType(), Ops, v));
    }

    //getOrCreateTyped finds through an optic, using the pointTyped default when absent
    public Typed<FT> GetOrCreateTyped<FT>(OpticFinder<FT> optic)
    {
        var optional = GetOptionalTyped(optic);
        var combined = DataFixUtils.Or(optional, () => optic.Type().PointTyped(Ops));
        if (combined.IsPresent) return combined.Get();
        throw new InvalidOperationException("Could not create default value for type: " + optic.Type());
    }

    //set replaces the focus through an optic with newValue
    public Typed<object> Set<FT>(OpticFinder<FT> optic, FT newValue)
        => Set<FT, FT>(optic, optic.Type(), newValue);

    //set replaces the focus through an optic with a new Type and value
    public Typed<object> Set<FT, FR>(OpticFinder<FT> optic, T.Type<FR> newType, FR newValue)
        => Set<FT, FR>(optic, new Typed<FR>(newType, Ops, newValue));

    //set replaces the focus through an optic with a new Typed
    public Typed<object> Set<FT, FR>(OpticFinder<FT> optic, Typed<FR> newValue)
    {
        var findResult = optic.FindType((T.Type<object>)(object)TypeValue!, newValue.TypeValue, false);
        if (findResult.IsRight) throw new InvalidOperationException("Field not found: " + findResult.GetRight().Get());
        var field = (TypedOptic<A, object, FT, FR>)(object)findResult.GetLeft().Get();
        return SetCap<object, FT, FR>(field, newValue);
    }

    //setCap writes a new value with the ReForgetC reverse forgetful optic and returns a new Typed
    private Typed<B> SetCap<B, FT, FR>(TypedOptic<A, B, FT, FR> field, Typed<FR> newValue)
    {
        var reForgetOptic = OpticsClass.ReForgetC<FR, FT, FR>("set",
            Either<Func<FR, FR>, Func<FT, FR, FR>>.Left((FR fr) => fr));
        var boxed = field.Apply<ReForgetCs.Mu<FR>>(ReForgetCInstance<FR>.InstanceOf, (App2<ReForgetCs.Mu<FR>, FT, FR>)(object)reForgetOptic);
        var impl = ReForgetCs.Unbox<FR, FT, FR>((App2<ReForgetCs.Mu<FR>, FT, FR>)(object)boxed).Impl();
        B b;
        if (impl.IsLeft)
        {
            var f = impl.GetLeft().Get();
            b = (B)(object)f(newValue.Value!)!;
        }
        else
        {
            var f = impl.GetRight().Get();
            b = (B)(object)f((FT)(object)Value!, newValue.Value!)!;
        }
        return new Typed<B>(field.TType(), Ops, b);
    }

    //updateTyped updates the focus through an optic and a Typed updating function
    public Typed<object> UpdateTyped<FT>(OpticFinder<FT> optic, Func<Typed<object>, Typed<object>> updater)
        => UpdateTypedImpl<FT, FT>(optic, optic.Type(), updater, false);

    //updateTyped updates the focus through an optic, a new Type, and a Typed updating function, maps to vanilla updateTyped(optic,newType,fn)
    public Typed<object> UpdateTyped<FT, FR>(OpticFinder<FT> optic, T.Type<FR> newType, Func<Typed<object>, Typed<object>> updater)
        => UpdateTypedImpl<FT, FR>(optic, newType, updater, false);

    //updateTypedImpl updates the focus through an optic, a new Type, and a Typed updating function
    private Typed<object> UpdateTypedImpl<FT, FR>(OpticFinder<FT> optic, T.Type<FR> newType, Func<Typed<object>, Typed<object>> updater, bool recurse)
    {
        var findResult = optic.FindType((T.Type<object>)(object)TypeValue!, newType, recurse);
        if (findResult.IsRight) throw new InvalidOperationException("Field not found: " + findResult.GetRight().Get());
        //findResult returns TypedOptic<object,object,object,object>; FieldFinder casts it to Type<object>, causing type erasure
        //use Unsafe.As to bypass the runtime type check, aligning with Java type erasure semantics
        var fieldObj = (object)findResult.GetLeft().Get();
        var field = System.Runtime.CompilerServices.Unsafe.As<object, TypedOptic<A, object, FT, FR>>(ref fieldObj);
        return UpdateCap<object, FT, FR>(field, ft =>
        {
            //optic.Type() returns Type<FT> but may actually be a Type<Dynamic<object>> subclass such as EmptyPartPassthrough
            //under C# strict generic invariance Type<Dynamic<object>> does not inherit Type<object>; a direct cast throws InvalidCastException
            //use Unsafe.As to bypass the runtime type check, aligning with Java type erasure semantics
            var opticTypeObj = (object)optic.Type()!;
            var opticType = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref opticTypeObj);
            var newValue = updater(new Typed<object>(opticType, Ops, (object)ft!));
            //newValue.TypeValue may actually be a Type<Dynamic<object>> subclass such as EmptyPartPassthrough
            //use Unsafe.As to bypass the runtime type check, aligning with Java type erasure semantics
            var newTypeObj = (object)newValue.TypeValue!;
            var newTypeCasted = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<FR>>(ref newTypeObj);
            var bType = field.BType()!;
            var ifSameResult = bType.IfSame<FR>(newTypeCasted!, (FR)(object)newValue.Value!);
            return ifSameResult.IsPresent ? ifSameResult.Get() : throw new ArgumentException("Function didn't update to the expected type bType=" + bType + " newValueType=" + newTypeCasted);
        });
    }

    //update updates the focus through an optic and a function
    public Typed<object> Update<FT>(OpticFinder<FT> optic, Func<FT, FT> updater)
        => UpdateImpl<FT, FT>(optic, optic.Type(), updater, false);

    //updateImpl updates the focus through an optic, a new Type, and a function
    private Typed<object> UpdateImpl<FT, FR>(OpticFinder<FT> optic, T.Type<FR> newType, Func<FT, FR> updater, bool recurse)
    {
        var findResult = optic.FindType((T.Type<object>)(object)TypeValue!, newType, recurse);
        if (findResult.IsRight) throw new InvalidOperationException("Field not found: " + findResult.GetRight().Get());
        //findResult returns TypedOptic<object,object,object,object>; FieldFinder casts it to Type<object>, causing type erasure
        //use Unsafe.As to bypass the runtime type check, aligning with Java type erasure semantics
        var fieldObj = (object)findResult.GetLeft().Get();
        var field = System.Runtime.CompilerServices.Unsafe.As<object, TypedOptic<A, object, FT, FR>>(ref fieldObj);
        return UpdateCap<object, FT, FR>(field, updater);
    }

    //updateRecursiveTyped updates the focus through a recursive optic and a Typed updating function
    public Typed<object> UpdateRecursiveTyped<FT>(OpticFinder<FT> optic, Func<Typed<object>, Typed<object>> updater)
        => UpdateTypedImpl<FT, FT>(optic, optic.Type(), updater, true);

    //updateRecursive updates the focus through a recursive optic and a function
    public Typed<object> UpdateRecursive<FT>(OpticFinder<FT> optic, Func<FT, FT> updater)
        => UpdateImpl<FT, FT>(optic, optic.Type(), updater, true);

    //updateCap applies updater with Traversal.wander plus IdF and returns a new Typed
    private Typed<B> UpdateCap<B, FT, FR>(TypedOptic<A, B, FT, FR> field, Func<FT, FR> updater)
    {
        //UpCast returns object, actually a concrete Optic such as Proj2; at runtime it is not Optic<ITraversalPMu,...>
        //the (object) cast followed by an (Optic<ITraversalPMu,...>) cast throws InvalidCastException at runtime
        //use Unsafe.As to bypass the runtime type check, aligning with Java type erasure semantics
        var opticObj = (object)field.UpCast(TypeClassesMarker.TraversalPToken)!.Get()!;
        var traversalOptic = System.Runtime.CompilerServices.Unsafe.As<object, Optic<ITraversalPMu, A, B, FT, FR>>(ref opticObj);
        var traversal = OpticsClass.ToTraversal<A, B, FT, FR>(traversalOptic)!;
        //traversal's actual type may be Traversal<Pair<string,object>,Pair<string,object>,FT,FR>; Unsafe.As casts it to Traversal<A,B,FT,FR>
        //under C# strict generic invariance the two closed types do not share a method table entry; calling Wander directly throws EntryPointNotFoundException
        //uses a reflective delegate cache to call Wander, aligning with Java virtual dispatch semantics after type erasure
        var boxed = WanderInvokerCache.InvokeWander<FT, FR, IdFs.Mu, IdFInstance.Mu>(traversal, IdFInstance.InstanceOf, ft => IdFs.Create<FR>(updater(ft)), Value!);
        //boxed is object but actually an IdF instance; Unsafe.As converts it to App<IdFs.Mu,B>, aligning with Java type erasure
        var boxedApp = System.Runtime.CompilerServices.Unsafe.As<object, App<IdFs.Mu, B>>(ref boxed);
        var b = IdFs.Get<B>(boxedApp);
        return new Typed<B>(field.TType(), Ops, b);
    }

    //getAllTyped takes all Typeds through an optic
    public List<Typed<FT>> GetAllTyped<FT>(OpticFinder<FT> optic)
    {
        var findResult = optic.FindType((T.Type<object>)(object)TypeValue!, optic.Type(), false);
        if (findResult.IsRight) throw new InvalidOperationException("Field not found: " + findResult.GetRight().Get());
        var field = (TypedOptic<object, object, FT, object>)(object)findResult.GetLeft().Get();
        var all = GetAll<FT>(field);
        var result = new List<Typed<FT>>(all.Count);
        foreach (var ft in all)
        {
            result.Add(new Typed<FT>(optic.Type(), Ops, ft));
        }
        return result;
    }

    //getAll takes all focus values through a TypedOptic
    //uses Const<List<FT>> as the Applicative with ListMonoid to accumulate all focuses
    public List<FT> GetAll<FT>(TypedOptic<object, object, FT, object> field)
    {
        //UpCast returns object; use Unsafe.As to convert it to Optic, aligning with Java type erasure semantics
        var opticObj = (object)field.UpCast(TypeClassesMarker.TraversalPToken)!.Get()!;
        var traversalOptic = System.Runtime.CompilerServices.Unsafe.As<object, Optic<ITraversalPMu, object, object, FT, object>>(ref opticObj);
        var traversal = OpticsClass.ToTraversal<object, object, FT, object>(traversalOptic)!;
        var constInstance = new ConstInstance<List<FT>>(Monoids.ListMonoid<FT>());
        //traversal's actual type may be Unsafe.As-cast; calling Wander directly throws EntryPointNotFoundException
        //uses the WanderInvokerCache reflective delegate cache, aligning with Java virtual dispatch semantics after type erasure
        var boxed = WanderInvokerCache.InvokeWander<FT, object, Consts.Mu<List<FT>>, ConstInstance<List<FT>>.Mu>(
            traversal, constInstance, ft => Consts.Create<List<FT>, object>(new List<FT> { ft }), (object)Value!);
        //boxed is object but actually a Const<List<FT>> instance; Unsafe.As converts it to App<Consts.Mu<List<FT>>,object>, aligning with Java type erasure
        var boxedApp = System.Runtime.CompilerServices.Unsafe.As<object, App<Consts.Mu<List<FT>>, object>>(ref boxed);
        return Consts.Unbox<List<FT>, object>(boxedApp);
    }

    //out expands a recursive point type into unfold
    public Typed<A> Out()
    {
        if (TypeValue is not RecursivePoint.RecursivePointType<A>)
        {
            throw new ArgumentException("Not recursive");
        }
        var unfold = ((RecursivePoint.RecursivePointType<A>)TypeValue).Unfold();
        return new Typed<A>(unfold, Ops, Value);
    }

    //inj1 injects the Either left
    public Typed<Either<A, B>> Inj1<B>(T.Type<B> type)
    {
        var inj1 = OpticsClass.Inj1<A, B, A>();
        var eitherValue = inj1.Build(Value);
        return new Typed<Either<A, B>>(DSL.Or(TypeValue, type), Ops, eitherValue);
    }

    //inj2 injects the Either right
    public Typed<Either<B, A>> Inj2<B>(T.Type<B> type)
    {
        var inj2 = OpticsClass.Inj2<B, A, A>();
        var eitherValue = inj2.Build(Value);
        return new Typed<Either<B, A>>(DSL.Or(type, TypeValue), Ops, eitherValue);
    }

    //pair combines two Typeds into a Pair using Util.Pair, aligning with the DSL.And return type
    public static Typed<NetCraft.DataFixer.Util.Pair<A, B>> Pair<A, B>(Typed<A> first, Typed<B> second)
        => new Typed<NetCraft.DataFixer.Util.Pair<A, B>>(DSL.And(first.TypeValue, second.TypeValue), first.Ops, NetCraft.DataFixer.Util.Pair<A, B>.Of(first.Value, second.Value));

    public T.Type<A> GetType() => TypeValue;
    public DynamicOps<object> GetOps() => Ops;
    public A GetValue() => Value;

    //write writes value back to a Dynamic
    public DataResult<Dynamic<object>> Write() => TypeValue.WriteDynamic(Ops, Value);
}
