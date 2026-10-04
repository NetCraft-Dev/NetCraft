namespace NetCraft.DataFixer;

using System;
using System.Collections.Generic;
using System.Linq;
using NetCraft.Codec;
using NetCraft.DataFixer.Functions;
using NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Util;

//TypeRewriteRule类型重写规则对应原版TypeRewriteRule
//对Type<A>应用规则返回Optional<RewriteResult<A,?>>
public interface TypeRewriteRule
{
    //rewrite对type应用规则
    Optional<RewriteResult<A, object>> Rewrite<A>(Type<A> type);

    //nop无操作规则单例
    static TypeRewriteRule Nop() => NopRule.INSTANCE;

    //seq组合多个规则按顺序应用
    static TypeRewriteRule Seq(List<TypeRewriteRule> rules) => new SeqRule(rules);

    //seq组合两个规则
    static TypeRewriteRule Seq(TypeRewriteRule first, TypeRewriteRule second)
        => ReferenceEquals(first, Nop()) ? second
            : ReferenceEquals(second, Nop()) ? first
            : Seq(new List<TypeRewriteRule> { first, second });

    //seq组合首个与可变参
    static TypeRewriteRule Seq(TypeRewriteRule firstRule, params TypeRewriteRule[] rules)
    {
        TypeRewriteRule rule = firstRule;
        foreach (var r in rules)
        {
            rule = Seq(rule, r);
        }
        return rule;
    }

    //orElse首个失败时应用第二
    static TypeRewriteRule OrElse(TypeRewriteRule first, TypeRewriteRule second)
        => new OrElseRule(first, () => second);

    //orElse首个失败时惰性应用第二
    static TypeRewriteRule OrElse(TypeRewriteRule first, Func<TypeRewriteRule> second)
        => new OrElseRule(first, second);

    //all对所有子类型应用规则
    static TypeRewriteRule All(TypeRewriteRule rule, bool recurse, bool checkIndex)
        => new AllRule(rule, recurse, checkIndex);

    //one对唯一子类型应用规则
    static TypeRewriteRule One(TypeRewriteRule rule) => new OneRule(rule);

    //once应用一次失败回退到one递归
    static TypeRewriteRule Once(TypeRewriteRule rule)
        => OrElse(rule, () => One(Once(rule)));

    //checkOnce检查应用结果为nop时回调
    static TypeRewriteRule CheckOnce(TypeRewriteRule rule, Action<Type<object>> onFail)
        => new CheckOnceRule(rule, onFail);

    //everywhere到处递归应用规则
    static TypeRewriteRule Everywhere(TypeRewriteRule rule, PointFreeRule optimizationRule, bool recurse, bool checkIndex)
        => new EverywhereRule(rule, optimizationRule, recurse, checkIndex);

    //ifSame按目标类型匹配时返回value
    static TypeRewriteRule IfSame<B>(Type<B> targetType, RewriteResult<B, object> value)
        => new IfSameRule<B>(targetType, value);

    //NopRule无操作规则实现
    public sealed class NopRule : TypeRewriteRule
    {
        public static readonly NopRule INSTANCE = new();
        private NopRule() { }
        public Optional<RewriteResult<A, object>> Rewrite<A>(Type<A> type)
            => Optional<RewriteResult<A, object>>.Of(RewriteResult<A, object>.Nop(type));
        //单例比较所有NopRule相等
        public override bool Equals(object? obj) => obj is NopRule;
        public override int GetHashCode() => typeof(NopRule).GetHashCode();
    }

    //SeqRule顺序组合按前一条结果newType作为下一条输入对齐原版cap1链式
    public sealed class SeqRule : TypeRewriteRule
    {
        private readonly List<TypeRewriteRule> _rules;
        public SeqRule(List<TypeRewriteRule> rules) => _rules = rules;
        public Optional<RewriteResult<A, object>> Rewrite<A>(Type<A> type)
        {
            RewriteResult<A, object> result = RewriteResult<A, object>.Nop(type);
            foreach (var rule in _rules)
            {
                var newResult = Cap1(rule, result);
                if (!newResult.IsPresent) return Optional<RewriteResult<A, object>>.Empty();
                result = newResult.Get();
            }
            return Optional<RewriteResult<A, object>>.Of(result);
        }
        //cap1把前一条结果f.view.newType作为下一条rule的输入结果与f复合
        //newType可能是NamedType<object>等Type<Pair<...>>子类C#严格泛型下不能强转Type<B>
        //用TypeObjectConverterFactory.AsObjectType包装对齐Java类型擦除语义
        //s运行时类型为RewriteResult<具体A,object>编译时RewriteResult<B,object>强转失败用Unsafe.As
        private static Optional<RewriteResult<A, object>> Cap1<A, B>(TypeRewriteRule rule, RewriteResult<A, B> f)
        {
            var newTypeObj = (object)TypeObjectConverterFactory.AsObjectType(f.View().NewType()!);
            var newTypeB = System.Runtime.CompilerServices.Unsafe.As<object, Type<B>>(ref newTypeObj);
            var newTypeOpt = rule.Rewrite(newTypeB);
            return newTypeOpt.Map(s =>
            {
                var sObj = (object)s;
                var sCast = System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<B, object>>(ref sObj);
                return ComposeResult<A, B, object>(sCast, f);
            });
        }
        //composeResult把后继s与前置f复合对齐原版RewriteResult.compose
        private static RewriteResult<A, object> ComposeResult<A, B, C>(RewriteResult<B, C> first, RewriteResult<A, B> second)
        {
            var composed = first.View().Compose(second.View());
            return RewriteResult<A, object>.Create(
                (View<A, object>)(object)composed,
                second.RecData());
        }
        //按规则列表结构比较让RewriteCacheKey命中缓存避免无限递归
        public override bool Equals(object? obj)
            => obj is SeqRule that && _rules.SequenceEqual(that._rules);
        public override int GetHashCode()
        {
            int hash = 0;
            foreach (var r in _rules) hash = unchecked(hash * 31 + (r?.GetHashCode() ?? 0));
            return hash;
        }
    }

    //OrElseRule分支规则
    public sealed class OrElseRule : TypeRewriteRule
    {
        private readonly TypeRewriteRule _first;
        private readonly Func<TypeRewriteRule> _second;
        public OrElseRule(TypeRewriteRule first, Func<TypeRewriteRule> second)
        {
            _first = first;
            _second = second;
        }
        public Optional<RewriteResult<A, object>> Rewrite<A>(Type<A> type)
        {
            var result = _first.Rewrite(type);
            if (result.IsPresent) return result;
            return _second().Rewrite(type);
        }
        //_second是工厂委托引用比较结构按_first比较
        public override bool Equals(object? obj)
            => obj is OrElseRule that && Equals(_first, that._first);
        public override int GetHashCode() => _first?.GetHashCode() ?? 0;
    }

    //AllRule对所有子类型应用规则
    public sealed class AllRule : TypeRewriteRule
    {
        private readonly TypeRewriteRule _rule;
        private readonly bool _recurse;
        private readonly bool _checkIndex;
        public AllRule(TypeRewriteRule rule, bool recurse, bool checkIndex)
        {
            _rule = rule;
            _recurse = recurse;
            _checkIndex = checkIndex;
        }
        public Optional<RewriteResult<A, object>> Rewrite<A>(Type<A> type)
            => Optional<RewriteResult<A, object>>.Of(type.All(_rule, _recurse, _checkIndex));
        //按rule+标志位结构比较
        public override bool Equals(object? obj)
            => obj is AllRule that && Equals(_rule, that._rule) && _recurse == that._recurse && _checkIndex == that._checkIndex;
        public override int GetHashCode() => unchecked(((_rule?.GetHashCode() ?? 0) * 31 + _recurse.GetHashCode()) * 31 + _checkIndex.GetHashCode());
    }

    //OneRule对唯一子类型应用规则
    public sealed record OneRule(TypeRewriteRule Rule) : TypeRewriteRule
    {
        public Optional<RewriteResult<A, object>> Rewrite<A>(Type<A> type)
            => type.One(Rule);
    }

    //EverywhereRule到处递归应用规则
    public sealed class EverywhereRule : TypeRewriteRule
    {
        private readonly TypeRewriteRule _rule;
        private readonly PointFreeRule _optimizationRule;
        private readonly bool _recurse;
        private readonly bool _checkIndex;
        public EverywhereRule(TypeRewriteRule rule, PointFreeRule optimizationRule, bool recurse, bool checkIndex)
        {
            _rule = rule;
            _optimizationRule = optimizationRule;
            _recurse = recurse;
            _checkIndex = checkIndex;
        }
        public Optional<RewriteResult<A, object>> Rewrite<A>(Type<A> type)
            => type.Everywhere(_rule, _optimizationRule, _recurse, _checkIndex);
        //按rule+opt+标志位结构比较让RewriteCacheKey命中
        public override bool Equals(object? obj)
            => obj is EverywhereRule that && Equals(_rule, that._rule) && Equals(_optimizationRule, that._optimizationRule)
                && _recurse == that._recurse && _checkIndex == that._checkIndex;
        public override int GetHashCode()
            => unchecked((((_rule?.GetHashCode() ?? 0) * 31 + (_optimizationRule?.GetHashCode() ?? 0)) * 31 + _recurse.GetHashCode()) * 31 + _checkIndex.GetHashCode());
    }

    //IfSameRule按目标类型匹配规则
    public sealed class IfSameRule<B> : TypeRewriteRule
    {
        private readonly Type<B> _targetType;
        private readonly RewriteResult<B, object> _value;
        public IfSameRule(Type<B> targetType, RewriteResult<B, object> value)
        {
            _targetType = targetType;
            _value = value;
        }
        public Optional<RewriteResult<A, object>> Rewrite<A>(Type<A> type)
            => type.IfSame(_targetType, _value);
        //按目标类型+值结构比较
        public override bool Equals(object? obj)
            => obj is IfSameRule<B> that && Equals(_targetType, that._targetType) && Equals(_value, that._value);
        public override int GetHashCode() => unchecked(((_targetType?.GetHashCode() ?? 0) * 31) + (_value?.GetHashCode() ?? 0));
    }

    //CheckOnceRule检查应用结果为nop时回调
    public sealed class CheckOnceRule : TypeRewriteRule
    {
        private readonly TypeRewriteRule _rule;
        private readonly Action<Type<object>> _onFail;
        public CheckOnceRule(TypeRewriteRule rule, Action<Type<object>> onFail)
        {
            _rule = rule;
            _onFail = onFail;
        }
        public Optional<RewriteResult<A, object>> Rewrite<A>(Type<A> type)
        {
            var result = _rule.Rewrite(type);
            if (!result.IsPresent || result.Get().View().IsNop())
            {
                _onFail((Type<object>)(object)type);
            }
            return result;
        }
        //_onFail委托引用比较rule结构比较
        public override bool Equals(object? obj)
            => obj is CheckOnceRule that && Equals(_rule, that._rule) && Equals(_onFail, that._onFail);
        public override int GetHashCode() => unchecked(((_rule?.GetHashCode() ?? 0) * 31) + (_onFail?.GetHashCode() ?? 0));
    }
}
