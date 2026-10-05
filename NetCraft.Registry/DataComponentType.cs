using NetCraft.Codec;

namespace NetCraft.Registry;

//DataComponentType 数据组件类型对应原版 net.minecraft.core.component.DataComponentType
//接口只定义 Codec 持久化编解码StreamCodec 在 Network 子库的 SimpleDataComponentType 实现
//Builder.persistent 设 Codec networkSynchronized 设 StreamCodec build 返回 SimpleDataComponentType
//IsTransient 表示无 Codec 不持久化仅网络同步
public interface DataComponentType<T>
{
    //Codec 持久化编解码器 null 表示非持久化组件
    Codec<T>? Codec { get; }

    //IgnoreSwapAnimation 是否忽略交换动画
    bool IgnoreSwapAnimation { get; }

    //IsTransient 是否非持久化 Codec 为 null 即 transient
    bool IsTransient => Codec is null;

    //CodecOrThrow 获取 Codec 非持久化抛异常
    Codec<T> CodecOrThrow()
    {
        if (Codec is null)
            throw new InvalidOperationException($"{this} is not a persistent component");
        return Codec;
    }

    //CODEC 按注册名解析组件类型 对应原版 DataComponentType.CODEC
    public static readonly Codec<DataComponentType<object>> CODEC = new ComponentTypeByNameCodec();

    //PERSISTENT_CODEC 同 CODEC 但拒绝 transient 组件 对应原版 PERSISTENT_CODEC
    public static readonly Codec<DataComponentType<object>> PERSISTENT_CODEC = CODEC.ComapFlatMap(
        type => type.IsTransient
            ? DataResult<DataComponentType<object>>.Error(
                () => $"遇到非持久化组件 {BuiltInRegistries.DATA_COMPONENT_TYPE.GetKey(type)}")
            : DataResult<DataComponentType<object>>.Success(type),
        type => type);

    //VALUE_MAP_CODEC 组件类型到值的映射编解码 每种类型用自己的 codec 对应原版 VALUE_MAP_CODEC
    public static readonly Codec<Dictionary<DataComponentType<object>, object>> VALUE_MAP_CODEC
        = Codecs.DispatchedMap(PERSISTENT_CODEC, type => type.CodecOrThrow());
}

//ComponentTypeByNameCodec 组件类型按注册名解析与回写 对应原版 BuiltInRegistries.DATA_COMPONENT_TYPE.byNameCodec
internal sealed class ComponentTypeByNameCodec : ScalarCodec<DataComponentType<object>>
{
    public override DataResult<DataComponentType<object>> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (!text.Result().IsPresent)
            return DataResult<DataComponentType<object>>.Error(() => "组件类型必须是字符串");
        var id = Identifier.TryParse(text.GetOrThrow());
        if (id is null)
            return DataResult<DataComponentType<object>>.Error(() => $"非法的组件类型名 {text.GetOrThrow()}");
        return BuiltInRegistries.DATA_COMPONENT_TYPE.GetValue(id.Value) is DataComponentType<object> type
            ? DataResult<DataComponentType<object>>.Success(type)
            : DataResult<DataComponentType<object>>.Error(() => $"未知组件类型 {id}");
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, DataComponentType<object> value)
    {
        var key = BuiltInRegistries.DATA_COMPONENT_TYPE.GetKey(value);
        return key is null
            ? DataResult<U>.Error(() => "组件类型未注册 无法编码")
            : DataResult<U>.Success(ops.CreateString(key.Value.ToString()));
    }
}
