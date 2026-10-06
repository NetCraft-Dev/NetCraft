namespace NetCraft.Registry;

//DataComponentHolder a holder with a component map, maps to vanilla net.minecraft.core.component.DataComponentHolder
//Implementers only provide GetComponents; value access and presence checks are handled here
public interface DataComponentHolder : DataComponentGetter
{
    //GetComponents component map
    DataComponentMap GetComponents();

    //Get delegates to the component map
    T? DataComponentGetter.Get<T>(DataComponentType<T> type) where T : class
        => GetComponents().Get(type);

    //GetOrDefault delegates to the component map
    T DataComponentGetter.GetOrDefault<T>(DataComponentType<T> type, T fallback) where T : class
        => GetComponents().Get(type) ?? fallback;

    //Has whether the component is present
    bool Has<T>(DataComponentType<T> type) where T : class => GetComponents().Get(type) is not null;
}
