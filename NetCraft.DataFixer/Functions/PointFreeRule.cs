namespace NetCraft.DataFixer.Functions;

using System;
using System.Collections.Generic;
using NetCraft.Codec;
using NetCraft.DataFixer.Types.Families;
using NetCraft.Util;
using OpticsClass = NetCraft.DataFixer.Optics.Optics;

//PointFreeRule无点函数重写规则对应原版com.mojang.datafixers.functions.PointFreeRule
//对PointFree应用优化变换
public abstract class PointFreeRule
{
    //rewrite对PointFree应用规则子类提供
    public abstract Optional<PointFree<T>> Rewrite<T>(PointFree<T> expr);

    //rewriteOrNop应用规则失败返回原表达式
    public PointFree<T> RewriteOrNop<T>(PointFree<T> expr)
        => Rewrite(expr).OrElse(expr);

    //applyIfPresent非空时应用规则
    public Optional<PointFree<T>> ApplyIfPresent<T>(Optional<PointFree<T>> expr)
        => expr.IsPresent ? Rewrite<T>(expr.Get()) : Optional<PointFree<T>>.Empty();

    //view规则名
    public abstract string Name();

    //Nop空规则直接返回原表达式
    public sealed class NopRule : PointFreeRule
    {
        public static readonly NopRule Instance = new();
        private NopRule() { }

        public override Optional<PointFree<T>> Rewrite<T>(PointFree<T> expr)
            => Optional<PointFree<T>>.Of(expr);

        public override string Name() => "nop";
    }

    //Seq顺序应用多个规则返回最后一个结果
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

    //Choice按顺序尝试规则首个命中即返回
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

    //All对全部子项应用规则委托到expr.All
    public sealed class AllRule : PointFreeRule
    {
        private readonly PointFreeRule _rule;
        public AllRule(PointFreeRule rule) => _rule = rule;

        public override Optional<PointFree<T>> Rewrite<T>(PointFree<T> expr)
            => expr.All(_rule);

        public override string Name() => "all";
    }

    //One对唯一子项应用规则委托到expr.One
    public sealed class OneRule : PointFreeRule
    {
        private readonly PointFreeRule _rule;
        public OneRule(PointFreeRule rule) => _rule = rule;

        public override Optional<PointFree<T>> Rewrite<T>(PointFree<T> expr)
            => expr.One(_rule);

        public override string Name() => "one";
    }

    //Once先尝试整体重写失败则对子项应用规则一次
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

    //Many反复应用规则直到不再变化
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

    //Everywhere先topDown后递归all再bottomUp
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

    //nop工厂返回NopRule单例
    public static PointFreeRule Nop() => NopRule.Instance;

    //seq顺序组合多个规则
    public static PointFreeRule Seq(params PointFreeRule[] rules) => new SeqRule(rules);

    //choice按序尝试首个命中
    public static PointFreeRule Choice(params PointFreeRule[] rules)
    {
        if (rules.Length == 1) return rules[0];
        return new ChoiceRule(rules);
    }

    //all对所有子项应用规则
    public static PointFreeRule All(PointFreeRule rule) => new AllRule(rule);

    //one对唯一子项应用规则
    public static PointFreeRule One(PointFreeRule rule) => new OneRule(rule);

    //once先整体后子项只命中一次
    public static PointFreeRule Once(PointFreeRule rule) => new OnceRule(rule);

    //many反复应用直到稳定
    public static PointFreeRule Many(PointFreeRule rule) => new ManyRule(rule);

    //everywhere递归到处应用topDown+bottomUp
    public static PointFreeRule Everywhere(PointFreeRule topDown, PointFreeRule bottomUp)
        => new EverywhereRule(topDown, bottomUp);

    //BangEta单元 eta 展开规则对应原版 PointFreeRule.BangEta
    //Bang 表达式自身不匹配Func<A,EmptyPart> 类型包装为 Bang<A>
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

    //LensAppId ap lens id 化简为 id 对应原版 PointFreeRule.LensAppId
    //Apply 的 func 是 ProfunctorTransformer 且 arg 是 Id 时替换为 Functions.Id(first)
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

    //AppNest ap 嵌套合并规则对应原版 PointFreeRule.AppNest
    //(ap f1 (ap f2 arg)) -> (ap (f1 ◦ f2) arg)
    //反射判断Apply嵌套两个func都是ProfunctorTransformer时走Cap用optic.Compose否则走Functions.Comp
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
            //直接new Apply跳过反射Invoke运行时类型检查对齐Java类型擦除语义
            var composedObj = composed;
            var composedCast = System.Runtime.CompilerServices.Unsafe.As<object, PointFree<Func<object, T>>>(ref composedObj);
            var secondArgObj = secondArg;
            var secondArgCast = System.Runtime.CompilerServices.Unsafe.As<object, PointFree<object>>(ref secondArgObj);
            var result = new Apply<object, T>(composedCast, secondArgCast);
            return Optional<PointFree<T>>.Of(result);
        }

        //Compose 合并两个func都是ProfunctorTransformer时走Cap否则走Functions.Comp
        //用Unsafe.As绕过反射Invoke对PointFree泛型参数的运行时类型检查对齐Java类型擦除语义
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

        //Cap 通过optic.Compose合并两个ProfunctorTransformer再包装回ProfunctorTransformer
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

    //CompRewrite 组合重写抽象基类对应原版 PointFreeRule.CompRewrite
    //对 Comp 的相邻 function 做 DoRewrite 重写子类提供
    //Rewrite 默认实现处理 Comp 链路用 LinkedList 模拟 ArrayDeque 双向操作
    public abstract class CompRewrite : PointFreeRule
    {
        public abstract Optional<PointFree<Func<object, object>>> DoRewrite(
            PointFree<Func<object, object>> first,
            PointFree<Func<object, object>> second);

        //Rewrite 默认实现处理 Comp 数组按序合并相邻function命中则替换回写到队列
        public override Optional<PointFree<T>> Rewrite<T>(PointFree<T> expr)
        {
            var exprType = expr.GetType();
            if (!exprType.IsGenericType || exprType.GetGenericTypeDefinition() != typeof(Comp<,>))
            {
                return Optional<PointFree<T>>.Empty();
            }
            //Functions是Comp方法非属性用GetMethod反射调用拿object[]
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
            //Comp<object,object>继承PointFree<Func<object,object>>对齐原版类型擦除后Comp<?>语义
            var compType = typeof(Comp<,>).MakeGenericType(typeof(object), typeof(object));
            //Activator.CreateInstance参数需显式包object[]避免rewrite被当params展开
            return Optional<PointFree<T>>.Of((PointFree<T>)Activator.CreateInstance(compType, new object?[] { rewrite, null })!);
        }

        //RewriteArray 用 LinkedList 双向队列处理相邻function命中替换
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

        //AsPF用Unsafe.As把object当PointFree<Func<object,object>>用对齐Java类型擦除
        private static PointFree<Func<object, object>> AsPF(object function)
        {
            var obj = function;
            return System.Runtime.CompilerServices.Unsafe.As<object, PointFree<Func<object, object>>>(ref obj);
        }

        //AddFirst Comp 展开逆序加入队列头部其他直接加头部对齐原版 addFirst
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

    //TogetherRule 多个 CompRewrite 按序尝试首个命中
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

    //SortProj π1/π2 顺序重排对应原版 PointFreeRule.SortProj
    //(ap π1 f)◦(ap π2 g) -> (ap π2 g)◦(ap π1 f)
    //first 最外层 optic 是 Proj2 second 最外层 optic 是 Proj1 时交换两者顺序
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

    //SortInj i1/i2 顺序重排对应原版 PointFreeRule.SortInj
    //(ap i1 f)◦(ap i2 g) -> (ap i2 g)◦(ap i1 f)
    //first 最外层 optic 是 Inj2 second 最外层 optic 是 Inj1 时交换两者顺序
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

    //SortOptic 公共交换逻辑供 SortProj/SortInj 复用
    //用反射拿 Apply.Func/Arg/Type 与 ProfunctorTransformer.Optic
    //用 CastOuterUncheckedObject 改外层类型 DSL.AndObject/OrObject 构造新外层
    //用 Unsafe.As 强转构造新 Apply 与 Comp 对齐 Java 类型擦除语义
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

        //input = secondType.First() (ProductType<A,B> 或 SumType<A,B>)
        //output = firstType.Second() (ProductType<A2,B2> 或 SumType<A2,B2>)
        var input = secondType!.GetType().GetMethod("First")!.Invoke(secondType, null);
        var output = firstType!.GetType().GetMethod("Second")!.Invoke(firstType, null);
        var inputSecond = input!.GetType().GetMethod("Second")!.Invoke(input, null);
        var outputFirst = output!.GetType().GetMethod("First")!.Invoke(output, null);

        //newOuter = DSL.AndObject/OrObject(outputFirst, inputSecond) 对应 Pair<A2,B> 或 Either<A2,B>
        //用 object 接收因 OrObject 与 AndObject 返回类型不兼容对齐 Java 类型擦除后 Type<?> 语义
        object newOuter = isSum
            ? DSL.OrObject(outputFirst!, inputSecond!)
            : DSL.AndObject(outputFirst!, inputSecond!);

        //secondFunc.castOuterUnchecked(newOuter, output) -> ProfunctorTransformer<Pair<A2,B>, Pair<A2,B2>, B, B2>
        var newSecondFunc = secondFunc!.GetType().GetMethod("CastOuterUncheckedObject")!.Invoke(secondFunc, new object[] { newOuter, output });
        //firstFunc.castOuterUnchecked(input, newOuter) -> ProfunctorTransformer<Pair<A,B>, Pair<A2,B>, A, A2>
        var newFirstFunc = firstFunc!.GetType().GetMethod("CastOuterUncheckedObject")!.Invoke(firstFunc, new object[] { input, newOuter });

        //new Apply(newSecondFunc, secondArg) 与 new Apply(newFirstFunc, firstArg)
        var secondApply = NewApplyObject(newSecondFunc!, secondArg!);
        var firstApply = NewApplyObject(newFirstFunc!, firstArg!);

        //new Comp(secondApply, firstApply) 复合顺序对应原版 new Comp<>(secondPart, firstPart)
        var comp = NewCompObject(secondApply, firstApply);
        return Optional<PointFree<Func<object, object>>>.Of((PointFree<Func<object, object>>)(object)comp);
    }

    //LensComp lens 复合合并对应原版 PointFreeRule.LensComp
    //(ap lens f)◦(ap lens g) -> (ap lens (f ◦ g))
    //两个 ProfunctorTransformer 的 optic 有公共前缀时合并前缀与 fork 后段
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

            //分解两个 optic 为 Element 列表找公共前缀
            var firstElements = (System.Collections.IList)firstOptic!.GetType().GetProperty("Elements")!.GetValue(firstOptic)!;
            var secondElements = (System.Collections.IList)secondOptic!.GetType().GetProperty("Elements")!.GetValue(secondOptic)!;
            var prefixSize = FindCommonPrefix(firstElements, secondElements);
            if (prefixSize == 0) return Empty;

            //全等时 capApp(optic, capComp(arg1, arg2)) 直接复合 arg
            if (prefixSize == firstElements.Count && prefixSize == secondElements.Count)
            {
                var comp = NewCompObject(firstArg!, secondArg!);
                var appResult = CapApp(firstOptic!, comp);
                return Optional<PointFree<Func<object, object>>>.Of(AsPF(appResult));
            }

            //部分前缀 prefix + firstFork + secondFork
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

        //AsPF用Unsafe.As把任意PointFree当PointFree<Func<object,object>>用对齐Java类型擦除
        private static PointFree<Func<object, object>> AsPF(object function)
        {
            var obj = function;
            return System.Runtime.CompilerServices.Unsafe.As<object, PointFree<Func<object, object>>>(ref obj);
        }

        //FindCommonPrefix 找两个 Element 列表中 optic 相同的最长前缀长度
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

        //SubList 取 IList 中 [start, start+count) 的元素构造新 List<object>
        private static List<object> SubList(System.Collections.IList source, int start, int count)
        {
            var result = new List<object>(count);
            for (int i = 0; i < count; i++) result.Add(source[start + i]!);
            return result;
        }

        //MergeBounds 合并两个 bounds 序列为 HashSet<object>
        private static HashSet<object> MergeBounds(System.Collections.IEnumerable first, System.Collections.IEnumerable second)
        {
            var set = new HashSet<object>();
            foreach (var b in first) set.Add(b!);
            foreach (var b in second) set.Add(b!);
            return set;
        }

        //CapApp optic 为空时直接返回 f 否则构造 ProfunctorTransformer + Apply
        //对应原版 capApp new ProfunctorTransformer<>(optic).app(f)
        private static object CapApp(object optic, object arg)
        {
            var elements = (System.Collections.IList)optic.GetType().GetProperty("Elements")!.GetValue(optic)!;
            if (elements.Count == 0) return arg;
            var pt = NewProfunctorTransformer(optic);
            return NewApplyObject(pt, arg);
        }

        //NewTypedOptic 用反射构造 TypedOptic<object,object,object,object>(bounds, elements)
        private static object NewTypedOptic(HashSet<object> bounds, List<object> elements)
        {
            var typedOpticType = typeof(TypedOptic<,,,>)
                .MakeGenericType(typeof(object), typeof(object), typeof(object), typeof(object));
            return Activator.CreateInstance(typedOpticType, bounds, elements)!;
        }

        //NewProfunctorTransformer直接new避免Activator按运行时类型查构造函数失败
        private static object NewProfunctorTransformer(object optic)
        {
            var opticObj = optic;
            var opticCast = System.Runtime.CompilerServices.Unsafe.As<object, TypedOptic<object, object, object, object>>(ref opticObj);
            return new ProfunctorTransformer<object, object, object, object>(opticCast);
        }

        public override string Name() => "lensComp";
    }

    //TryGetApplyFuncArg 从 PointFree 拿 Apply.Func/Arg/Type 三元组
    //非 Apply 或 Func/Arg 为 null 时返回 false
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

    //IsProfunctorTransformer 检查 func 是否是 ProfunctorTransformer<,,,> 并输出其 Optic 属性
    private static bool IsProfunctorTransformer(object func, out object optic)
    {
        optic = null!;
        var funcType = func.GetType();
        if (!funcType.IsGenericType || funcType.GetGenericTypeDefinition() != typeof(ProfunctorTransformer<,,,>)) return false;
        optic = funcType.GetProperty("Optic")!.GetValue(func)!;
        return optic is not null;
    }

    //NewApplyObject 用 Unsafe.As 强转构造 Apply<object,object> 对齐 Java 类型擦除
    private static object NewApplyObject(object func, object arg)
    {
        var funcObj = func;
        var funcCast = System.Runtime.CompilerServices.Unsafe.As<object, PointFree<Func<object, object>>>(ref funcObj);
        var argObj = arg;
        var argCast = System.Runtime.CompilerServices.Unsafe.As<object, PointFree<object>>(ref argObj);
        return new Apply<object, object>(funcCast, argCast);
    }

    //NewCompObject 用 Functions.Comp<object,object,object> 构造复合函数
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

    //CataFuseSame 相同 fold 融合对应原版 PointFreeRule.CataFuseSame
    //(fold g ◦ in) ◦ fold (f ◦ in) -> fold (g ◦ f ◦ in)
    //要求 firstFold 与 secondFold 同 family 同 index 且每个 index 最多一处同时修改
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

        //GetCompose firstAlgFunc.Compose(secondAlgFunc) 把 second 输出接 first 输入对齐原版
        private static RewriteResult<object, object> GetCompose(
            RewriteResult<object, object> firstAlgFunc,
            RewriteResult<object, object> secondAlgFunc)
            => firstAlgFunc.Compose(secondAlgFunc);

        public override string Name() => "cataFuseSame";
    }

    //CataFuseDifferent 不同 fold 融合对应原版 PointFreeRule.CataFuseDifferent
    //(fold g ◦ in) ◦ fold (f ◦ in) -> fold (g ◦ f ◦ in)
    //要求两个 fold 不修改同一 index 且 recData 不相交
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

    //BangEta 工厂返回单例
    public static PointFreeRule BangEta() => BangEtaRule.Instance;

    //LensAppId 工厂返回单例
    public static PointFreeRule LensAppId() => LensAppIdRule.Instance;

    //AppNest 工厂返回单例占位
    public static PointFreeRule AppNest() => AppNestRule.Instance;

    //SortProj 工厂返回单例占位
    public static CompRewrite SortProj() => SortProjRule.Instance;

    //SortInj 工厂返回单例占位
    public static CompRewrite SortInj() => SortInjRule.Instance;

    //LensComp 工厂返回单例占位
    public static CompRewrite LensComp() => LensCompRule.Instance;

    //CataFuseSame 工厂返回单例占位
    public static CompRewrite CataFuseSame() => CataFuseSameRule.Instance;

    //CataFuseDifferent 工厂返回单例占位
    public static CompRewrite CataFuseDifferent() => CataFuseDifferentRule.Instance;
}
