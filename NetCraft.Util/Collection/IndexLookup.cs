namespace NetCraft.Util.Collection;

//索引查找工具对应原版net.minecraft.util.Util.createIndexLookup/createIndexIdentityLookup
//小列表用IndexOf线性查找大列表用Dictionary索引
public static class IndexLookup
{
    //LINEAR_LOOKUP_THRESHOLD小于此值用线性查找对应原版LINEAR_LOOKUP_THRESHOLD
    private const int LinearLookupThreshold = 8;

    //createIndexLookup构造索引查找委托对应原版Util.createIndexLookup
    //小列表用IndexOf线性查找大列表用Dictionary按值查找
    public static Func<T, int> CreateIndexLookup<T>(IReadOnlyList<T> values)
        where T : notnull
    {
        var size = values.Count;
        if (size < LinearLookupThreshold)
            return value =>
            {
                for (var i = 0; i < size; i++)
                    if (EqualityComparer<T>.Default.Equals(values[i], value))
                        return i;
                return -1;
            };
        var map = new Dictionary<T, int>(size);
        for (var i = 0; i < size; i++)
            map[values[i]] = i;
        return value => map.TryGetValue(value, out var idx) ? idx : -1;
    }

    //createIndexIdentityLookup构造引用相等索引查找委托对应原版Util.createIndexIdentityLookup
    //小列表用引用比较IndexOf大列表用ReferenceEqualityComparer字典按引用查找
    public static Func<T, int> CreateIndexIdentityLookup<T>(IReadOnlyList<T> values)
        where T : class
    {
        var size = values.Count;
        if (size < LinearLookupThreshold)
        {
            return value =>
            {
                for (var i = 0; i < size; i++)
                    if (ReferenceEquals(values[i], value))
                        return i;
                return -1;
            };
        }
        var map = new Dictionary<T, int>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < size; i++)
            map[values[i]] = i;
        return lookup => map.TryGetValue(lookup, out var idx) ? idx : -1;
    }
}
