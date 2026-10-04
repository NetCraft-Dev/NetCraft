namespace NetCraft.DataFixer.Functions;

using System;
using NetCraft.Codec;
using T = NetCraft.DataFixer.Types;

//Apply函数应用对应原版com.mojang.datafixers.functions.Apply
//把PointFree<A->B>应用到PointFree<A>得到PointFree<B>
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

    //Type未缓存时从func.Type()推断对应原版((Func<?,B>)func.type()).second()
    //用Unsafe.As绕过严格泛型强转T.Func<X,B>到T.Func<object,B>对齐Java类型擦除
    public override T.Type<B> Type()
        => _type ?? TypeFromFunc();

    private T.Type<B> TypeFromFunc()
    {
        var funcType = _func.Type();
        var obj = (object)funcType;
        var cast = System.Runtime.CompilerServices.Unsafe.As<object, T.Func<object, B>>(ref obj);
        return cast.Second();
    }

    //eval惰性求值func与arg后应用func(arg)
    public override Func<DynamicOps<object>, B> Eval()
        => ops => _func.EvalCached()(ops)(_arg.EvalCached()(ops));

    //all对func和arg都应用规则任一变化则重建Apply
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

    //one对func或arg首次命中重建Apply
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
