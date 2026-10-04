using System.Text.RegularExpressions;

namespace NetCraft.Registry.State;

//BlockStateDefinition 非泛型版本专门构建 BlockState struct
//对应原版 StateDefinition<Block, BlockState> 但数据存 BlockStateRegistry
//笛卡尔积枚举所有可能状态构建 neighbors 用 int 索引避免 BlockState 引用
public sealed class BlockStateDefinition
{
    private static readonly Regex NamePattern = new("^[a-z0-9_]+$", RegexOptions.Compiled);

    private readonly Block _owner;
    private readonly SortedDictionary<string, PropertyBase> _propertiesByName;
    private readonly IReadOnlyList<BlockState> _states;

    public BlockStateDefinition(Block owner, IDictionary<string, PropertyBase> properties)
    {
        _owner = owner;
        //原版用 ImmutableSortedMap.copyOf 按属性名做字符串序排列 这里必须用 Ordinal 对齐
        //用默认比较器会走文化相关排序 与 Java 的 UTF-16 码点序在部分字符上不一致
        _propertiesByName = new SortedDictionary<string, PropertyBase>(properties, StringComparer.Ordinal);
        _states = _propertiesByName.Count switch
        {
            0 => CreateSingletonState(owner),
            _ => CreateMultiPropertyStates(owner, _propertiesByName)
        };
    }

    //无属性的单一状态
    private static IReadOnlyList<BlockState> CreateSingletonState(Block owner)
    {
        var state = BlockStateRegistry.Register(owner, Array.Empty<PropertyBase>(), Array.Empty<object?>());
        BlockStateRegistry.InitializeNeighbors(state.Id, Array.Empty<int[]>());
        return new List<BlockState> { state };
    }

    //笛卡尔积枚举所有状态并构建 neighbors 用 int 索引
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

        //构建每个状态的 neighbors 用 int 索引避免 BlockState 引用
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

    //笛卡尔积枚举
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

    //值列表包装用作字典 key
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

    //构建器对应原版 StateDefinition.Builder
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
