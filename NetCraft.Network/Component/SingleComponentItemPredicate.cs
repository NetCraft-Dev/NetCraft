using NetCraft.Registry;

namespace NetCraft.Network.Component;

//SingleComponentItemPredicate 只管一个组件类型的谓词
//对应原版 net.minecraft.advancements.predicates.SingleComponentItemPredicate
//实现方给出组件类型与值级判定 Matches 由默认实现取组件后转发
public interface SingleComponentItemPredicate<T> : DataComponentPredicate where T : class
{
    //ComponentType 本谓词盯着的组件类型 组件泛型统一用 object 值级判定靠 T 区分
    DataComponentType<object> ComponentType { get; }

    //MatchesValue 组件值是否满足本谓词
    bool MatchesValue(T value);

    bool DataComponentPredicate.Matches(DataComponentGetter components)
        => components.Get(ComponentType) is T value && MatchesValue(value);
}
