namespace NetCraft.DataFixer.Functions;

using System;
using System.Collections.Generic;
using NetCraft.Codec;
using NetCraft.DataFixer.Types.Families;
using NetCraft.Util;
using OpticsClass = NetCraft.DataFixer.Optics.Optics;

//PointFreeRule point-free function rewrite rule maps to vanilla com.mojang.datafixers.functions.PointFreeRule
//applies optimization transforms to PointFree
public abstract class PointFreeRule
{
    //rewrite applies the rule to a PointFree; provided by subclasses
    public abstract Optional<PointFree<T>> Rewrite<T>(PointFree<T> expr);

    //rewriteOrNop applies the rule and returns the original expression on failure
    public PointFree<T> RewriteOrNop<T>(PointFree<T> expr)
        => Rewrite(expr).OrElse(expr);

    //applyIfPresent applies the rule when present
    public Optional<PointFree<T>> ApplyIfPresent<T>(Optional<PointFree<T>> expr)
        => expr.IsPresent ? Rewrite<T>(expr.Get()) : Optional<PointFree<T>>.Empty();

    //view rule name
    public abstract string Name();

    //Nop empty rule returns the original expression directly
    public sealed class NopRule : PointFreeRule
    {
        public static readonly NopRule Instance = new();
        private NopRule() { }

        public override Optional<PointFree<T>> Rewrite<T>(PointFree<T> expr)
            => Optional<PointFree<T>>.Of(expr);

        public override string Name() => "nop";
    }

    //Seq applies multiple rules in order and returns the last result
    public sealed class SeqRule : PointFreeRule
    {
        private readonly PointFreeRule[] _rules;
        public SeqRule(PointFreeRule[] rules) => _rules = rules;
        public PointFreeRule[] Rules => _rules;

        public override Optional<PointFree<T>> Rewrite<T>(PointFree<T> expr)
        {
            PointFree<T> result = expr;
            foreach (var rule in _rules)
            {
                result = rule.RewriteOrNop(result);
            }
            return Optional<PointFree<T>>.Of(result);
        }

        public override string Name() => "seq";

        public override bool Equals(object? obj)
            => obj is SeqRule that && Array.Equals(_rules, that._rules);

        public override int GetHashCode() => _rules?.GetHashCode() ?? 0;
    }

    //Choice tries the rules in order and returns on the first match
    public sealed class ChoiceRule : PointFreeRule
    {
        private readonly PointFreeRule[] _rules;
        public ChoiceRule(PointFreeRule[] rules) => _rules = rules;
        public PointFreeRule[] Rules => _rules;

        public override Optional<PointFree<T>> Rewrite<T>(PointFree<T> expr)
        {
            foreach (var rule in _rules)
            {
                var view = rule.Rewrite(expr);
                if (view.IsPresent)
                {
                    return view;
                }
            }
            return Optional<PointFree<T>>.Empty();
        }

        public override string Name() => "choice";

        public override bool Equals(object? obj)
            => obj is ChoiceRule that && Array.Equals(_rules, that._rules);

        public override int GetHashCode() => _rules?.GetHashCode() ?? 0;
    }

    //All applies the rule to all children, delegating to expr.All
    public sealed class AllRule : PointFreeRule
    {
        private readonly PointFreeRule _rule;
        public AllRule(PointFreeRule rule) => _rule = rule;

        public override Optional<PointFree<T>> Rewrite<T>(PointFree<T> expr)
            => expr.All(_rule);

        public override string Name() => "all";
    }

    //One applies the rule to the single child, delegating to expr.One
    public sealed class OneRule : PointFreeRule
    {
        private readonly PointFreeRule _rule;
        public OneRule(PointFreeRule rule) => _rule = rule;

        public override Optional<PointFree<T>> Rewrite<T>(PointFree<T> expr)
            => expr.One(_rule);

        public override string Name() => "one";
    }

    //Once first tries a whole rewrite; on failure applies the rule once to the children
    public sealed class OnceRule : PointFreeRule
    {
        private readonly PointFreeRule _rule;
        public OnceRule(PointFreeRule rule) => _rule = rule;

        public override Optional<PointFree<T>> Rewrite<T>(PointFree<T> expr)
        {
            var view = _rule.Rewrite(expr);
            if (view.IsPresent)
            {
                return view;
            }
            return expr.One(this);
        }

        public override string Name() => "once";
    }

    //Many repeatedly applies the rule until it stops changing
    public sealed class ManyRule : PointFreeRule
    {
        private readonly PointFreeRule _rule;
        public ManyRule(PointFreeRule rule) => _rule = rule;

        public override Optional<PointFree<T>> Rewrite<T>(PointFree<T> expr)
        {
            Optional<PointFree<T>> result = Optional<PointFree<T>>.Of(expr);
            while (true)
            {
                if (!result.IsPresent)
                {
                    return result;
                }
                var newResult = _rule.Rewrite(result.Get());
                if (!newResult.IsPresent)
                {
                    return result;
                }
                result = newResult;
            }
        }

        public override string Name() => "many";
    }

    //Everywhere runs topDown, then recursive all, then bottomUp
    public sealed class EverywhereRule : PointFreeRule
    {
        private readonly PointFreeRule _topDown;
        private readonly PointFreeRule _bottomUp;
        public EverywhereRule(PointFreeRule topDown, PointFreeRule bottomUp)
        {
            _topDown = topDown;
            _bottomUp = bottomUp;
        }

        public override Optional<PointFree<T>> Rewrite<T>(PointFree<T> expr)
        {
            var topDown = _topDown.RewriteOrNop(expr);
            var all = topDown.All(this).OrElse(topDown);
            var bottomUp = _bottomUp.RewriteOrNop(all);
            return Optional<PointFree<T>>.Of(bottomUp);
        }

        public override string Name() => "everywhere";
    }

    //nop factory returns the NopRule singleton
    public static PointFreeRule Nop() => NopRule.Instance;

    //seq composes multiple rules in order
    public static PointFreeRule Seq(params PointFreeRule[] rules) => new SeqRule(rules);

    //choice tries each in order and returns the first match
    public static PointFreeRule Choice(params PointFreeRule[] rules)
    {
        if (rules.Length == 1) return rules[0];
        return new ChoiceRule(rules);
    }

    //all applies the rule to all children
    public static PointFreeRule All(PointFreeRule rule) => new AllRule(rule);

    //one applies the rule to the single child
    public static PointFreeRule One(PointFreeRule rule) => new OneRule(rule);

    //once applies to the whole then the children, matching only once
    public static PointFreeRule Once(PointFreeRule rule) => new OnceRule(rule);

    //many applies repeatedly until it stabilizes
    public static PointFreeRule Many(PointFreeRule rule) => new ManyRule(rule);

    //everywhere applies recursively via topDown+bottomUp
    public static PointFreeRule Everywhere(PointFreeRule topDown, PointFreeRule bottomUp)
        => new EverywhereRule(topDown, bottomUp);

    //BangEta unit eta expansion rule maps to vanilla PointFreeRule.BangEta
    //a Bang expression itself does not match Func<A,EmptyPart>; wrap the type as Bang<A>
    public sealed class BangEtaRule : PointFreeRule
    {
        public static readonly BangEtaRule Instance = new();
        private BangEtaRule() { }

        public override Optional<PointFree<T>> Rewrite<T>(PointFree<T> expr)
        {
            var exprType = expr.GetType();
            if (exprType.IsGenericType && exprType.GetGenericTypeDefinition() == typeof(Bang<>))
            {
                return Optional<PointFree<T>>.Empty();
            }
            var type = expr.Type();
            var typeType = type.GetType();
            if (!typeType.IsGenericType || typeType.GetGenericTypeDefinition() != typeof(NetCraft.DataFixer.Types.Func<,>))
            {
                return Optional<PointFree<T>>.Empty();
            }
            var firstMethod = typeType.GetMethod("First")!;
            var secondMethod = typeType.GetMethod("Second")!;
            var firstValue = firstMethod.Invoke(type, null)!;
            var secondValue = secondMethod.Invoke(type, null)!;
            if (secondValue is not NetCraft.DataFixer.Types.Constant.EmptyPart)
            {
                return Optional<PointFree<T>>.Empty();
            }
            var aTypeParam = typeType.GetGenericArguments()[0];
            var bangMethod = typeof(Functions).GetMethod("Bang")!.MakeGenericMethod(aTypeParam);
            return Optional<PointFree<T>>.Of((PointFree<T>)bangMethod.Invoke(null, new[] { firstValue })!);
        }

        public override string Name() => "bangEta";
    }

    //LensAppId simplifies ap lens id to id, maps to vanilla PointFreeRule.LensAppId
    //when Apply's func is a ProfunctorTransformer and arg is Id, replace with Functions.Id(first)
    public sealed class LensAppIdRule : PointFreeRule
    {
        public static readonly LensAppIdRule Instance = new();
        private LensAppIdRule() { }

        public override Optional<PointFree<T>> Rewrite<T>(PointFree<T> expr)
        {
            var exprType = expr.GetType();
            if (!exprType.IsGenericType || exprType.GetGenericTypeDefinition() != typeof(Apply<,>))
            {
                return Optional<PointFree<T>>.Empty();
            }
            var funcProp = exprType.GetProperty("Func")!;
            var argProp = exprType.GetProperty("Arg")!;
            var funcValue = funcProp.GetValue(expr);
            var argValue = argProp.GetValue(expr);
            if (funcValue is null || argValue is null) return Optional<PointFree<T>>.Empty();
            var funcType = funcValue.GetType();
            if (!funcType.IsGenericType || funcType.GetGenericTypeDefinition() != typeof(ProfunctorTransformer<,,,>))
            {
                return Optional<PointFree<T>>.Empty();
            }
            var argType = argValue.GetType();
            if (!argType.IsGenericType || argType.GetGenericTypeDefinition() != typeof(Id<>))
            {
                return Optional<PointFree<T>>.Empty();
            }
            var type = expr.Type();
            var typeType = type.GetType();
            if (!typeType.IsGenericType || typeType.GetGenericTypeDefinition() != typeof(NetCraft.DataFixer.Types.Func<,>))
            {
                return Optional<PointFree<T>>.Empty();
            }
            var firstMethod = typeType.GetMethod("First")!;
            var firstValue = firstMethod.Invoke(type, null)!;
            var aTypeParam = typeType.GetGenericArguments()[0];
            var idMethod = typeof(Functions).GetMethod("Id")!.MakeGenericMethod(aTypeParam);
            return Optional<PointFree<T>>.Of((PointFree<T>)idMethod.Invoke(null, new[] { firstValue })!);
        }

        public override string Name() => "lensAppId";
    }

    //AppNest ap nesting merge rule maps to vanilla PointFreeRule.AppNest
    //(ap f1 (ap f2 arg)) -> (ap (f1 ◦ f2) arg)
    //reflectively detects nested Apply whose two funcs are both ProfunctorTransformer; use Cap with optic.Compose, otherwise Functions.Comp
    public sealed class AppNestRule : PointFreeRule
    {
        public static readonly AppNestRule Instance = new();
        private AppNestRule() { }

        public override Optional<PointFree<T>> Rewrite<T>(PointFree<T> expr)
        {
            var exprType = expr.GetType();
            if (!exprType.IsGenericType || exprType.GetGenericTypeDefinition() != typeof(Apply<,>))
            {
                return Optional<PointFree<T>>.Empty();
            }
            var argProp = exprType.GetProperty("Arg")!;
            var firstArg = argProp.GetValue(expr);
            if (firstArg is null) return Optional<PointFree<T>>.Empty();
            var firstArgType = firstArg.GetType();
            if (!firstArgType.IsGenericType || firstArgType.GetGenericTypeDefinition() != typeof(Apply<,>))
            {
                return Optional<PointFree<T>>.Empty();
            }
            var funcProp = exprType.GetProperty("Func")!;
            var firstFunc = funcProp.GetValue(expr);
            var secondFuncProp = firstArgType.GetProperty("Func")!;
            var secondArgProp = firstArgType.GetProperty("Arg")!;
            var secondFunc = secondFuncProp.GetValue(firstArg);
            var secondArg = secondArgProp.GetValue(firstArg);
            if (firstFunc is null || secondFunc is null || secondArg is null)
            {
                return Optional<PointFree<T>>.Empty();
            }
            var composed = Compose(firstFunc, secondFunc);
            //construct Apply directly to skip the runtime type check of reflective Invoke, aligning with Java type erasure semantics
            var composedObj = composed;
            var composedCast = System.Runtime.CompilerServices.Unsafe.As<object, PointFree<Func<object, T>>>(ref composedObj);
            var secondArgObj = secondArg;
            var secondArgCast = System.Runtime.CompilerServices.Unsafe.As<object, PointFree<object>>(ref secondArgObj);
            var result = new Apply<object, T>(composedCast, secondArgCast);
            return Optional<PointFree<T>>.Of(result);
        }

        //Compose merges when both funcs are ProfunctorTransformer; use Cap, otherwise Functions.Comp
        //use Unsafe.As to bypass the reflective Invoke runtime type check on the PointFree generic parameter, aligning with Java type erasure semantics
        private static object Compose(object first, object second)
        {
            var firstType = first.GetType();
            var secondType = second.GetType();
            if (firstType.IsGenericType && firstType.GetGenericTypeDefinition() == typeof(ProfunctorTransformer<,,,>)
                && secondType.IsGenericType && secondType.GetGenericTypeDefinition() == typeof(ProfunctorTransformer<,,,>))
            {
                return Cap(first, second);
            }
            if (Functions.IsIdUnchecked(first)) return second;
            if (Functions.IsIdUnchecked(second)) return first;
            var firstObj = first;
            var secondObj = second;
            var firstFunc = System.Runtime.CompilerServices.Unsafe.As<object, PointFree<Func<object, object>>>(ref firstObj);
            var secondFunc = System.Runtime.CompilerServices.Unsafe.As<object, PointFree<Func<object, object>>>(ref secondObj);
            return Functions.Comp<object, object, object>(firstFunc, secondFunc);
        }

        //Cap merges two ProfunctorTransformers via optic.Compose and wraps the result back into a ProfunctorTransformer
        private static object Cap(object first, object second)
        {
            var firstOptic = first.GetType().GetProperty("Optic")!.GetValue(first);
            var secondOptic = second.GetType().GetProperty("Optic")!.GetValue(second);
            var secondOpticTypeArgs = secondOptic!.GetType().GetGenericArguments();
            var aType = secondOpticTypeArgs[^2];
            var bType = secondOpticTypeArgs[^1];
            var composeMethod = firstOptic!.GetType().GetMethod("Compose")!.MakeGenericMethod(aType, bType);
            var composedOptic = composeMethod.Invoke(firstOptic, new[] { secondOptic });
            var firstOpticTypeArgs = firstOptic.GetType().GetGenericArguments();
            var sType = firstOpticTypeArgs[0];
            var tType = firstOpticTypeArgs[1];
            var profunctorMethod = typeof(Functions).GetMethod("ProfunctorTransformer")!
                .MakeGenericMethod(sType, tType, aType, bType);
            return profunctorMethod.Invoke(null, new[] { composedOptic })!;
        }

        public override string Name() => "appNest";
    }

    //CompRewrite composite rewrite abstract base class maps to vanilla PointFreeRule.CompRewrite
    //DoRewrite rewrites adjacent functions of a Comp; provided by subclasses
    //the default Rewrite implementation processes the Comp chain, using LinkedList to emulate ArrayDeque's two-ended operations
    public abstract class CompRewrite : PointFreeRule
    {
        public abstract Optional<PointFree<Func<object, object>>> DoRewrite(
            PointFree<Func<object, object>> first,
            PointFree<Func<object, object>> second);

        //the default Rewrite implementation processes the Comp array, merging adjacent functions in order and writing replacements back to the queue
        public override Optional<PointFree<T>> Rewrite<T>(PointFree<T> expr)
        {
            var exprType = expr.GetType();
            if (!exprType.IsGenericType || exprType.GetGenericTypeDefinition() != typeof(Comp<,>))
            {
                return Optional<PointFree<T>>.Empty();
            }
            //Functions.Comp is a method, not a property, so use GetMethod reflective invocation to get object[]
            var funcMethod = exprType.GetMethod("Functions")!;
            var functions = (object[])funcMethod.Invoke(expr, null)!;
            var rewriteOpt = RewriteArray(functions);
            if (!rewriteOpt.IsPresent)
            {
                return Optional<PointFree<T>>.Empty();
            }
            var rewrite = rewriteOpt.Get();
            if (rewrite.Length == 1)
            {
                var rewriteObj = rewrite[0];
                var rewriteCast = System.Runtime.CompilerServices.Unsafe.As<object, PointFree<T>>(ref rewriteObj!);
                return Optional<PointFree<T>>.Of(rewriteCast);
            }
            //Comp<object,object> inherits PointFree<Func<object,object>>, aligning with vanilla Comp<?> after type erasure
            var compType = typeof(Comp<,>).MakeGenericType(typeof(object), typeof(object));
            //Activator.CreateInstance needs the argument explicitly wrapped in object[], otherwise rewrite is expanded as params
            return Optional<PointFree<T>>.Of((PointFree<T>)Activator.CreateInstance(compType, new object?[] { rewrite, null })!);
        }

        //RewriteArray uses a LinkedList two-ended queue to replace matched adjacent functions
        private Optional<object[]> RewriteArray(object[] functions)
        {
            var result = new LinkedList<object>();
            var queue = new LinkedList<object>(functions);
            var rewritten = false;
            while (queue.Count > 0)
            {
                var next = queue.First!.Value;
                queue.RemoveFirst();
                var last = result.Last?.Value;
                var rewriteOpt = last is not null
                    ? DoRewrite(AsPF(last), AsPF(next))
                    : Optional<PointFree<Func<object, object>>>.Empty();
                if (rewriteOpt.IsPresent)
                {
                    result.RemoveLast();
                    AddFirst(queue, rewriteOpt.Get());
                    rewritten = true;
                }
                else
                {
                    result.AddLast(next);
                }
            }
            return rewritten
                ? Optional<object[]>.Of(result.ToArray())
                : Optional<object[]>.Empty();
        }

        //AsPF uses Unsafe.As to treat object as PointFree<Func<object,object>>, aligning with Java type erasure
        private static PointFree<Func<object, object>> AsPF(object function)
        {
            var obj = function;
            return System.Runtime.CompilerServices.Unsafe.As<object, PointFree<Func<object, object>>>(ref obj);
        }

        //AddFirst splices an expanded Comp in reverse at the queue head; others go directly to the head, aligning with vanilla addFirst
        private static void AddFirst(LinkedList<object> queue, object function)
        {
            var funcType = function.GetType();
            if (funcType.IsGenericType && funcType.GetGenericTypeDefinition() == typeof(Comp<,>))
            {
                var funcMethod = funcType.GetMethod("Functions")!;
                var funcs = (object[])funcMethod.Invoke(function, null)!;
                for (int i = funcs.Length - 1; i >= 0; i--)
                {
                    queue.AddFirst(funcs[i]);
                }
            }
            else
            {
                queue.AddFirst(function);
            }
        }

        public static CompRewrite Together(params CompRewrite[] rules)
            => new TogetherRule(rules);
    }

    //TogetherRule tries multiple CompRewrites in order and returns the first match
    private sealed class TogetherRule : CompRewrite
    {
        private readonly CompRewrite[] _rules;
        public TogetherRule(CompRewrite[] rules) => _rules = rules;

        public override Optional<PointFree<Func<object, object>>> DoRewrite(
            PointFree<Func<object, object>> first,
            PointFree<Func<object, object>> second)
        {
            foreach (var rule in _rules)
            {
                var view = rule.DoRewrite(first, second);
                if (view.IsPresent) return view;
            }
            return Optional<PointFree<Func<object, object>>>.Empty();
        }

        public override string Name() => "together";
    }

    //SortProj reorders π1/π2, maps to vanilla PointFreeRule.SortProj
    //(ap π1 f)◦(ap π2 g) -> (ap π2 g)◦(ap π1 f)
    //swap first and second when first's outermost optic is Proj2 and second's is Proj1
    public sealed class SortProjRule : CompRewrite
    {
        public static readonly SortProjRule Instance = new();
        private SortProjRule() { }

        public override Optional<PointFree<Func<object, object>>> DoRewrite(
            PointFree<Func<object, object>> first,
            PointFree<Func<object, object>> second)
            => SortOptic(first, second, OpticsClass.IsProj2, OpticsClass.IsProj1, isSum: false);

        public override string Name() => "sortProj";
    }

    //SortInj reorders i1/i2, maps to vanilla PointFreeRule.SortInj
    //(ap i1 f)◦(ap i2 g) -> (ap i2 g)◦(ap i1 f)
    //swap first and second when first's outermost optic is Inj2 and second's is Inj1
    public sealed class SortInjRule : CompRewrite
    {
        public static readonly SortInjRule Instance = new();
        private SortInjRule() { }

        public override Optional<PointFree<Func<object, object>>> DoRewrite(
            PointFree<Func<object, object>> first,
            PointFree<Func<object, object>> second)
            => SortOptic(first, second, OpticsClass.IsInj2, OpticsClass.IsInj1, isSum: true);

        public override string Name() => "sortInj";
    }

    //SortOptic common swap logic reused by SortProj/SortInj
    //use reflection to get Apply.Func/Arg/Type and ProfunctorTransformer.Optic
    //use CastOuterUncheckedObject to change the outer type; DSL.AndObject/OrObject builds the new outer type
    //use Unsafe.As to cast-construct the new Apply and Comp, aligning with Java type erasure semantics
    private static Optional<PointFree<Func<object, object>>> SortOptic(
        PointFree<Func<object, object>> first,
        PointFree<Func<object, object>> second,
        Func<object, bool> firstCheck,
        Func<object, bool> secondCheck,
        bool isSum)
    {
        if (!TryGetApplyFuncArg(first, out var firstFunc, out var firstArg, out var firstType)) return Empty;
        if (!TryGetApplyFuncArg(second, out var secondFunc, out var secondArg, out var secondType)) return Empty;
        if (!IsProfunctorTransformer(firstFunc, out var firstOptic)) return Empty;
        if (!IsProfunctorTransformer(secondFunc, out var secondOptic)) return Empty;

        var firstOuter = firstOptic!.GetType().GetMethod("Outermost")!.Invoke(firstOptic, null);
        var secondOuter = secondOptic!.GetType().GetMethod("Outermost")!.Invoke(secondOptic, null);
        if (!firstCheck(firstOuter!)) return Empty;
        if (!secondCheck(secondOuter!)) return Empty;

        //input = secondType.First() (ProductType<A,B> or SumType<A,B>)
        //output = firstType.Second() (ProductType<A2,B2> or SumType<A2,B2>)
        var input = secondType!.GetType().GetMethod("First")!.Invoke(secondType, null);
        var output = firstType!.GetType().GetMethod("Second")!.Invoke(firstType, null);
        var inputSecond = input!.GetType().GetMethod("Second")!.Invoke(input, null);
        var outputFirst = output!.GetType().GetMethod("First")!.Invoke(output, null);

        //newOuter = DSL.AndObject/OrObject(outputFirst, inputSecond), maps to Pair<A2,B> or Either<A2,B>
        //receive with object because OrObject and AndObject return incompatible types, aligning with Java type-erasure Type<?> semantics
        object newOuter = isSum
            ? DSL.OrObject(outputFirst!, inputSecond!)
            : DSL.AndObject(outputFirst!, inputSecond!);

        //secondFunc.castOuterUnchecked(newOuter, output) -> ProfunctorTransformer<Pair<A2,B>, Pair<A2,B2>, B, B2>
        var newSecondFunc = secondFunc!.GetType().GetMethod("CastOuterUncheckedObject")!.Invoke(secondFunc, new object[] { newOuter, output });
        //firstFunc.castOuterUnchecked(input, newOuter) -> ProfunctorTransformer<Pair<A,B>, Pair<A2,B>, A, A2>
        var newFirstFunc = firstFunc!.GetType().GetMethod("CastOuterUncheckedObject")!.Invoke(firstFunc, new object[] { input, newOuter });

        //new Apply(newSecondFunc, secondArg) and new Apply(newFirstFunc, firstArg)
        var secondApply = NewApplyObject(newSecondFunc!, secondArg!);
        var firstApply = NewApplyObject(newFirstFunc!, firstArg!);

        //new Comp(secondApply, firstApply) composes in order, maps to vanilla new Comp<>(secondPart, firstPart)
        var comp = NewCompObject(secondApply, firstApply);
        return Optional<PointFree<Func<object, object>>>.Of((PointFree<Func<object, object>>)(object)comp);
    }

    //LensComp lens composition merge maps to vanilla PointFreeRule.LensComp
    //(ap lens f)◦(ap lens g) -> (ap lens (f ◦ g))
    //when two ProfunctorTransformers' optics share a common prefix, merge the prefix and fork the remainder
    public sealed class LensCompRule : CompRewrite
    {
        public static readonly LensCompRule Instance = new();
        private LensCompRule() { }

        public override Optional<PointFree<Func<object, object>>> DoRewrite(
            PointFree<Func<object, object>> first,
            PointFree<Func<object, object>> second)
        {
            if (!TryGetApplyFuncArg(first, out var firstFunc, out var firstArg, out _)) return Empty;
            if (!TryGetApplyFuncArg(second, out var secondFunc, out var secondArg, out _)) return Empty;
            if (!IsProfunctorTransformer(firstFunc, out var firstOptic)) return Empty;
            if (!IsProfunctorTransformer(secondFunc, out var secondOptic)) return Empty;

            //decompose both optics into Element lists to find the common prefix
            var firstElements = (System.Collections.IList)firstOptic!.GetType().GetProperty("Elements")!.GetValue(firstOptic)!;
            var secondElements = (System.Collections.IList)secondOptic!.GetType().GetProperty("Elements")!.GetValue(secondOptic)!;
            var prefixSize = FindCommonPrefix(firstElements, secondElements);
            if (prefixSize == 0) return Empty;

            //on full equality capApp(optic, capComp(arg1, arg2)) composes arg directly
            if (prefixSize == firstElements.Count && prefixSize == secondElements.Count)
            {
                var comp = NewCompObject(firstArg!, secondArg!);
                var appResult = CapApp(firstOptic!, comp);
                return Optional<PointFree<Func<object, object>>>.Of(AsPF(appResult));
            }

            //partial prefix: prefix + firstFork + secondFork
            var firstBounds = (System.Collections.IEnumerable)firstOptic!.GetType().GetProperty("Bounds")!.GetValue(firstOptic)!;
            var secondBounds = (System.Collections.IEnumerable)secondOptic!.GetType().GetProperty("Bounds")!.GetValue(secondOptic)!;
            var bounds = MergeBounds(firstBounds, secondBounds);

            var prefixElements = SubList(firstElements, 0, prefixSize);
            var firstForkElements = SubList(firstElements, prefixSize, firstElements.Count - prefixSize);
            var secondForkElements = SubList(secondElements, prefixSize, secondElements.Count - prefixSize);

            var prefixOptic = NewTypedOptic(bounds, prefixElements);
            var firstForkOptic = NewTypedOptic(bounds, firstForkElements);
            var secondForkOptic = NewTypedOptic(bounds, secondForkElements);

            var firstForkApp = CapApp(firstForkOptic, firstArg!);
            var secondForkApp = CapApp(secondForkOptic, secondArg!);
            var forkComp = NewCompObject(firstForkApp, secondForkApp);
            var result = CapApp(prefixOptic, forkComp);
            return Optional<PointFree<Func<object, object>>>.Of(AsPF(result));
        }

        //AsPF uses Unsafe.As to treat any PointFree as PointFree<Func<object,object>>, aligning with Java type erasure
        private static PointFree<Func<object, object>> AsPF(object function)
        {
            var obj = function;
            return System.Runtime.CompilerServices.Unsafe.As<object, PointFree<Func<object, object>>>(ref obj);
        }

        //FindCommonPrefix finds the longest prefix length where the optics match in two Element lists
        private static int FindCommonPrefix(System.Collections.IList first, System.Collections.IList second)
        {
            var size = Math.Min(first.Count, second.Count);
            for (int i = 0; i < size; i++)
            {
                var firstElem = first[i]!;
                var secondElem = second[i]!;
                var firstOpticProp = firstElem.GetType().GetProperty("Optic")!.GetValue(firstElem);
                var secondOpticProp = secondElem.GetType().GetProperty("Optic")!.GetValue(secondElem);
                if (!Equals(firstOpticProp, secondOpticProp)) return i;
            }
            return size;
        }

        //SubList takes elements [start, start+count) from an IList and builds a new List<object>
        private static List<object> SubList(System.Collections.IList source, int start, int count)
        {
            var result = new List<object>(count);
            for (int i = 0; i < count; i++) result.Add(source[start + i]!);
            return result;
        }

        //MergeBounds merges two bounds sequences into a HashSet<object>
        private static HashSet<object> MergeBounds(System.Collections.IEnumerable first, System.Collections.IEnumerable second)
        {
            var set = new HashSet<object>();
            foreach (var b in first) set.Add(b!);
            foreach (var b in second) set.Add(b!);
            return set;
        }

        //CapApp returns f directly when optic is empty, otherwise builds ProfunctorTransformer + Apply
        //maps to vanilla capApp new ProfunctorTransformer<>(optic).app(f)
        private static object CapApp(object optic, object arg)
        {
            var elements = (System.Collections.IList)optic.GetType().GetProperty("Elements")!.GetValue(optic)!;
            if (elements.Count == 0) return arg;
            var pt = NewProfunctorTransformer(optic);
            return NewApplyObject(pt, arg);
        }

        //NewTypedOptic reflectively constructs TypedOptic<object,object,object,object>(bounds, elements)
        private static object NewTypedOptic(HashSet<object> bounds, List<object> elements)
        {
            var typedOpticType = typeof(TypedOptic<,,,>)
                .MakeGenericType(typeof(object), typeof(object), typeof(object), typeof(object));
            return Activator.CreateInstance(typedOpticType, bounds, elements)!;
        }

        //NewProfunctorTransformer uses new directly, avoiding Activator failing to find the constructor by runtime type
        private static object NewProfunctorTransformer(object optic)
        {
            var opticObj = optic;
            var opticCast = System.Runtime.CompilerServices.Unsafe.As<object, TypedOptic<object, object, object, object>>(ref opticObj);
            return new ProfunctorTransformer<object, object, object, object>(opticCast);
        }

        public override string Name() => "lensComp";
    }

    //TryGetApplyFuncArg gets the Apply.Func/Arg/Type triple from a PointFree
    //returns false when not an Apply or when Func/Arg is null
    private static bool TryGetApplyFuncArg(
        PointFree<Func<object, object>> expr,
        out object func, out object arg, out object type)
    {
        func = null!; arg = null!; type = null!;
        var exprType = expr.GetType();
        if (!exprType.IsGenericType || exprType.GetGenericTypeDefinition() != typeof(Apply<,>)) return false;
        func = exprType.GetProperty("Func")!.GetValue(expr)!;
        arg = exprType.GetProperty("Arg")!.GetValue(expr)!;
        type = expr.GetType().GetMethod("Type")!.Invoke(expr, null)!;
        return func is not null && arg is not null;
    }

    //IsProfunctorTransformer checks whether func is a ProfunctorTransformer<,,,> and outputs its Optic property
    private static bool IsProfunctorTransformer(object func, out object optic)
    {
        optic = null!;
        var funcType = func.GetType();
        if (!funcType.IsGenericType || funcType.GetGenericTypeDefinition() != typeof(ProfunctorTransformer<,,,>)) return false;
        optic = funcType.GetProperty("Optic")!.GetValue(func)!;
        return optic is not null;
    }

    //NewApplyObject uses Unsafe.As to cast-construct Apply<object,object>, aligning with Java type erasure
    private static object NewApplyObject(object func, object arg)
    {
        var funcObj = func;
        var funcCast = System.Runtime.CompilerServices.Unsafe.As<object, PointFree<Func<object, object>>>(ref funcObj);
        var argObj = arg;
        var argCast = System.Runtime.CompilerServices.Unsafe.As<object, PointFree<object>>(ref argObj);
        return new Apply<object, object>(funcCast, argCast);
    }

    //NewCompObject builds a composite function via Functions.Comp<object,object,object>
    private static object NewCompObject(object first, object second)
    {
        var firstObj = first;
        var firstCast = System.Runtime.CompilerServices.Unsafe.As<object, PointFree<Func<object, object>>>(ref firstObj);
        var secondObj = second;
        var secondCast = System.Runtime.CompilerServices.Unsafe.As<object, PointFree<Func<object, object>>>(ref secondObj);
        return Functions.Comp<object, object, object>(firstCast, secondCast);
    }

    private static Optional<PointFree<Func<object, object>>> Empty
        => Optional<PointFree<Func<object, object>>>.Empty();

    //CataFuseSame fuses identical folds, maps to vanilla PointFreeRule.CataFuseSame
    //(fold g ◦ in) ◦ fold (f ◦ in) -> fold (g ◦ f ◦ in)
    //requires firstFold and secondFold to share the same family and index, and at most one modification per index
    public sealed class CataFuseSameRule : CompRewrite
    {
        public static readonly CataFuseSameRule Instance = new();
        private CataFuseSameRule() { }

        public override Optional<PointFree<Func<object, object>>> DoRewrite(
            PointFree<Func<object, object>> first,
            PointFree<Func<object, object>> second)
        {
            if (first is not Fold<object, object> firstFold || second is not Fold<object, object> secondFold)
            {
                return Optional<PointFree<Func<object, object>>>.Empty();
            }
            var family = firstFold.AType.Family();
            if (firstFold.Index != secondFold.Index || !Equals(family, secondFold.AType.Family()))
            {
                return Optional<PointFree<Func<object, object>>>.Empty();
            }
            var newFamily = firstFold.BType.Family();
            var newAlgebra = new List<RewriteResult<object, object>>();
            var foundOne = false;
            for (int i = 0; i < family.Size(); i++)
            {
                var firstAlgFunc = firstFold.Algebra.Apply(i);
                var secondAlgFunc = secondFold.Algebra.Apply(i);
                var firstId = firstAlgFunc.View().IsNop();
                var secondId = secondAlgFunc.View().IsNop();
                if (firstId && secondId)
                {
                    newAlgebra.Add(firstAlgFunc);
                }
                else if (!foundOne && !firstId && !secondId)
                {
                    newAlgebra.Add(GetCompose(firstAlgFunc, secondAlgFunc));
                    foundOne = true;
                }
                else
                {
                    return Optional<PointFree<Func<object, object>>>.Empty();
                }
            }
            var algebra = new ListAlgebra("FusedSame", newAlgebra);
            var function = family.Fold(algebra, newFamily)(firstFold.Index).View().Function!;
            return Optional<PointFree<Func<object, object>>>.Of((PointFree<Func<object, object>>)(object)function);
        }

        //GetCompose firstAlgFunc.Compose(secondAlgFunc) connects second's output to first's input, aligning with vanilla
        private static RewriteResult<object, object> GetCompose(
            RewriteResult<object, object> firstAlgFunc,
            RewriteResult<object, object> secondAlgFunc)
            => firstAlgFunc.Compose(secondAlgFunc);

        public override string Name() => "cataFuseSame";
    }

    //CataFuseDifferent fuses different folds, maps to vanilla PointFreeRule.CataFuseDifferent
    //(fold g ◦ in) ◦ fold (f ◦ in) -> fold (g ◦ f ◦ in)
    //requires the two folds not to modify the same index and their recData to be disjoint
    public sealed class CataFuseDifferentRule : CompRewrite
    {
        public static readonly CataFuseDifferentRule Instance = new();
        private CataFuseDifferentRule() { }

        public override Optional<PointFree<Func<object, object>>> DoRewrite(
            PointFree<Func<object, object>> first,
            PointFree<Func<object, object>> second)
        {
            if (first is not Fold<object, object> firstFold || second is not Fold<object, object> secondFold)
            {
                return Optional<PointFree<Func<object, object>>>.Empty();
            }
            var family = firstFold.AType.Family();
            if (firstFold.Index != secondFold.Index || !Equals(family, secondFold.AType.Family()))
            {
                return Optional<PointFree<Func<object, object>>>.Empty();
            }
            var newFamily = firstFold.BType.Family();
            var newAlgebra = new List<RewriteResult<object, object>>();
            var firstModifies = new BitSet(family.Size());
            var secondModifies = new BitSet(family.Size());
            for (int i = 0; i < family.Size(); i++)
            {
                var firstAlgFunc = firstFold.Algebra.Apply(i);
                var secondAlgFunc = secondFold.Algebra.Apply(i);
                var firstId = firstAlgFunc.View().IsNop();
                var secondId = secondAlgFunc.View().IsNop();
                if (!firstId && !secondId)
                {
                    return Optional<PointFree<Func<object, object>>>.Empty();
                }
                firstModifies.Set(i, !firstId);
                secondModifies.Set(i, !secondId);
            }
            for (int i = 0; i < family.Size(); i++)
            {
                var firstAlgFunc = firstFold.Algebra.Apply(i);
                var secondAlgFunc = secondFold.Algebra.Apply(i);
                if (firstAlgFunc.RecData().Intersects(secondModifies) || secondAlgFunc.RecData().Intersects(firstModifies))
                {
                    return Optional<PointFree<Func<object, object>>>.Empty();
                }
                if (firstAlgFunc.View().IsNop())
                {
                    newAlgebra.Add(secondAlgFunc);
                }
                else
                {
                    newAlgebra.Add(firstAlgFunc);
                }
            }
            var algebra = new ListAlgebra("FusedDifferent", newAlgebra);
            var function = family.Fold(algebra, newFamily)(firstFold.Index).View().Function!;
            return Optional<PointFree<Func<object, object>>>.Of((PointFree<Func<object, object>>)(object)function);
        }

        public override string Name() => "cataFuseDifferent";
    }

    //BangEta factory returns the singleton
    public static PointFreeRule BangEta() => BangEtaRule.Instance;

    //LensAppId factory returns the singleton
    public static PointFreeRule LensAppId() => LensAppIdRule.Instance;

    //AppNest factory returns a singleton placeholder
    public static PointFreeRule AppNest() => AppNestRule.Instance;

    //SortProj factory returns a singleton placeholder
    public static CompRewrite SortProj() => SortProjRule.Instance;

    //SortInj factory returns a singleton placeholder
    public static CompRewrite SortInj() => SortInjRule.Instance;

    //LensComp factory returns a singleton placeholder
    public static CompRewrite LensComp() => LensCompRule.Instance;

    //CataFuseSame factory returns a singleton placeholder
    public static CompRewrite CataFuseSame() => CataFuseSameRule.Instance;

    //CataFuseDifferent factory returns a singleton placeholder
    public static CompRewrite CataFuseDifferent() => CataFuseDifferentRule.Instance;
}
