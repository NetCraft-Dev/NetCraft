namespace NetCraft.DataFixer.Types.Constant;

using System;
using NetCraft.Codec;
using NetCraft.DataFixer;
using NetCraft.DataFixer.Types.Templates;
using NetCraft.DataFixer.Util;

//EmptyPart empty unit type maps to vanilla com.mojang.datafixers.types.constant.EmptyPart
//used as the placeholder empty type of recursive type families; point returns Unit
public sealed class EmptyPart : Type<Unit>
{
    public override string ToString() => "EmptyPart";

    //point returns the Unit singleton
    public override Optional<Unit> Point<T>(DynamicOps<T> ops)
        => Optional<Unit>.Of(Unit.Instance);

    //the empty type equals only itself
    public override bool Equals(object? o, bool ignoreRecursionPoints, bool checkIndex)
        => ReferenceEquals(this, o);

    //empty type template wraps itself with constType
    public override TypeTemplate BuildTemplate()
        => DSL.ConstType(this);

    //empty type codec always decodes to Unit and encodes empty
    protected override Codec<Unit> BuildCodec()
        => new EmptyUnitCodec();

    //EmptyUnitCodec empty type codec; decode returns Unit and encode is empty
    private sealed class EmptyUnitCodec : AbstractMapCodec<Unit>
    {
        public override DataResult<Unit> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
            => DataResult<Unit>.Success(Unit.Instance);

        public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, Unit value, RecordBuilder<U> builder)
            => builder;
    }
}
