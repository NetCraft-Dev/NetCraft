namespace NetCraft.DataFixer.Functions;

using System;
using NetCraft.Codec;
using NetCraft.DataFixer;
using T = NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Templates;

//Out递归点出向函数对应原版com.mojang.datafixers.functions.Out
//把RecursivePointType展开为unfold结果
public sealed class Out<A> : PointFree<Func<A, A>>
{
    private readonly RecursivePoint.RecursivePointType<A> _type;

    public Out(RecursivePoint.RecursivePointType<A> type)
    {
        _type = type;
    }

    //type返回type->unfold的函数类型对应DSL.func(type, type.unfold())
    public override T.Type<Func<A, A>> Type()
        => DSL.Func(_type, _type.Unfold());

    public override string ToString(int level) => "Out[" + _type + "]";

    public override bool Equals(object? obj)
        => obj is Out<A> other && Equals(_type, other._type);

    public override int GetHashCode() => _type?.GetHashCode() ?? 0;

    public override Func<DynamicOps<object>, Func<A, A>> Eval()
        => _ => x => x;
}
