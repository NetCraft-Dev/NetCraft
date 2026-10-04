namespace NetCraft.DataFixer.Functions;

using System;
using NetCraft.Codec;
using NetCraft.DataFixer;
using NetCraft.DataFixer.Util;
using T = NetCraft.DataFixer.Types;

//Bang丢弃函数对应原版com.mojang.datafixers.functions.Bang
//把任意A映射为Unit用于空字段
public sealed class Bang<A> : PointFree<Func<A, Unit>>
{
    private readonly T.Type<A> _type;

    public Bang(T.Type<A> type)
    {
        _type = type;
    }

    //type返回A->Unit的函数类型对应DSL.func(type, emptyPartType)
    public override T.Type<Func<A, Unit>> Type()
        => DSL.Func(_type, DSL.EmptyPartType());

    public override bool Equals(object? obj)
        => obj is Bang<A> other && Equals(_type, other._type);

    public override int GetHashCode() => _type?.GetHashCode() ?? 0;

    public override string ToString(int level) => "!";

    public override Func<DynamicOps<object>, Func<A, Unit>> Eval()
        => ops => _ => Unit.Instance;
}
