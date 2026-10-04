namespace NetCraft.DataFixer.Types.Constant;

using System;
using NetCraft.Codec;
using NetCraft.DataFixer;
using NetCraft.DataFixer.Types.Templates;
using NetCraft.DataFixer.Util;

//EmptyPart空单元类型对应原版com.mojang.datafixers.types.constant.EmptyPart
//用作递归类型家族的占位空类型point返回Unit
public sealed class EmptyPart : Type<Unit>
{
    public override string ToString() => "EmptyPart";

    //point返回Unit单例
    public override Optional<Unit> Point<T>(DynamicOps<T> ops)
        => Optional<Unit>.Of(Unit.Instance);

    //空类型只与自身相等
    public override bool Equals(object? o, bool ignoreRecursionPoints, bool checkIndex)
        => ReferenceEquals(this, o);

    //空类型模板用constType包装自身
    public override TypeTemplate BuildTemplate()
        => DSL.ConstType(this);

    //空类型codec解码总是Unit编码空
    protected override Codec<Unit> BuildCodec()
        => new EmptyUnitCodec();

    //EmptyUnitCodec空类型编解码器解码返回Unit编码空
    private sealed class EmptyUnitCodec : AbstractMapCodec<Unit>
    {
        public override DataResult<Unit> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
            => DataResult<Unit>.Success(Unit.Instance);

        public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, Unit value, RecordBuilder<U> builder)
            => builder;
    }
}
