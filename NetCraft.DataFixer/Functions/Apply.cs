namespace NetCraft.DataFixer.Functions;

using System;
using NetCraft.Codec;
using T = NetCraft.DataFixer.Types;

//Apply function application maps to vanilla com.mojang.datafixers.functions.Apply
//applies PointFree<A->B> to PointFree<A> to get PointFree<B>
public sealed class Apply<A, B> : PointFree<B>
{
    private readonly PointFree<Func<A, B>> _func;
    private readonly PointFree<A> _arg;
    private readonly T.Type<B> _type;

    public Apply(PointFree<Func<A, B>> func, PointFree<A> arg, T.Type<B>? type = null)
    {
        _func = func;
        _arg = arg;
        _type = type!;
    }

    public PointFree<Func<A, B>> Func => _func;
    public PointFree<A> Arg => _arg;

    //Type is inferred from func.Type() when not cached; maps to vanilla ((Func<?,B>)func.type()).second()
    //use Unsafe.As to bypass the strict generic cast of T.Func<X,B> to T.Func<object,B> and align with Java type erasure
    public override T.Type<B> Type()
        => _type ?? TypeFromFunc();

    private T.Type<B> TypeFromFunc()
    {
        var funcType = _func.Type();
        var obj = (object)funcType;
        var cast = System.Runtime.CompilerServices.Unsafe.As<object, T.Func<object, B>>(ref obj);
        return cast.Second();
    }

    //eval lazily evaluates func and arg, then applies func(arg)
    public override Func<DynamicOps<object>, B> Eval()
        => ops => _func.EvalCached()(ops)(_arg.EvalCached()(ops));

    //all applies the rule to both func and arg; rebuilds Apply if either changes
    public override Optional<PointFree<B>> All(PointFreeRule rule)
    {
        var f = rule.RewriteOrNop(_func);
        var a = rule.RewriteOrNop(_arg);
        if (ReferenceEquals(f, _func) && ReferenceEquals(a, _arg))
        {
            return Optional<PointFree<B>>.Of(this);
        }
        return Optional<PointFree<B>>.Of(new Apply<A, B>(f, a, _type));
    }

    //one rebuilds Apply on the first match of func or arg
    public override Optional<PointFree<B>> One(PointFreeRule rule)
    {
        var fOpt = rule.Rewrite(_func);
        if (fOpt.IsPresent)
        {
            return Optional<PointFree<B>>.Of(new Apply<A, B>(fOpt.Get(), _arg, _type));
        }
        var aOpt = rule.Rewrite(_arg);
        if (aOpt.IsPresent)
        {
            return Optional<PointFree<B>>.Of(new Apply<A, B>(_func, aOpt.Get(), _type));
        }
        return Optional<PointFree<B>>.Empty();
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj)) return true;
        if (obj is not Apply<A, B> other) return false;
        return Equals(_func, other._func) && Equals(_arg, other._arg);
    }

    public override int GetHashCode()
    {
        var result = _func?.GetHashCode() ?? 0;
        return 31 * result + (_arg?.GetHashCode() ?? 0);
    }

    public override string ToString(int level)
        => "(ap " + _func?.ToString(level + 1) + "\n" + Indent(level + 1) + _arg?.ToString(level + 1) + "\n" + Indent(level) + ")";
}
