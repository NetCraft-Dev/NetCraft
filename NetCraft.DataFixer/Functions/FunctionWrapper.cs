namespace NetCraft.DataFixer.Functions;

using System;
using NetCraft.Codec;
using NetCraft.DataFixer;
using T = NetCraft.DataFixer.Types;

//FunctionWrapper函数包装对应原版FunctionWrapper
//把普通Function<DynamicOps,Function<A,B>>包装为PointFree
internal sealed class FunctionWrapper<A, B> : PointFree<Func<A, B>>
{
    private readonly string _name;
    private readonly Func<DynamicOps<object>, Func<A, B>> _fun;
    private readonly T.Type<Func<A, B>> _type;

    public FunctionWrapper(string name, Func<DynamicOps<object>, Func<A, B>> fun, T.Type<A> input, T.Type<B> output)
    {
        _name = name;
        _fun = fun;
        _type = DSL.Func(input, output);
    }

    public override T.Type<Func<A, B>> Type() => _type;

    public override string ToString(int level) => "fun[" + _name + "]";

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj)) return true;
        if (obj is not FunctionWrapper<A, B> that) return false;
        return Equals(_fun, that._fun) && Equals(_type, that._type);
    }

    public override int GetHashCode() => _fun?.GetHashCode() ?? 0;

    public override Func<DynamicOps<object>, Func<A, B>> Eval() => _fun;
}
