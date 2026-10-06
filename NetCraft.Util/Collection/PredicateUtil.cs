namespace NetCraft.Util.Collection;

//Predicate helpers, map to vanilla net.minecraft.util.Util.allOf/anyOf
//Varargs merge multiple Predicates into a single Predicate
public static class PredicateUtil
{
    //allOf with no arguments always returns true, maps to vanilla Util.allOf()
    public static Func<T, bool> AllOf<T>() => _ => true;

    //allOf with one argument returns it directly, maps to vanilla Util.allOf(Predicate)
    public static Func<T, bool> AllOf<T>(Func<T, bool> condition) => condition;

    //allOf two-argument logical AND, maps to vanilla Util.allOf(Predicate,Predicate)
    public static Func<T, bool> AllOf<T>(Func<T, bool> c1, Func<T, bool> c2)
        => t => c1(t) && c2(t);

    //allOf three-argument logical AND, maps to vanilla Util.allOf(Predicate,Predicate,Predicate)
    public static Func<T, bool> AllOf<T>(Func<T, bool> c1, Func<T, bool> c2, Func<T, bool> c3)
        => t => c1(t) && c2(t) && c3(t);

    //allOf four-argument logical AND, maps to vanilla Util.allOf(four args)
    public static Func<T, bool> AllOf<T>(Func<T, bool> c1, Func<T, bool> c2, Func<T, bool> c3, Func<T, bool> c4)
        => t => c1(t) && c2(t) && c3(t) && c4(t);

    //allOf five-argument logical AND, maps to vanilla Util.allOf(five args)
    public static Func<T, bool> AllOf<T>(Func<T, bool> c1, Func<T, bool> c2, Func<T, bool> c3, Func<T, bool> c4, Func<T, bool> c5)
        => t => c1(t) && c2(t) && c3(t) && c4(t) && c5(t);

    //allOf varargs logical AND, maps to vanilla Util.allOf(Predicate...)
    public static Func<T, bool> AllOf<T>(params Func<T, bool>[] conditions)
        => t =>
        {
            foreach (var c in conditions)
                if (!c(t))
                    return false;
            return true;
        };

    //allOf list-argument logical AND, maps to vanilla Util.allOf(List)
    public static Func<T, bool> AllOf<T>(IReadOnlyList<Func<T, bool>> conditions)
        => t =>
        {
            foreach (var c in conditions)
                if (!c(t))
                    return false;
            return true;
        };

    //anyOf with no arguments always returns false, maps to vanilla Util.anyOf()
    public static Func<T, bool> AnyOf<T>() => _ => false;

    //anyOf with one argument returns it directly, maps to vanilla Util.anyOf(Predicate)
    public static Func<T, bool> AnyOf<T>(Func<T, bool> condition) => condition;

    //anyOf two-argument logical OR, maps to vanilla Util.anyOf(Predicate,Predicate)
    public static Func<T, bool> AnyOf<T>(Func<T, bool> c1, Func<T, bool> c2)
        => t => c1(t) || c2(t);

    //anyOf three-argument logical OR, maps to vanilla Util.anyOf(Predicate,Predicate,Predicate)
    public static Func<T, bool> AnyOf<T>(Func<T, bool> c1, Func<T, bool> c2, Func<T, bool> c3)
        => t => c1(t) || c2(t) || c3(t);

    //anyOf four-argument logical OR, maps to vanilla Util.anyOf(four args)
    public static Func<T, bool> AnyOf<T>(Func<T, bool> c1, Func<T, bool> c2, Func<T, bool> c3, Func<T, bool> c4)
        => t => c1(t) || c2(t) || c3(t) || c4(t);

    //anyOf five-argument logical OR, maps to vanilla Util.anyOf(five args)
    public static Func<T, bool> AnyOf<T>(Func<T, bool> c1, Func<T, bool> c2, Func<T, bool> c3, Func<T, bool> c4, Func<T, bool> c5)
        => t => c1(t) || c2(t) || c3(t) || c4(t) || c5(t);

    //anyOf varargs logical OR, maps to vanilla Util.anyOf(Predicate...)
    public static Func<T, bool> AnyOf<T>(params Func<T, bool>[] conditions)
        => t =>
        {
            foreach (var c in conditions)
                if (c(t))
                    return true;
            return false;
        };

    //anyOf list-argument logical OR, maps to vanilla Util.anyOf(List)
    public static Func<T, bool> AnyOf<T>(IReadOnlyList<Func<T, bool>> conditions)
        => t =>
        {
            foreach (var c in conditions)
                if (c(t))
                    return true;
            return false;
        };
}
