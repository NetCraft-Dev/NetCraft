namespace NetCraft.Game.Advancements.Predicates;

//IValuePredicate 值判定接口 对应原版 java.util.function.Predicate
//谓词对象自身实现它 这样集合谓词与物品谓词才能带 codec 组合
public interface IValuePredicate<in T>
{
    bool Test(T value);
}
