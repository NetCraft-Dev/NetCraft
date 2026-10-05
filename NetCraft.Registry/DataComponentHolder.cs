namespace NetCraft.Registry;

//DataComponentHolder 带组件映射的持有者 对应原版 net.minecraft.core.component.DataComponentHolder
//实现方只需给出 GetComponents 取值与判存都由它代劳
public interface DataComponentHolder : DataComponentGetter
{
    //GetComponents 组件映射
    DataComponentMap GetComponents();

    //Get 委托给组件映射
    T? DataComponentGetter.Get<T>(DataComponentType<T> type) where T : class
        => GetComponents().Get(type);

    //GetOrDefault 委托给组件映射
    T DataComponentGetter.GetOrDefault<T>(DataComponentType<T> type, T fallback) where T : class
        => GetComponents().Get(type) ?? fallback;

    //Has 是否存在该组件
    bool Has<T>(DataComponentType<T> type) where T : class => GetComponents().Get(type) is not null;
}
