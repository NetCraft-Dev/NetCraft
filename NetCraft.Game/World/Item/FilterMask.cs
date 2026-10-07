using NetCraft.Registry;

namespace NetCraft.Game.World.Items;

//FilterMask component filter mask, maps to vanilla net.minecraft.world.item.component.FilterMask
//Used by Container-type codecs such as shulker boxes to filter components by type reference when serializing
//Everything passes by default; Add excludes explicitly and Remove includes explicitly
//When container items land, FilterMask.Filter limits the subset of components exposed to the client
public sealed class FilterMask
{
    //Exclusion set of explicitly excluded type references, matching vanilla exclusionMask
    private readonly HashSet<object> _exclusion = new(ReferenceEqualityComparer.Instance);
    //Inclusion set of explicitly included type references, matching vanilla inclusionMask
    private readonly HashSet<object> _inclusion = new(ReferenceEqualityComparer.Instance);

    public FilterMask() { }

    //IsEmpty no mask rules at all, every component passes by default
    public bool IsEmpty => _exclusion.Count == 0 && _inclusion.Count == 0;

    //Add adds a type to the exclusion set, maps to vanilla add
    //Passing by default becomes excluded after Add
    public void Add<T>(DataComponentType<T> type) where T : class
        => _exclusion.Add(type);

    //Remove takes a type out of the exclusion set and adds it to the inclusion set, maps to vanilla remove
    //Still passing after Remove but reverse-marked as explicitly included
    public void Remove<T>(DataComponentType<T> type) where T : class
    {
        _exclusion.Remove(type);
        _inclusion.Add(type);
    }

    //IsFiltered whether a type is filtered out
    //Returns true on an exclusion set hit, false otherwise
    public bool IsFiltered<T>(DataComponentType<T> type) where T : class
        => IsFilteredObject(type);

    //IsFilteredObject non-generic version for the Func<object,bool> inside PredicateDataComponentMap
    public bool IsFilteredObject(object type)
        => _exclusion.Contains(type);

    //IsExplicitlyIncluded whether a type is explicitly marked as included
    //Used by the Container codec to decide whether to force-keep certain components
    public bool IsExplicitlyIncluded<T>(DataComponentType<T> type) where T : class
        => _inclusion.Contains(type);

    //Filter filters a DataComponentMap by the mask and returns a new map keeping only non-excluded components
    //Matches vanilla filter, producing a new DataComponentMap after applying the exclusion rules
    public DataComponentMap Filter(DataComponentMap map)
    {
        if (IsEmpty) return map;
        //Wraps the original map with PredicateDataComponentMap to filter by the mask without copying
        return new PredicateDataComponentMap(map, IsFilteredObject);
    }

    //PredicateDataComponentMap a view wrapping the original map filtered by a predicate
    //Get goes through the original map but returns null for filtered types
    private sealed class PredicateDataComponentMap : DataComponentMap
    {
        private readonly DataComponentMap _delegate;
        private readonly Func<object, bool> _isFiltered;

        public PredicateDataComponentMap(DataComponentMap map, Func<object, bool> isFiltered)
        {
            _delegate = map;
            _isFiltered = isFiltered;
        }

        public T? Get<T>(DataComponentType<T> type) where T : class
            => _isFiltered(type) ? null : _delegate.Get(type);

        public IEnumerable<object> KeySet
            => _delegate.KeySet.Where(t => !_isFiltered(t));
    }
}
