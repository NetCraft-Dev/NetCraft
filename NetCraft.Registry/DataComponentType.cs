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
}
