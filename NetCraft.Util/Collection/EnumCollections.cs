namespace NetCraft.Util.Collection;

//Enum collection helpers, map to vanilla net.minecraft.util.Util.makeEnumMap/allOfEnumExcept
//C# uses Enum.GetValues to get enum constants, maps to vanilla keyType.getEnumConstants
public static class EnumCollections
{
    //makeEnumMap builds a dictionary keyed by enum type, maps to vanilla Util.makeEnumMap
    //Iterates enum constants calling function to build values
    public static Dictionary<K, V> MakeEnumMap<K, V>(Func<K, V> function)
        where K : struct, Enum
    {
        var map = new Dictionary<K, V>();
        foreach (var key in Enum.GetValues<K>())
            map[key] = function(key);
        return map;
    }

    //allOfEnumExcept returns all enum values except the given one, maps to vanilla Util.allOfEnumExcept
    //Maps to vanilla EnumSet.complementOf(EnumSet.of(value))
    public static HashSet<T> AllOfEnumExcept<T>(T value)
        where T : struct, Enum
    {
        var result = new HashSet<T>();
        foreach (var v in Enum.GetValues<T>())
            if (!EqualityComparer<T>.Default.Equals(v, value))
                result.Add(v);
        return result;
    }
}
