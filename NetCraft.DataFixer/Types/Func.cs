namespace NetCraft.DataFixer.Types;

using System;
using NetCraft.Codec;
using NetCraft.DataFixer.Types.Templates;

//Func函数类型对应原版com.mojang.datafixers.types.Func
//表示A->B的函数类型无法编解码只用于View内部
//Func<A,B>继承Type<System.Func<A,B>>此处Func类与System.Func同名需全限定
public sealed class Func<A, B> : Type<System.Func<A, B>>
{
    private readonly Type<A> _first;
    private readonly Type<B> _second;

    public Func(Type<A> first, Type<B> second)
    {
        _first = first;
        _second = second;
    }

    //函数类型不构建模板
    public override TypeTemplate BuildTemplate()
        => throw new NotSupportedException("No template for function types");

    //函数类型不可编解码编解码均返回错误
    protected override Codec<System.Func<A, B>> BuildCodec()
        => new FunctionCodec();

    public Type<A> First() => _first;
    public Type<B> Second() => _second;

    public override bool Equals(object? o, bool ignoreRecursionPoints, bool checkIndex)
        => o is Func<A, B> other
            && _first.Equals(other._first, ignoreRecursionPoints, checkIndex)
            && _second.Equals(other._second, ignoreRecursionPoints, checkIndex);

    public override int GetHashCode()
        => unchecked((_first?.GetHashCode() ?? 0) * 31 + (_second?.GetHashCode() ?? 0));

    public override string ToString() => "(" + _first + " -> " + _second + ")";

    //FunctionCodec函数类型编解码器编解码均返回错误
    private sealed class FunctionCodec : ScalarCodec<System.Func<A, B>>
    {
        public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, System.Func<A, B> value)
            => DataResult<U>.Error(() => "Cannot save a function");

        public override DataResult<System.Func<A, B>> Parse<U>(DynamicOps<U> ops, U input)
            => DataResult<System.Func<A, B>>.Error(() => "Cannot read a function");
    }
}
