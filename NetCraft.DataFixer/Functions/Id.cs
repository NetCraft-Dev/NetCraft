namespace NetCraft.DataFixer.Functions;

using System;
using NetCraft.Codec;
using T = NetCraft.DataFixer.Types;

//Id单位函数对应原版com.mojang.datafixers.functions.Id
//PointFree<Func<A,A>>求值返回identity
public sealed class Id<A> : PointFree<Func<A, A>>
{
    private readonly T.Type<Func<A, A>>? _type;

    public Id(T.Type<Func<A, A>>? type)
    {
        _type = type;
    }

    public override T.Type<Func<A, A>> Type()
        => _type!;

    public override bool Equals(object? obj)
        => obj is Id<A> other && Equals(_type, other._type);

    public override int GetHashCode() => _type?.GetHashCode() ?? 0;

    public override string ToString(int level) => "id";

    public override Func<DynamicOps<object>, Func<A, A>> Eval()
        => _ => x => x;
}
