namespace NetCraft.Registry;

//DataComponentGetter read-only component access interface, maps to vanilla net.minecraft.core.component.DataComponentGetter
//Get is provided by the implementer; getOrDefault and getTyped are default methods
public interface DataComponentGetter
{
    //Get returns the value by type; returns null if absent
    T? Get<T>(DataComponentType<T> type) where T : class;

    //GetOrDefault uses the fallback when missing
    T GetOrDefault<T>(DataComponentType<T> type, T fallback) where T : class
        => Get(type) ?? fallback;

    //GetTyped gets the typed component entry; returns null if absent
    TypedDataComponent<T>? GetTyped<T>(DataComponentType<T> type) where T : class
        => Get(type) is { } value ? new TypedDataComponent<T>(type, value) : null;
}
