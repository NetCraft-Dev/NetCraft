namespace NetCraft.Util.Collection;

//Predicate工具对应原版net.minecraft.util.Util.allOf/anyOf
//变长参数合并多个Predicate为单一Predicate
public static class PredicateUtil
{
    //allOf无参数永远返回true对应原版Util.allOf()
    public static Func<T, bool> AllOf<T>() => _ => true;

    //allOf单参数直接返回对应原版Util.allOf(Predicate)
    public static Func<T, bool> AllOf<T>(Func<T, bool> condition) => condition;

    //allOf双参数逻辑与对应原版Util.allOf(Predicate,Predicate)
    public static Func<T, bool> AllOf<T>(Func<T, bool> c1, Func<T, bool> c2)
        => t => c1(t) && c2(t);

    //allOf三参数逻辑与对应原版Util.allOf(Predicate,Predicate,Predicate)
    public static Func<T, bool> AllOf<T>(Func<T, bool> c1, Func<T, bool> c2, Func<T, bool> c3)
        => t => c1(t) && c2(t) && c3(t);

    //allOf四参数逻辑与对应原版Util.allOf(四参数)
    public static Func<T, bool> AllOf<T>(Func<T, bool> c1, Func<T, bool> c2, Func<T, bool> c3, Func<T, bool> c4)
        => t => c1(t) && c2(t) && c3(t) && c4(t);

    //allOf五参数逻辑与对应原版Util.allOf(五参数)
    public static Func<T, bool> AllOf<T>(Func<T, bool> c1, Func<T, bool> c2, Func<T, bool> c3, Func<T, bool> c4, Func<T, bool> c5)
        => t => c1(t) && c2(t) && c3(t) && c4(t) && c5(t);

    //allOf变长参数逻辑与对应原版Util.allOf(Predicate...)
    public static Func<T, bool> AllOf<T>(params Func<T, bool>[] conditions)
        => t =>
        {
            foreach (var c in conditions)
                if (!c(t))
                    return false;
            return true;
        };

    //allOf列表参数逻辑与对应原版Util.allOf(List)
    public static Func<T, bool> AllOf<T>(IReadOnlyList<Func<T, bool>> conditions)
        => t =>
        {
            foreach (var c in conditions)
                if (!c(t))
                    return false;
            return true;
        };

    //anyOf无参数永远返回false对应原版Util.anyOf()
    public static Func<T, bool> AnyOf<T>() => _ => false;

    //anyOf单参数直接返回对应原版Util.anyOf(Predicate)
    public static Func<T, bool> AnyOf<T>(Func<T, bool> condition) => condition;

    //anyOf双参数逻辑或对应原版Util.anyOf(Predicate,Predicate)
    public static Func<T, bool> AnyOf<T>(Func<T, bool> c1, Func<T, bool> c2)
        => t => c1(t) || c2(t);

    //anyOf三参数逻辑或对应原版Util.anyOf(Predicate,Predicate,Predicate)
    public static Func<T, bool> AnyOf<T>(Func<T, bool> c1, Func<T, bool> c2, Func<T, bool> c3)
        => t => c1(t) || c2(t) || c3(t);

    //anyOf四参数逻辑或对应原版Util.anyOf(四参数)
    public static Func<T, bool> AnyOf<T>(Func<T, bool> c1, Func<T, bool> c2, Func<T, bool> c3, Func<T, bool> c4)
        => t => c1(t) || c2(t) || c3(t) || c4(t);

    //anyOf五参数逻辑或对应原版Util.anyOf(五参数)
    public static Func<T, bool> AnyOf<T>(Func<T, bool> c1, Func<T, bool> c2, Func<T, bool> c3, Func<T, bool> c4, Func<T, bool> c5)
        => t => c1(t) || c2(t) || c3(t) || c4(t) || c5(t);

    //anyOf变长参数逻辑或对应原版Util.anyOf(Predicate...)
    public static Func<T, bool> AnyOf<T>(params Func<T, bool>[] conditions)
        => t =>
        {
            foreach (var c in conditions)
                if (c(t))
                    return true;
            return false;
        };

    //anyOf列表参数逻辑或对应原版Util.anyOf(List)
    public static Func<T, bool> AnyOf<T>(IReadOnlyList<Func<T, bool>> conditions)
        => t =>
        {
            foreach (var c in conditions)
                if (c(t))
                    return true;
            return false;
        };
}
