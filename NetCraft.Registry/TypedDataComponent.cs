namespace NetCraft.Registry;

//TypedDataComponent 带类型的组件值对应原版 net.minecraft.core.component.TypedDataComponent
//record 持 DataComponentType<T> 与 T value 不可变值类型语义
public sealed record TypedDataComponent<T>(DataComponentType<T> Type, T Value) where T : class
{
    public override string ToString() => $"{Type}={Value}";
}
