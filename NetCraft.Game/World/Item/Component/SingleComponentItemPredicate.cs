using NetCraft.Registry;

namespace NetCraft.Game.World.Items.Component;

//SingleComponentItemPredicate predicate dealing with a single component type
//Maps to vanilla net.minecraft.advancements.predicates.SingleComponentItemPredicate
//Implementers supply the component type and value-level check; the default Matches takes the component and forwards
public interface SingleComponentItemPredicate<T> : DataComponentPredicate where T : class
{
    //ComponentType the component type this predicate watches; component generics uniformly use object and the value-level check discriminates by T
    DataComponentType<object> ComponentType { get; }

    //MatchesValue checks whether the component value satisfies this predicate
    bool MatchesValue(T value);

    bool DataComponentPredicate.Matches(DataComponentGetter components)
        => components.Get(ComponentType) is T value && MatchesValue(value);
}
