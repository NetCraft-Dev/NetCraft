using NetCraft.Codec;

namespace NetCraft.Registry;

//TypedDataComponent 带类型的组件值对应原版 net.minecraft.core.component.TypedDataComponent
//record 持 DataComponentType<T> 与 T value 不可变值类型语义
//流编解码依赖 Network 层的注册表 id 读写放在 TypedDataComponentCodecs
public sealed record TypedDataComponent<T>(DataComponentType<T> Type, T Value) where T : class
{
    //CreateUnchecked 已知类型安全时跳过泛型校验 对应原版 createUnchecked
    public static TypedDataComponent<T> CreateUnchecked(DataComponentType<T> type, object value)
        => new(type, (T)value);

    //FromEntryUnchecked 从非泛型键值造条目 对应原版 fromEntryUnchecked
    public static TypedDataComponent<object> FromEntryUnchecked(object type, object value)
        => new((DataComponentType<object>)type, value);

    //EncodeValue 用该类型的持久化 codec 编码 对应原版 encodeValue
    public DataResult<D> EncodeValue<D>(DynamicOps<D> ops)
        => Type.Codec is { } codec
            ? codec.EncodeStart(ops, Value)
            : DataResult<D>.Error(() => $"{Type} is not an encodable component");

    public override string ToString() => $"{Type}=>{Value}";
}
