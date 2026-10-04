namespace NetCraft.DataFixer.Types.Constant;

using System;
using NetCraft.Codec;
using NetCraft.DataFixer;
using NetCraft.DataFixer.Types.Templates;

//EmptyPartPassthrough透传Dynamic类型对应原版EmptyPartPassthrough
//保留原始Dynamic值用于remainder字段
public sealed class EmptyPartPassthrough : Type<Dynamic<object>>
{
    public override string ToString() => "EmptyPartPassthrough";

    //point返回空Dynamic占位
    public override Optional<Dynamic<object>> Point<T>(DynamicOps<T> ops)
        => Optional<Dynamic<object>>.Of(new Dynamic<object>(null!, default!));

    public override bool Equals(object? o, bool ignoreRecursionPoints, bool checkIndex)
        => ReferenceEquals(this, o);

    //透传类型模板用constType包装自身
    public override TypeTemplate BuildTemplate()
        => DSL.ConstType(this);

    //透传类型codec原样保留Dynamic值
    protected override Codec<Dynamic<object>> BuildCodec()
        => new PassthroughCodec();

    //PassthroughCodec透传编解码器保留原Dynamic值对齐原版passthrough语义
    //ops与input按object透传给Dynamic<object>跨U泛型对齐Java类型擦除
    private sealed class PassthroughCodec : ScalarCodec<Dynamic<object>>
    {
        public override DataResult<Dynamic<object>> Parse<U>(DynamicOps<U> ops, U input)
        {
            //C#泛型不变性禁止把DynamicOps<U>当DynamicOps<object>用
            //Unsafe.As跨泛型接口分发会触发CLR 0x80131506方法表条目不匹配
            //用ObjectOpsAdapter<U>包装委托对齐Java类型擦除语义
            DynamicOps<object> objectOps = ops as DynamicOps<object> ?? new ObjectOpsAdapter<U>(ops);
            return DataResult<Dynamic<object>>.Success(new Dynamic<object>(objectOps, (object)input!));
        }

        public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Dynamic<object> value)
        {
            //value.Value实际类型由原始ops决定(如Tag)直接is模式匹配转U
            if (value.Value is U v)
                return DataResult<U>.Success(v);
            return DataResult<U>.Error(() => "Cannot encode passthrough dynamic: value type mismatch");
        }
    }
}
