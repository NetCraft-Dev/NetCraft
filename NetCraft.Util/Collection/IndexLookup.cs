namespace NetCraft.Util.Collection;

//Index lookup helpers, map to vanilla net.minecraft.util.Util.createIndexLookup/createIndexIdentityLookup
//Small lists use IndexOf linear search, large lists use a Dictionary index
public static class IndexLookup
{
    //LINEAR_LOOKUP_THRESHOLD below this uses linear search, maps to vanilla LINEAR_LOOKUP_THRESHOLD
    private const int LinearLookupThreshold = 8;

    //createIndexLookup builds an index lookup delegate, maps to vanilla Util.createIndexLookup
    //Small lists use IndexOf linear search, large lists use a Dictionary for value lookup
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

    //createIndexIdentityLookup builds a reference-equality index lookup delegate, maps to vanilla Util.createIndexIdentityLookup
    //Small lists use reference-comparing IndexOf, large lists use a ReferenceEqualityComparer dictionary for reference lookup
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
