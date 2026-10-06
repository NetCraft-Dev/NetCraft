namespace NetCraft.Game.Advancements.Predicates;

//IValuePredicate value predicate interface, maps to vanilla java.util.function.Predicate
//Predicate objects implement it themselves, so collection and item predicates can combine with codecs
public interface IValuePredicate<in T>
{
    bool Test(T value);
}
