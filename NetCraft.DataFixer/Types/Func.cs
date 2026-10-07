namespace NetCraft.DataFixer.Types;

using System;
using NetCraft.Codec;
using NetCraft.DataFixer.Types.Templates;

//Func function type maps to vanilla com.mojang.datafixers.types.Func
//represents the A->B function type; not encodable, used only inside View
//Func<A,B> inherits Type<System.Func<A,B>>; here the Func class shares a name with System.Func and must be fully qualified
public sealed class Func<A, B> : Type<System.Func<A, B>>
{
    private readonly Type<A> _first;
    private readonly Type<B> _second;

    public Func(Type<A> first, Type<B> second)
    {
        _first = first;
        _second = second;
    }

    //function types do not build a template
    public override TypeTemplate BuildTemplate()
        => throw new NotSupportedException("No template for function types");

    //function types are not encodable; both encode and decode return an error
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

    //FunctionCodec function type codec; both encode and decode return an error
    private sealed class FunctionCodec : ScalarCodec<System.Func<A, B>>
    {
        public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, System.Func<A, B> value)
            => DataResult<U>.Error(() => "Cannot save a function");

        public override DataResult<System.Func<A, B>> Parse<U>(DynamicOps<U> ops, U input)
            => DataResult<System.Func<A, B>>.Error(() => "Cannot read a function");
    }
}
