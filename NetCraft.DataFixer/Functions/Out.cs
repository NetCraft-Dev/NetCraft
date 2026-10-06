namespace NetCraft.DataFixer.Functions;

using System;
using NetCraft.Codec;
using NetCraft.DataFixer;
using T = NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Templates;

//Out recursive point outgoing function maps to vanilla com.mojang.datafixers.functions.Out
//expands RecursivePointType into the unfold result
public sealed class Out<A> : PointFree<Func<A, A>>
{
    private readonly RecursivePoint.RecursivePointType<A> _type;

    public Out(RecursivePoint.RecursivePointType<A> type)
    {
        _type = type;
    }

    //type returns the type->unfold function type, maps to DSL.func(type, type.unfold())
    public override T.Type<Func<A, A>> Type()
        => DSL.Func(_type, _type.Unfold());

    public override string ToString(int level) => "Out[" + _type + "]";

    public override bool Equals(object? obj)
        => obj is Out<A> other && Equals(_type, other._type);

    public override int GetHashCode() => _type?.GetHashCode() ?? 0;

    public override Func<DynamicOps<object>, Func<A, A>> Eval()
        => _ => x => x;
}
