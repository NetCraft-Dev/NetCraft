using System.Text.RegularExpressions;
using NetCraft.Codec;
using NetCraft.Registry.Codec;

namespace NetCraft.Registry.State;

//状态定义工厂接口，对应原版 StateDefinition.Factory<O, S>
public interface StateFactory<O, S> where S : StateHolder<O, S>
{
    S Create(O owner, PropertyBase[] propertyKeys, object?[] propertyValues);
}

//状态定义，对应原版 StateDefinition<O, S extends StateHolder<O, S>>
//枚举所有可能状态组合，构建 neighbors 二维数组使 setValue O(1)
//简化：propertiesCodec 暂不实现（依赖 Codec 子系统）
public class StateDefinition<O, S> where S : StateHolder<O, S>
{
    private static readonly Regex NamePattern = new("^[a-z0-9_]+$", RegexOptions.Compiled);

    private readonly O _owner;
    private readonly SortedDictionary<string, PropertyBase> _propertiesByName;
    private readonly IReadOnlyList<S> _states;

    public StateDefinition(Func<O, S> defaultState, O owner, StateFactory<O, S> factory, IDictionary<string, PropertyBase> properties)
    {
        _owner = owner;
        _propertiesByName = new SortedDictionary<string, PropertyBase>(properties);
        _states = _propertiesByName.Count switch
        {
            0 => CreateSingletonState(owner, factory),
            _ => CreateMultiPropertyStates(owner, factory, _propertiesByName)
        };
    }

    //无属性的单一状态
    private static IReadOnlyList<S> CreateSingletonState(O owner, StateFactory<O, S> factory)
    {
        var state = factory.Create(owner, Array.Empty<PropertyBase>(), Array.Empty<object?>());
        state.InitializeNeighbors(CreateEmptyNeighbors());
        return new List<S> { state };
    }

    //笛卡尔积枚举所有状态并构建 neighbors
    private static IReadOnlyList<S> CreateMultiPropertyStates(O owner, StateFactory<O, S> factory, SortedDictionary<string, PropertyBase> propertiesByName)
    {
        var propertyKeys = propertiesByName.Values.ToArray();
        var valuesPerProperty = propertyKeys.Select(p => p.PossibleValuesAsObjects).ToList();
        var statesByValues = new Dictionary<ValueList, S>(ValueListComparer.Instance);
        var states = new List<S>();

        foreach (var combination in CartesianProduct(valuesPerProperty))
        {
            var values = combination.ToList();
            var state = factory.Create(owner, propertyKeys, values.ToArray());
            statesByValues[new ValueList(values)] = state;
            states.Add(state);
        }

        //构建每个状态的 neighbors 二维数组
        foreach (var (values, state) in statesByValues)
        {
            var neighbors = new S[propertyKeys.Length][];
            for (var i = 0; i < propertyKeys.Length; i++)
            {
                var propValues = propertyKeys[i].PossibleValuesAsObjects;
                neighbors[i] = new S[propValues.Count];
                for (var j = 0; j < propValues.Count; j++)
                {
                    var newValues = values.Values.ToList();
                    newValues[i] = propValues[j];
                    neighbors[i][j] = statesByValues[new ValueList(newValues)];
                }
            }
            state.InitializeNeighbors(neighbors);
        }
        return states;
    }

    private static S[][] CreateEmptyNeighbors() => Array.Empty<S[]>();

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

    public O Owner => _owner;

    public IReadOnlyList<S> PossibleStates => _states;

    public S Any() => _states[0];

    public IReadOnlyCollection<PropertyBase> Properties => _propertiesByName.Values;

    public PropertyBase? GetProperty(string name)
        => _propertiesByName.TryGetValue(name, out var p) ? p : null;

    public bool IsSingletonState => _propertiesByName.Count == 0;

    public override string ToString()
        => $"StateDefinition(owner={_owner}, properties=[{string.Join(",", _propertiesByName.Values.Select(p => p.Name))}])";

    //值列表包装，用作字典 key
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

    //构建器，对应原版 StateDefinition.Builder
    public class Builder
    {
        private readonly O _owner;
        private readonly Dictionary<string, PropertyBase> _properties = new();

        public Builder(O owner) => _owner = owner;

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

        public StateDefinition<O, S> Create(Func<O, S> defaultState, StateFactory<O, S> factory)
            => new(defaultState, _owner, factory, _properties);
    }
}

//StateDefinition相关codec对应原版StateDefinition.propertiesCodec
public static class StateDefinitionCodecs
{
    //propertiesCodec对应原版propertiesCodec
    //Name字段编码ownerProperties字段编码属性键值对
    public static Codec<S> PropertiesCodec<O, S>(
        Codec<O> ownerCodec,
        Func<O, StateDefinition<O, S>> definitionGetter)
        where S : StateHolder<O, S>
    {
        var emptyProps = new Dictionary<string, string>();
        return RecordCodecBuilder.Of2<S, O, Dictionary<string, string>>(
            ownerCodec.FieldOf(StateHolder<O, S>.NameTag).ForGetter((S s) => s.Owner),
            StringMapCodec.Instance.OptionalFieldOf(StateHolder<O, S>.PropertiesTag, emptyProps)
                .ForGetter((S s) => StateToPropertiesMap<O, S>(s)),
            (owner, props) => BuildStateFromProperties(owner, props, definitionGetter));
    }

    //从state构建属性字典name到valueName
    private static Dictionary<string, string> StateToPropertiesMap<O, S>(S state)
        where S : StateHolder<O, S>
    {
        var dict = new Dictionary<string, string>();
        foreach (var pv in state.GetValues())
            dict[pv.Property.Name] = pv.ValueName;
        return dict;
    }

    //从owner与属性字典重建state对应原版propertiesCodec的apply
    private static S BuildStateFromProperties<O, S>(
        O owner,
        Dictionary<string, string> props,
        Func<O, StateDefinition<O, S>> definitionGetter)
        where S : StateHolder<O, S>
    {
        var definition = definitionGetter(owner);
        var state = definition.Any();
        foreach (var (key, value) in props)
        {
            var prop = definition.GetProperty(key);
            if (prop is null) continue;
            var val = prop.GetValueForName(value);
            if (val is null) continue;
            state = state.SetValue(prop, val);
        }
        return state;
    }
}
