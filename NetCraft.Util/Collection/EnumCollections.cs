namespace NetCraft.Util.Collection;

//Enum集合工具对应原版net.minecraft.util.Util.makeEnumMap/allOfEnumExcept
//C#用Enum.GetValues获取枚举常量对应原版keyType.getEnumConstants
public static class EnumCollections
{
    //makeEnumMap按枚举键类型构造字典对应原版Util.makeEnumMap
    //遍历枚举常量调用function构造值
    public static Dictionary<K, V> MakeEnumMap<K, V>(Func<K, V> function)
        where K : struct, Enum
    {
        var map = new Dictionary<K, V>();
        foreach (var key in Enum.GetValues<K>())
            map[key] = function(key);
        return map;
    }

    //allOfEnumExcept返回除指定值外所有枚举值对应原版Util.allOfEnumExcept
    //对应原版EnumSet.complementOf(EnumSet.of(value))
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
