namespace NetCraft.DataFixer.Functions;

using System;
using NetCraft.Codec;
using NetCraft.DataFixer;
using T = NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Templates;

//In递归点入向函数对应原版com.mojang.datafixers.functions.In
//把unfold结果折叠回RecursivePointType
public sealed class In<A> : PointFree<Func<A, A>>
{
    private readonly RecursivePoint.RecursivePointType<A> _type;

    public In(RecursivePoint.RecursivePointType<A> type)
    {
        _type = type;
    }

    //type返回unfold->type的函数类型对应DSL.func(type.unfold(), type)
    public override T.Type<Func<A, A>> Type()
        => DSL.Func(_type.Unfold(), _type);

    public override string ToString(int level) => "In[" + _type + "]";

    public override bool Equals(object? obj)
        => obj is In<A> other && Equals(_type, other._type);

    public override int GetHashCode() => _type?.GetHashCode() ?? 0;

    public override Func<DynamicOps<object>, Func<A, A>> Eval()
        => _ => x => x;
}
