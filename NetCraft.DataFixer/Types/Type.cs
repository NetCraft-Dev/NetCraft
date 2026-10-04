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

//Codec.Pair与Util.Pair重名用Codec.Pair全限定避免冲突
//DataFixer.Util.Pair是HKT版本不适用于Type返回值
//Either在Util命名空间需要完全限定NetCraft.DataFixer.Util.Either

//Type类型基类对应原版com.mojang.datafixers.types.Type
//所有具体类型的根抽象类提供重写/查找/编解码入口
public abstract class Type<A> : App<Type<A>.Mu, A>
{
    //Mu一元HKT标记
    public sealed class Mu : K1 { }

    //还原类型应用为Type<A>
    public static Type<A> Unbox<A2>(App<Mu, A2> box) where A2 : A
        => (Type<A>)(object)box!;

    //RewriteCacheKey重写缓存键由类型+规则+优化规则组成
    private sealed record RewriteCacheKey(Type<object> Type, object Rule, object OptimizationRule);

    //REWRITE_CACHE已完成重写的缓存
    private static readonly Dictionary<RewriteCacheKey, object> REWRITE_CACHE = new();
    private static readonly object _cacheLock = new();

    private TypeTemplate? _template;
    private Codec<A>? _codec;

    //rewriteOrNop尝试应用规则失败返回nop
    public RewriteResult<A, object> RewriteOrNop(object rule)
    {
        var opt = ((TypeRewriteRule)rule).Rewrite(this);
        return opt.IsPresent ? opt.Get() : RewriteResult<A, object>.Nop(this);
    }

    //opticView把子视图重写结果用optic投射到外层nop直接返回nop
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

    //all对所有直接子类型应用规则并组合结果默认nop
    public virtual RewriteResult<A, object> All(object rule, bool recurse, bool checkIndex)
        => RewriteResult<A, object>.Nop(this);

    //one对唯一子类型应用规则默认空
    public virtual Optional<RewriteResult<A, object>> One(object rule)
        => Optional<RewriteResult<A, object>>.Empty();

    //everywhere递归到处应用规则组合orElse+all递归
    public virtual Optional<RewriteResult<A, object>> Everywhere(object rule, object optimizationRule, bool recurse, bool checkIndex)
    {
        var typeRule = (TypeRewriteRule)rule;
        var optRule = (PointFreeRule)optimizationRule;
        var rule2 = TypeRewriteRule.Seq(
            TypeRewriteRule.OrElse(typeRule, TypeRewriteRule.Nop()),
            TypeRewriteRule.All(TypeRewriteRule.Everywhere(typeRule, optRule, recurse, checkIndex), recurse, checkIndex));
        return Rewrite(rule2, optimizationRule);
    }

    //updateMu用新Family替换递归点默认返回自身
    public virtual Type<object> UpdateMu(RecursiveTypeFamily newFamily)
        => (Type<object>)(object)this;

    //template惰性构建模板
    public TypeTemplate Template()
        => _template ??= BuildTemplate();

    //buildTemplate子类提供模板构建逻辑
    public abstract TypeTemplate BuildTemplate();

    //findChoiceType查找带标签选择类型默认空
    public virtual Optional<object> FindChoiceType(string name, int index)
        => Optional<object>.Empty();

    //findCheckedType查找检查类型默认空
    public virtual Optional<Type<object>> FindCheckedType(int index)
        => Optional<Type<object>>.Empty();

    //read从Dynamic读取返回值与剩余Dynamic
    public DataResult<NetCraft.Codec.Pair<A, Dynamic<T>>> Read<T>(Dynamic<T> input)
        => Codec().Parse(input.Ops, input.Value).Map(a => new NetCraft.Codec.Pair<A, Dynamic<T>>(a, input));

    //codec惰性构建编解码器
    public Codec<A> Codec() => _codec ??= BuildCodec();

    //buildCodec子类提供编解码器构建逻辑
    protected abstract Codec<A> BuildCodec();

    //write将值编码到ops
    public DataResult<T> Write<T>(DynamicOps<T> ops, A value)
        => Codec().EncodeStart(ops, value);

    //writeDynamic将值编码为Dynamic
    public DataResult<Dynamic<T>> WriteDynamic<T>(DynamicOps<T> ops, A value)
        => Write(ops, value).Map(result => new Dynamic<T>(ops, result));

    //readTyped从Dynamic读取并包装为Typed
    public DataResult<NetCraft.Codec.Pair<Typed<A>, T>> ReadTyped<T>(Dynamic<T> input)
        => ReadTyped(input.Ops, input.Value);

    //readTyped从原始值读取并包装为Typed
    public DataResult<NetCraft.Codec.Pair<Typed<A>, T>> ReadTyped<T>(DynamicOps<T> ops, T input)
        => Codec().Parse(ops, input).Map(v => new NetCraft.Codec.Pair<Typed<A>, T>(new Typed<A>(this, (DynamicOps<object>)(object)ops, v), input));

    //read按规则读取并应用重写
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

    //readAndWrite读取并按规则重写后写入期望类型
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

    //capWrite把解码值经view转换后用新类型编码回T
    private DataResult<T> CapWrite<T, B>(DynamicOps<T> ops, Type<object> expectedType, T rest, A value, View<A, B> view)
    {
        //对齐原版capWrite用equals(view.newType(), true, false) ignoreRecursionPoints=true
        if (!expectedType.Equals(view.NewType(), true, false))
        {
            return DataResult<T>.Error(() => "Rewritten type doesn't match");
        }
        //ops是DynamicOps<T>原版Java靠类型擦除当DynamicOps<Object>用
        //C#严格泛型不变量用Unsafe.As绕过运行时检查
        var opsObj = System.Runtime.CompilerServices.Unsafe.As<DynamicOps<T>, DynamicOps<object>>(ref ops);
        var valueObj = (object)value;
        var fixedValue = view.Function!.EvalCached()(opsObj)((A)valueObj!);
        var encodeResult = view.NewType().Codec().EncodeStart(ops, (B)(object)fixedValue!);
        string encodeErr = "n/a";
        encodeResult.ResultOrPartial(err => encodeErr = err);
        return encodeResult;
    }

    //rewrite按规则+优化规则重写带缓存
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

    //getSetType通过optic查找新类型
    public Type<object> GetSetType<FT, FR>(OpticFinder<FT> optic, Type<FR> newType)
    {
        var findResult = optic.FindType((Type<object>)(object)this, newType, false);
        if (findResult.IsRight)
        {
            throw new InvalidOperationException("Field not found: " + findResult.GetRight().Get());
        }
        return ((TypedOptic<object, object, FT, FR>)findResult.GetLeft().Get()).TType();
    }

    //TypeMatcher类型匹配器接口
    public interface TypeMatcher<FT, FR>
    {
        Either<TypedOptic<object, object, FT, FR>, FieldNotFoundException> Match<S>(Type<S> targetType);
    }

    //findFieldTypeOpt查找字段类型可选默认空
    public virtual Optional<Type<object>> FindFieldTypeOpt(string name)
        => Optional<Type<object>>.Empty();

    //findFieldType查找字段类型失败抛异常
    public Type<object> FindFieldType(string name)
    {
        var opt = FindFieldTypeOpt(name);
        return opt.IsPresent ? opt.Get() : throw new ArgumentException("Field not found: " + name);
    }

    //findField构造字段查找器
    public OpticFinder<A> FindField(string name)
        => new FieldFinder<A>(name, (Type<A>)(object)FindFieldType(name));

    //point用ops填充默认值默认空
    public virtual Optional<A> Point<T>(DynamicOps<T> ops)
        => Optional<A>.Empty();

    //pointTyped用ops填充默认值包装为Typed
    public Optional<Typed<A>> PointTyped<T>(DynamicOps<T> ops)
        => Point(ops).Map(value => new Typed<A>(this, (DynamicOps<object>)(object)ops, value));

    //findTypeCached缓存查找类型
    public Either<TypedOptic<object, object, FT, FR>, FieldNotFoundException> FindTypeCached<FT, FR>(Type<FT> type, Type<FR> resultType, TypeMatcher<FT, FR> matcher, bool recurse)
        => FindType(type, resultType, matcher, recurse);

    //findType查找类型matcher先匹配自身失败则查子
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

    //findTypeInChildren在子类型中查找默认无更多子
    //标记virtual让CheckType等子类重写委托到delegate对齐原版语义
    public virtual Either<TypedOptic<object, object, FT, FR>, FieldNotFoundException> FindTypeInChildren<FT, FR>(Type<FT> type, Type<FR> resultType, TypeMatcher<FT, FR> matcher, bool recurse)
        => Either<TypedOptic<object, object, FT, FR>, FieldNotFoundException>.Right(new FieldNotFoundException("No more children"));

    //finder构造自身查找器
    public OpticFinder<A> Finder()
        => DSL.TypeFinder(this);

    //ifSame按Typed判断类型相同返回值
    public Optional<A> IfSame<B>(Typed<B> value)
        => IfSame(value.GetType(), value.GetValue());

    //ifSame按类型+值判断相同返回值
    public Optional<A> IfSame<B>(Type<B> type, B value)
    {
        if (Equals(type, true, true))
        {
            return Optional<A>.OfNullable((A)(object)value!);
        }
        return Optional<A>.Empty();
    }

    //ifSame按类型+重写结果判断相同返回重写结果
    public Optional<RewriteResult<A, object>> IfSame<B>(Type<B> type, RewriteResult<B, object> value)
    {
        var equal = Equals(type, true, true);
        if (equal)
        {
            return Optional<RewriteResult<A, object>>.Of((RewriteResult<A, object>)(object)value);
        }
        return Optional<RewriteResult<A, object>>.Empty();
    }

    //equals最终委托到参数化版本
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj)) return true;
        return Equals(obj, false, true);
    }

    public override int GetHashCode() => base.GetHashCode();

    //equals参数化版本子类提供
    public abstract bool Equals(object? o, bool ignoreRecursionPoints, bool checkIndex);

    //TypeError类型错误基类
    public abstract class TypeError
    {
        private readonly string _message;
        protected TypeError(string message) => _message = message;
        public override string ToString() => _message;
    }

    //FieldNotFoundException字段未找到异常
    public class FieldNotFoundException : TypeError
    {
        public FieldNotFoundException(string message) : base(message) { }
    }

    //Continue继续查找信号
    public sealed class Continue : FieldNotFoundException
    {
        public Continue() : base("Continue") { }
    }
}
