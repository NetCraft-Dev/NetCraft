namespace NetCraft.Registry;

//DataComponentGetter 只读组件取值接口 对应原版 net.minecraft.core.component.DataComponentGetter
//Get 由实现方提供 getOrDefault 与 getTyped 是默认方法
public interface DataComponentGetter
{
    //Get 按 type 取值 不存在返回 null
    T? Get<T>(DataComponentType<T> type) where T : class;

    //GetOrDefault 缺失时用兜底值
    T GetOrDefault<T>(DataComponentType<T> type, T fallback) where T : class
        => Get(type) ?? fallback;

    //GetTyped 取带类型的组件条目 缺失返回 null
    TypedDataComponent<T>? GetTyped<T>(DataComponentType<T> type) where T : class
        => Get(type) is { } value ? new TypedDataComponent<T>(type, value) : null;
}
