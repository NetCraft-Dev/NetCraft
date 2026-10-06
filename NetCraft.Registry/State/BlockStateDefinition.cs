using System.Text.RegularExpressions;

namespace NetCraft.Registry.State;

//BlockStateDefinition non-generic version dedicated to building the BlockState struct
//Maps to vanilla StateDefinition<Block, BlockState> but stores data in BlockStateRegistry
//Enumerates all possible states via Cartesian product to build neighbors, using int indexes to avoid BlockState references
public sealed class BlockStateDefinition
{
    private static readonly Regex NamePattern = new("^[a-z0-9_]+$", RegexOptions.Compiled);

    private readonly Block _owner;
    private readonly SortedDictionary<string, PropertyBase> _propertiesByName;
    private readonly IReadOnlyList<BlockState> _states;

    public BlockStateDefinition(Block owner, IDictionary<string, PropertyBase> properties)
    {
        _owner = owner;
        //Vanilla uses ImmutableSortedMap.copyOf to sort by property name in string order; Ordinal must be used here to align
        //Using the default comparer would sort by culture, which differs from Java's UTF-16 code point order on some characters
        _propertiesByName = new SortedDictionary<string, PropertyBase>(properties, StringComparer.Ordinal);
        _states = _propertiesByName.Count switch
        {
            0 => CreateSingletonState(owner),
            _ => CreateMultiPropertyStates(owner, _propertiesByName)
        };
    }

    //A singleton state with no properties
    private static IReadOnlyList<BlockState> CreateSingletonState(Block owner)
    {
        var state = BlockStateRegistry.Register(owner, Array.Empty<PropertyBase>(), Array.Empty<object?>());
        BlockStateRegistry.InitializeNeighbors(state.Id, Array.Empty<int[]>());
        return new List<BlockState> { state };
    }

    //Enumerate all states via Cartesian product and build neighbors using int indexes
    private static IReadOnlyList<BlockState> CreateMultiPropertyStates(Block owner, SortedDictionary<string, PropertyBase> propertiesByName)
    {
        var propertyKeys = propertiesByName.Values.ToArray();
        var valuesPerProperty = propertyKeys.Select(p => p.PossibleValuesAsObjects).ToList();
        var statesByValues = new Dictionary<ValueList, int>(ValueListComparer.Instance);
        var states = new List<BlockState>();

        foreach (var combination in CartesianProduct(valuesPerProperty))
        {
            var values = combination.ToList();
            var state = BlockStateRegistry.Register(owner, propertyKeys, values.ToArray());
            statesByValues[new ValueList(values)] = state.Id;
            states.Add(state);
        }

        //Build the neighbors for each state using int indexes to avoid BlockState references
        foreach (var (values, stateId) in statesByValues)
        {
            var neighbors = new int[propertyKeys.Length][];
            for (var i = 0; i < propertyKeys.Length; i++)
            {
                var propValues = propertyKeys[i].PossibleValuesAsObjects;
                neighbors[i] = new int[propValues.Count];
                for (var j = 0; j < propValues.Count; j++)
                {
                    var newValues = values.Values.ToList();
                    newValues[i] = propValues[j];
                    neighbors[i][j] = statesByValues[new ValueList(newValues)];
                }
            }
            BlockStateRegistry.InitializeNeighbors(stateId, neighbors);
        }
        return states;
    }

    //Cartesian product enumeration
    private static IEnumerable<IEnumerable<object>> CartesianProduct(IReadOnlyList<IReadOnlyList<object>> sequences)
    {
        var result = new List<List<object>> { new() };
        foreach (var sequence in sequences)
        {
            var next = new List<List<object>>();
            foreach (var existing in result)
                foreach (var item in sequence)
                {
                    var copy = new List<object>(existing) { item };
                    next.Add(copy);
                }
            result = next;
        }
        return result;
    }

    public Block Owner => _owner;
    public IReadOnlyList<BlockState> PossibleStates => _states;
    public BlockState Any() => _states[0];
    public IReadOnlyCollection<PropertyBase> Properties => _propertiesByName.Values;
    public bool IsSingletonState => _propertiesByName.Count == 0;

    public PropertyBase? GetProperty(string name)
        => _propertiesByName.TryGetValue(name, out var p) ? p : null;

    public override string ToString()
        => $"BlockStateDefinition(owner={_owner}, properties=[{string.Join(",", _propertiesByName.Values.Select(p => p.Name))}])";

    //Value list wrapper used as a dictionary key
    private sealed class ValueList : IEquatable<ValueList>
    {
        public IReadOnlyList<object> Values { get; }
        private readonly int _hash;

        public ValueList(IReadOnlyList<object> values)
        {
            Values = values;
            var hash = new HashCode();
            foreach (var v in values) hash.Add(v);
            _hash = hash.ToHashCode();
        }

        public bool Equals(ValueList? other)
        {
            if (other is null || other.Values.Count != Values.Count) return false;
            for (var i = 0; i < Values.Count; i++)
                if (!Equals(Values[i], other.Values[i])) return false;
            return true;
        }

        public override bool Equals(object? obj) => obj is ValueList vl && Equals(vl);
        public override int GetHashCode() => _hash;
    }

    private sealed class ValueListComparer : IEqualityComparer<ValueList>
    {
        public static readonly ValueListComparer Instance = new();
        public bool Equals(ValueList? x, ValueList? y) => x?.Equals(y) ?? y is null;
        public int GetHashCode(ValueList obj) => obj.GetHashCode();
    }

    //Builder, maps to vanilla StateDefinition.Builder
    public class Builder
    {
        private readonly Block _owner;
        private readonly Dictionary<string, PropertyBase> _properties = new();

        public Builder(Block owner) => _owner = owner;

        public Builder Add(params PropertyBase[] properties)
        {
            foreach (var property in properties)
            {
                ValidateProperty(property);
                _properties[property.Name] = property;
            }
            return this;
        }

        private void ValidateProperty(PropertyBase property)
        {
            var name = property.Name;
            if (!NamePattern.IsMatch(name))
                throw new ArgumentException($"{_owner} has invalidly named property: {name}");
            if (property.PossibleValuesAsObjects.Count <= 1)
                throw new ArgumentException($"{_owner} attempted use property {name} with <= 1 possible values");
            if (_properties.ContainsKey(name))
                throw new ArgumentException($"{_owner} has duplicate property: {name}");
        }

        public BlockStateDefinition Create() => new(_owner, _properties);
    }
}
