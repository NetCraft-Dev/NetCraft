using NetCraft.Nbt;
using NetCraft.Registry;

namespace NetCraft.Storage;

//GameRuleMapData, game rule save data, maps to vanilla GameRuleMap SavedData
//Stored in data/minecraft/game_rules.dat, mapping rule names to values; bool is stored as a byte and int as an integer, matching the vanilla dispatchedMap encoding
//Runtime rule semantics are consumed by the Game layer when the /gamerule command is wired in; this class only handles persistence
public sealed class GameRuleMapData : SavedData
{
    //TypeId, the save identifier, maps to the minecraft:game_rules of the vanilla SavedDataType
    private const string TypeId = "minecraft:game_rules";

    //Type, the SavedData factory; an empty tag creates an empty rule set
    public static readonly SavedDataType<GameRuleMapData> Type = new GameRuleMapType();

    private readonly Dictionary<string, object> _rules = new();

    public override string Id => TypeId;

    //Get returns the rule value, or default when absent; only bool and int value types are supported
    public T? Get<T>(string name)
        => _rules.TryGetValue(name, out var value) && value is T typed ? typed : default;

    //GetBool gets a boolean rule; falls back to the rule's own default when never stored, maps to vanilla GameRuleMap.get
    public bool GetBool(GameRule<object> rule)
        => _rules.TryGetValue(rule.Id.ToShortString(), out var value) && value is bool flag
            ? flag
            : (bool)rule.DefaultValue;

    //GetInt gets an integer rule; falls back to the rule's own default when never stored
    public int GetInt(GameRule<object> rule)
        => _rules.TryGetValue(rule.Id.ToShortString(), out var value) && value is int number
            ? number
            : (int)rule.DefaultValue;

    //SetRule writes a value by rule definition; the value type must match the rule type
    public void SetRule(GameRule<object> rule, object value) => Set(rule.Id.ToShortString(), value);

    //Set writes a rule value and marks dirty
    public void Set(string name, object value)
    {
        _rules[name] = value;
        SetDirty();
    }

    //Contains, whether the given rule is stored
    public bool Contains(string name) => _rules.ContainsKey(name);

    private sealed class GameRuleMapType : SavedDataType<GameRuleMapData>
    {
        public string Id => TypeId;

        public GameRuleMapData Create(CompoundTag tag, RegistryAccess registryAccess)
        {
            var data = new GameRuleMapData();
            foreach (var (name, value) in tag)
            {
                if (value is IntTag i) data._rules[name] = i.Value;
                else if (value is ByteTag b) data._rules[name] = b.Value != 0;
            }
            return data;
        }
    }

    //Save writes all non-default rules; an empty rule set writes an empty compound
    public override CompoundTag Save(CompoundTag tag)
    {
        foreach (var (name, value) in _rules)
        {
            if (value is bool b) tag.PutBoolean(name, b);
            else if (value is int i) tag.PutInt(name, i);
        }
        return tag;
    }
}
