namespace NetCraft.DataFixer.Types;

using System;
using System.Collections.Generic;
using System.Threading;
using NetCraft.Codec;
using NetCraft.DataFixer;
using NetCraft.DataFixer.Functions;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Types.Families;
using NetCraft.DataFixer.Types.Templates;
using NetCraft.DataFixer.Util;

//Codec.Pair and Util.Pair share a name; fully qualify with Codec.Pair to avoid conflicts
//DataFixer.Util.Pair is the HKT version and is not suitable as a Type return value
//Either lives in the Util namespace and must be fully qualified as NetCraft.DataFixer.Util.Either

//Type base class maps to vanilla com.mojang.datafixers.types.Type
//root abstract class of all concrete types, providing rewrite/find/codec entry points
public abstract class Type<A> : App<Type<A>.Mu, A>
{
    //Mu unary HKT marker
    public sealed class Mu : K1 { }

    //recover the type application as Type<A>
    public static Type<A> Unbox<A2>(App<Mu, A2> box) where A2 : A
        => (Type<A>)(object)box!;

    //RewriteCacheKey rewrite cache key composed of type + rule + optimization rule
    private sealed record RewriteCacheKey(Type<object> Type, object Rule, object OptimizationRule);

    //REWRITE_CACHE cache of completed rewrites
    private static readonly Dictionary<RewriteCacheKey, object> REWRITE_CACHE = new();
    private static readonly object _cacheLock = new();

    private TypeTemplate? _template;
    private Codec<A>? _codec;

    //rewriteOrNop tries to apply the rule and returns nop on failure
    public RewriteResult<A, object> RewriteOrNop(object rule)
    {
        var opt = ((TypeRewriteRule)rule).Rewrite(this);
        return opt.IsPresent ? opt.Get() : RewriteResult<A, object>.Nop(this);
    }

    //opticView projects the rewritten child view onto the outer type via optic; nop passes nop through
    public static RewriteResult<S, T> OpticView<S, T>(Type<S> type, RewriteResult<object, object> view, TypedOptic<S, T, object, object> optic)
    {
        if (view.View().IsNop())
        {
            return RewriteResult<S, T>.Nop(type);
        }
        return RewriteResult<S, T>.Create(
            View<S, T>.Create(
                Functions.App<System.Func<object, object>, System.Func<S, T>>(
                    Functions.ProfunctorTransformer<S, T, object, object>(optic),
                    view.View().Function!),
                type,
                optic.TType()),
            view.RecData());
    }

    //all applies the rule to every direct child type and combines the results; defaults to nop
    public virtual RewriteResult<A, object> All(object rule, bool recurse, bool checkIndex)
        => RewriteResult<A, object>.Nop(this);

    //one applies the rule to the single child type; defaults to empty
    public virtual Optional<RewriteResult<A, object>> One(object rule)
        => Optional<RewriteResult<A, object>>.Empty();

    //everywhere applies the rule recursively everywhere, combining orElse with recursive all
    public virtual Optional<RewriteResult<A, object>> Everywhere(object rule, object optimizationRule, bool recurse, bool checkIndex)
    {
        var typeRule = (TypeRewriteRule)rule;
        var optRule = (PointFreeRule)optimizationRule;
        var rule2 = TypeRewriteRule.Seq(
            TypeRewriteRule.OrElse(typeRule, TypeRewriteRule.Nop()),
            TypeRewriteRule.All(TypeRewriteRule.Everywhere(typeRule, optRule, recurse, checkIndex), recurse, checkIndex));
        return Rewrite(rule2, optimizationRule);
    }

    //updateMu replaces the recursive point with a new Family; defaults to returning itself
    public virtual Type<object> UpdateMu(RecursiveTypeFamily newFamily)
        => (Type<object>)(object)this;

    //template lazily builds the template
    public TypeTemplate Template()
        => _template ??= BuildTemplate();

    //buildTemplate provides template construction logic in subclasses
    public abstract TypeTemplate BuildTemplate();

    //findChoiceType looks up the tagged choice type; defaults to empty
    public virtual Optional<object> FindChoiceType(string name, int index)
        => Optional<object>.Empty();

    //findCheckedType looks up the checked type; defaults to empty
    public virtual Optional<Type<object>> FindCheckedType(int index)
        => Optional<Type<object>>.Empty();

    //read reads from a Dynamic, returning the value and the remaining Dynamic
    public DataResult<NetCraft.Codec.Pair<A, Dynamic<T>>> Read<T>(Dynamic<T> input)
        => Codec().Parse(input.Ops, input.Value).Map(a => new NetCraft.Codec.Pair<A, Dynamic<T>>(a, input));

    //codec lazily builds the codec
    public Codec<A> Codec() => _codec ??= BuildCodec();

    //buildCodec provides codec construction logic in subclasses
    protected abstract Codec<A> BuildCodec();

    //write encodes the value into ops
    public DataResult<T> Write<T>(DynamicOps<T> ops, A value)
        => Codec().EncodeStart(ops, value);

    //writeDynamic encodes the value as a Dynamic
    public DataResult<Dynamic<T>> WriteDynamic<T>(DynamicOps<T> ops, A value)
        => Write(ops, value).Map(result => new Dynamic<T>(ops, result));

    //readTyped reads from a Dynamic and wraps it as Typed
    public DataResult<NetCraft.Codec.Pair<Typed<A>, T>> ReadTyped<T>(Dynamic<T> input)
        => ReadTyped(input.Ops, input.Value);

    //readTyped reads from a raw value and wraps it as Typed
    public DataResult<NetCraft.Codec.Pair<Typed<A>, T>> ReadTyped<T>(DynamicOps<T> ops, T input)
        => Codec().Parse(ops, input).Map(v => new NetCraft.Codec.Pair<Typed<A>, T>(new Typed<A>(this, (DynamicOps<object>)(object)ops, v), input));

    //read reads and applies rewrites according to the rule
    public DataResult<NetCraft.Codec.Pair<Optional<object>, T>> Read<T>(DynamicOps<T> ops, object rule, object fRule, T input)
        => Codec().Parse(ops, input).Map(v =>
        {
            var rewriteOpt = Rewrite(rule, fRule);
            if (rewriteOpt.IsPresent)
            {
                var func = rewriteOpt.Get().View().Function!.EvalCached();
            var opsObj = (DynamicOps<object>)(object)ops;
            var valueObj = (object)v;
            var result = func(opsObj)((A)valueObj!);
                return new NetCraft.Codec.Pair<Optional<object>, T>(Optional<object>.OfNullable(result), input);
            }
            return new NetCraft.Codec.Pair<Optional<object>, T>(Optional<object>.Empty(), input);
        });

    //readAndWrite reads, rewrites per the rule, then writes to the expected type
    public DataResult<T> ReadAndWrite<T>(DynamicOps<T> ops, Type<object> expectedType, object rule, object fRule, T input)
    {
        var rewriteOpt = Rewrite(rule, fRule);
        if (!rewriteOpt.IsPresent)
        {
            return DataResult<T>.Error(() => "Could not build a rewrite rule: " + rule + " " + fRule, input);
        }
        var view = rewriteOpt.Get().View();
        if (view.IsNop())
        {
            return DataResult<T>.Success(input);
        }
        return Codec().Parse(ops, input).FlatMap(pair => CapWrite(ops, expectedType!, input, pair, view));
    }

    //capWrite converts the decoded value through the view, then encodes it back to T with the new type
    private DataResult<T> CapWrite<T, B>(DynamicOps<T> ops, Type<object> expectedType, T rest, A value, View<A, B> view)
    {
        //aligns with vanilla capWrite using equals(view.newType(), true, false), ignoreRecursionPoints=true
        if (!expectedType.Equals(view.NewType(), true, false))
        {
            return DataResult<T>.Error(() => "Rewritten type doesn't match");
        }
        //ops is DynamicOps<T>; vanilla Java uses type erasure to treat it as DynamicOps<Object>
        //C# strict generic invariance requires Unsafe.As to bypass the runtime check
        var opsObj = System.Runtime.CompilerServices.Unsafe.As<DynamicOps<T>, DynamicOps<object>>(ref ops);
        var valueObj = (object)value;
        var fixedValue = view.Function!.EvalCached()(opsObj)((A)valueObj!);
        var encodeResult = view.NewType().Codec().EncodeStart(ops, (B)(object)fixedValue!);
        string encodeErr = "n/a";
        encodeResult.ResultOrPartial(err => encodeErr = err);
        return encodeResult;
    }

    //rewrite rewrites per rule + optimization rule, with caching
    public Optional<RewriteResult<A, object>> Rewrite(object rule, object fRule)
    {
        var key = new RewriteCacheKey(TypeObjectConverterFactory.AsObjectType(this), rule, fRule);
        int hitCount = 0;
        TypeRewriteRule? similarRule = null;
        lock (_cacheLock)
        {
            foreach (var kv in REWRITE_CACHE)
            {
                if (kv.Key.Type?.Equals(key.Type) == true && kv.Key.OptimizationRule?.Equals(key.OptimizationRule) == true)
                {
                    hitCount++;
                    similarRule = (TypeRewriteRule?)kv.Key.Rule;
                    if (kv.Key.Rule?.Equals(key.Rule) == true)
                    {
                        return (Optional<RewriteResult<A, object>>)kv.Value!;
                    }
                }
            }
        }
        var result = ((TypeRewriteRule)rule!).Rewrite(this).FlatMap(r =>
            r.View().Rewrite((PointFreeRule)fRule!).Map(view => RewriteResult<A, object>.Create(view, r.RecData())));
        lock (_cacheLock)
        {
            REWRITE_CACHE[key] = result;
        }
        return result;
    }

    //getSetType looks up the new type through an optic
    public Type<object> GetSetType<FT, FR>(OpticFinder<FT> optic, Type<FR> newType)
    {
        var findResult = optic.FindType((Type<object>)(object)this, newType, false);
        if (findResult.IsRight)
        {
            throw new InvalidOperationException("Field not found: " + findResult.GetRight().Get());
        }
        return ((TypedOptic<object, object, FT, FR>)findResult.GetLeft().Get()).TType();
    }

    //TypeMatcher type matcher interface
    public interface TypeMatcher<FT, FR>
    {
        Either<TypedOptic<object, object, FT, FR>, FieldNotFoundException> Match<S>(Type<S> targetType);
    }

    //findFieldTypeOpt looks up a field type as an optional; defaults to empty
    public virtual Optional<Type<object>> FindFieldTypeOpt(string name)
        => Optional<Type<object>>.Empty();

    //findFieldType looks up a field type and throws on failure
    public Type<object> FindFieldType(string name)
    {
        var opt = FindFieldTypeOpt(name);
        return opt.IsPresent ? opt.Get() : throw new ArgumentException("Field not found: " + name);
    }

    //findField builds a field finder
    public OpticFinder<A> FindField(string name)
        => new FieldFinder<A>(name, (Type<A>)(object)FindFieldType(name));

    //point fills in a default value using ops; defaults to empty
    public virtual Optional<A> Point<T>(DynamicOps<T> ops)
        => Optional<A>.Empty();

    //pointTyped fills in a default value using ops and wraps it as Typed
    public Optional<Typed<A>> PointTyped<T>(DynamicOps<T> ops)
        => Point(ops).Map(value => new Typed<A>(this, (DynamicOps<object>)(object)ops, value));

    //findTypeCached caches type lookups
    public Either<TypedOptic<object, object, FT, FR>, FieldNotFoundException> FindTypeCached<FT, FR>(Type<FT> type, Type<FR> resultType, TypeMatcher<FT, FR> matcher, bool recurse)
        => FindType(type, resultType, matcher, recurse);

    //findType looks up a type; matcher tries itself first, then the children on failure
    public Either<TypedOptic<object, object, FT, FR>, FieldNotFoundException> FindType<FT, FR>(Type<FT> type, Type<FR> resultType, TypeMatcher<FT, FR> matcher, bool recurse)
    {
        var matchResult = matcher.Match(this);
        if (matchResult.IsLeft)
        {
            return Either<TypedOptic<object, object, FT, FR>, FieldNotFoundException>.Left(matchResult.GetLeft().Get());
        }
        var right = matchResult.GetRight().Get();
        return right is Continue
            ? FindTypeInChildren(type, resultType, matcher, recurse)
            : Either<TypedOptic<object, object, FT, FR>, FieldNotFoundException>.Right(right);
    }

    //findTypeInChildren searches child types; defaults to no further children
    //marked virtual so subclasses such as CheckType can override and delegate, aligning with vanilla semantics
    public virtual Either<TypedOptic<object, object, FT, FR>, FieldNotFoundException> FindTypeInChildren<FT, FR>(Type<FT> type, Type<FR> resultType, TypeMatcher<FT, FR> matcher, bool recurse)
        => Either<TypedOptic<object, object, FT, FR>, FieldNotFoundException>.Right(new FieldNotFoundException("No more children"));

    //finder builds a finder for itself
    public OpticFinder<A> Finder()
        => DSL.TypeFinder(this);

    //ifSame returns the value when the Typed types are equal
    public Optional<A> IfSame<B>(Typed<B> value)
        => IfSame(value.GetType(), value.GetValue());

    //ifSame returns the value when type + value match
    public Optional<A> IfSame<B>(Type<B> type, B value)
    {
        if (Equals(type, true, true))
        {
            return Optional<A>.OfNullable((A)(object)value!);
        }
        return Optional<A>.Empty();
    }

    //ifSame returns the rewrite result when type + rewrite result match
    public Optional<RewriteResult<A, object>> IfSame<B>(Type<B> type, RewriteResult<B, object> value)
    {
        var equal = Equals(type, true, true);
        if (equal)
        {
            return Optional<RewriteResult<A, object>>.Of((RewriteResult<A, object>)(object)value);
        }
        return Optional<RewriteResult<A, object>>.Empty();
    }

    //equals ultimately delegates to the parameterized version
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj)) return true;
        return Equals(obj, false, true);
    }

    public override int GetHashCode() => base.GetHashCode();

    //equals parameterized version; provided by subclasses
    public abstract bool Equals(object? o, bool ignoreRecursionPoints, bool checkIndex);

    //TypeError type error base class
    public abstract class TypeError
    {
        private readonly string _message;
        protected TypeError(string message) => _message = message;
        public override string ToString() => _message;
    }

    //FieldNotFoundException field-not-found exception
    public class FieldNotFoundException : TypeError
    {
        public FieldNotFoundException(string message) : base(message) { }
    }

    //Continue: keep searching signal
    public sealed class Continue : FieldNotFoundException
    {
        public Continue() : base("Continue") { }
    }
}
