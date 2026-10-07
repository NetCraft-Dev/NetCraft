namespace NetCraft.DataFixer;

using System;
using System.Collections.Generic;
using System.Linq;
using NetCraft.Codec;
using NetCraft.DataFixer.Functions;
using NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Util;

//TypeRewriteRule type rewrite rule maps to vanilla TypeRewriteRule
//applies a rule to Type<A>, returning Optional<RewriteResult<A,?>>
public interface TypeRewriteRule
{
    //rewrite applies the rule to type
    Optional<RewriteResult<A, object>> Rewrite<A>(Type<A> type);

    //nop no-op rule singleton
    static TypeRewriteRule Nop() => NopRule.INSTANCE;

    //seq composes multiple rules, applied in order
    static TypeRewriteRule Seq(List<TypeRewriteRule> rules) => new SeqRule(rules);

    //seq composes two rules
    static TypeRewriteRule Seq(TypeRewriteRule first, TypeRewriteRule second)
        => ReferenceEquals(first, Nop()) ? second
            : ReferenceEquals(second, Nop()) ? first
            : Seq(new List<TypeRewriteRule> { first, second });

    //seq composes the first with a params array
    static TypeRewriteRule Seq(TypeRewriteRule firstRule, params TypeRewriteRule[] rules)
    {
        TypeRewriteRule rule = firstRule;
        foreach (var r in rules)
        {
            rule = Seq(rule, r);
        }
        return rule;
    }

    //orElse applies the second when the first fails
    static TypeRewriteRule OrElse(TypeRewriteRule first, TypeRewriteRule second)
        => new OrElseRule(first, () => second);

    //orElse lazily applies the second when the first fails
    static TypeRewriteRule OrElse(TypeRewriteRule first, Func<TypeRewriteRule> second)
        => new OrElseRule(first, second);

    //all applies the rule to all child types
    static TypeRewriteRule All(TypeRewriteRule rule, bool recurse, bool checkIndex)
        => new AllRule(rule, recurse, checkIndex);

    //one applies the rule to the single child type
    static TypeRewriteRule One(TypeRewriteRule rule) => new OneRule(rule);

    //once applies once; on failure falls back to recursive one
    static TypeRewriteRule Once(TypeRewriteRule rule)
        => OrElse(rule, () => One(Once(rule)));

    //checkOnce verifies the result and invokes the callback on nop
    static TypeRewriteRule CheckOnce(TypeRewriteRule rule, Action<Type<object>> onFail)
        => new CheckOnceRule(rule, onFail);

    //everywhere applies the rule recursively everywhere
    static TypeRewriteRule Everywhere(TypeRewriteRule rule, PointFreeRule optimizationRule, bool recurse, bool checkIndex)
        => new EverywhereRule(rule, optimizationRule, recurse, checkIndex);

    //ifSame returns value when the target type matches
    static TypeRewriteRule IfSame<B>(Type<B> targetType, RewriteResult<B, object> value)
        => new IfSameRule<B>(targetType, value);

    //NopRule no-op rule implementation
    public sealed class NopRule : TypeRewriteRule
    {
        public static readonly NopRule INSTANCE = new();
        private NopRule() { }
        public Optional<RewriteResult<A, object>> Rewrite<A>(Type<A> type)
            => Optional<RewriteResult<A, object>>.Of(RewriteResult<A, object>.Nop(type));
        //singleton comparison; all NopRule instances are equal
        public override bool Equals(object? obj) => obj is NopRule;
        public override int GetHashCode() => typeof(NopRule).GetHashCode();
    }

    //SeqRule sequential composition; the previous result's newType is fed as the next input, aligning with vanilla cap1 chaining
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
        //cap1 uses the previous result's f.view.newType as the next rule's input and composes the result with f
        //newType may be a Type<Pair<...>> subclass such as NamedType<object>; under C# strict generics it cannot be cast to Type<B>
        //wrap with TypeObjectConverterFactory.AsObjectType, aligning with Java type erasure semantics
        //at runtime s is RewriteResult<concrete A,object> but is RewriteResult<B,object> at compile time; the cast fails, so use Unsafe.As
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
        //composeResult composes the successor s with the predecessor f, aligning with vanilla RewriteResult.compose
        private static RewriteResult<A, object> ComposeResult<A, B, C>(RewriteResult<B, C> first, RewriteResult<A, B> second)
        {
            var composed = first.View().Compose(second.View());
            return RewriteResult<A, object>.Create(
                (View<A, object>)(object)composed,
                second.RecData());
        }
        //compares by rule-list structure so RewriteCacheKey hits the cache and avoids infinite recursion
        public override bool Equals(object? obj)
            => obj is SeqRule that && _rules.SequenceEqual(that._rules);
        public override int GetHashCode()
        {
            int hash = 0;
            foreach (var r in _rules) hash = unchecked(hash * 31 + (r?.GetHashCode() ?? 0));
            return hash;
        }
    }

    //OrElseRule branch rule
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
        //_second is a factory delegate; the reference is compared, and structure is compared by _first
        public override bool Equals(object? obj)
            => obj is OrElseRule that && Equals(_first, that._first);
        public override int GetHashCode() => _first?.GetHashCode() ?? 0;
    }

    //AllRule applies the rule to all child types
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
        //compares by rule + flag structure
        public override bool Equals(object? obj)
            => obj is AllRule that && Equals(_rule, that._rule) && _recurse == that._recurse && _checkIndex == that._checkIndex;
        public override int GetHashCode() => unchecked(((_rule?.GetHashCode() ?? 0) * 31 + _recurse.GetHashCode()) * 31 + _checkIndex.GetHashCode());
    }

    //OneRule applies the rule to the single child type
    public sealed record OneRule(TypeRewriteRule Rule) : TypeRewriteRule
    {
        public Optional<RewriteResult<A, object>> Rewrite<A>(Type<A> type)
            => type.One(Rule);
    }

    //EverywhereRule applies the rule recursively everywhere
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
        //compares by rule + opt + flag structure so RewriteCacheKey hits
        public override bool Equals(object? obj)
            => obj is EverywhereRule that && Equals(_rule, that._rule) && Equals(_optimizationRule, that._optimizationRule)
                && _recurse == that._recurse && _checkIndex == that._checkIndex;
        public override int GetHashCode()
            => unchecked((((_rule?.GetHashCode() ?? 0) * 31 + (_optimizationRule?.GetHashCode() ?? 0)) * 31 + _recurse.GetHashCode()) * 31 + _checkIndex.GetHashCode());
    }

    //IfSameRule matches by the target type
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
        //compares by target type + value structure
        public override bool Equals(object? obj)
            => obj is IfSameRule<B> that && Equals(_targetType, that._targetType) && Equals(_value, that._value);
        public override int GetHashCode() => unchecked(((_targetType?.GetHashCode() ?? 0) * 31) + (_value?.GetHashCode() ?? 0));
    }

    //CheckOnceRule verifies the result and invokes the callback on nop
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
        //_onFail is a delegate; the reference is compared, and rule structure is compared
        public override bool Equals(object? obj)
            => obj is CheckOnceRule that && Equals(_rule, that._rule) && Equals(_onFail, that._onFail);
        public override int GetHashCode() => unchecked(((_rule?.GetHashCode() ?? 0) * 31) + (_onFail?.GetHashCode() ?? 0));
    }
}
