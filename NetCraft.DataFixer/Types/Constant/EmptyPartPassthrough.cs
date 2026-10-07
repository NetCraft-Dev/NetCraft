namespace NetCraft.DataFixer.Types.Constant;

using System;
using NetCraft.Codec;
using NetCraft.DataFixer;
using NetCraft.DataFixer.Types.Templates;

//EmptyPartPassthrough passthrough Dynamic type maps to vanilla EmptyPartPassthrough
//preserves the original Dynamic value for the remainder field
public sealed class EmptyPartPassthrough : Type<Dynamic<object>>
{
    public override string ToString() => "EmptyPartPassthrough";

    //point returns an empty Dynamic placeholder
    public override Optional<Dynamic<object>> Point<T>(DynamicOps<T> ops)
        => Optional<Dynamic<object>>.Of(new Dynamic<object>(null!, default!));

    public override bool Equals(object? o, bool ignoreRecursionPoints, bool checkIndex)
        => ReferenceEquals(this, o);

    //passthrough type template wraps itself with constType
    public override TypeTemplate BuildTemplate()
        => DSL.ConstType(this);

    //passthrough type codec preserves the Dynamic value as-is
    protected override Codec<Dynamic<object>> BuildCodec()
        => new PassthroughCodec();

    //PassthroughCodec passthrough codec preserves the original Dynamic value, aligning with vanilla passthrough semantics
    //ops and input are passed as object to Dynamic<object>, crossing the U generic to align with Java type erasure
    private sealed class PassthroughCodec : ScalarCodec<Dynamic<object>>
    {
        public override DataResult<Dynamic<object>> Parse<U>(DynamicOps<U> ops, U input)
        {
            //C# generic invariance forbids treating DynamicOps<U> as DynamicOps<object>
            //Unsafe.As across generic interface dispatch triggers CLR 0x80131506 method table entry mismatch
            //use ObjectOpsAdapter<U> to wrap and delegate, aligning with Java type erasure semantics
            DynamicOps<object> objectOps = ops as DynamicOps<object> ?? new ObjectOpsAdapter<U>(ops);
            return DataResult<Dynamic<object>>.Success(new Dynamic<object>(objectOps, (object)input!));
        }

        public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Dynamic<object> value)
        {
            //the actual type of value.Value is determined by the original ops (e.g. Tag); convert to U directly via is pattern matching
            if (value.Value is U v)
                return DataResult<U>.Success(v);
            return DataResult<U>.Error(() => "Cannot encode passthrough dynamic: value type mismatch");
        }
    }
}
