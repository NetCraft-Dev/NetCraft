using NetCraft.Nbt;
using NetCraft.Registry;

namespace NetCraft.Storage;

//GameRuleMapData 游戏规则存档对应原版 GameRuleMap SavedData
//存 data/minecraft/game_rules.dat 规则名到值 bool 存字节 int 存整数对齐原版 dispatchedMap 编码
//规则运行时语义由 Game 层接入 /gamerule 命令时消费本类只管持久化
public sealed class GameRuleMapData : SavedData
{
    //TypeId 存档标识对应原版 SavedDataType 的 minecraft:game_rules
    private const string TypeId = "minecraft:game_rules";

    //Type SavedData 工厂空档创建空规则集
    public static readonly SavedDataType<GameRuleMapData> Type = new GameRuleMapType();

    private readonly Dictionary<string, object> _rules = new();

    public override string Id => TypeId;

    //Get 取规则值不存在返回默认 仅支持 bool/int 两种值类型
    public T? Get<T>(string name)
        => _rules.TryGetValue(name, out var value) && value is T typed ? typed : default;

    //GetBool 取布尔规则 没存过时回退规则自身的默认值 对应原版 GameRuleMap.get
    public bool GetBool(GameRule<object> rule)
        => _rules.TryGetValue(rule.Id.ToShortString(), out var value) && value is bool flag
            ? flag
            : (bool)rule.DefaultValue;

    //GetInt 取整数规则 没存过时回退规则自身的默认值
    public int GetInt(GameRule<object> rule)
        => _rules.TryGetValue(rule.Id.ToShortString(), out var value) && value is int number
            ? number
            : (int)rule.DefaultValue;

    //SetRule 按规则定义写值 值类型必须与规则类型一致
    public void SetRule(GameRule<object> rule, object value) => Set(rule.Id.ToShortString(), value);

    //Set 写规则值并标脏
    public void Set(string name, object value)
    {
        _rules[name] = value;
        SetDirty();
    }

    //Contains 是否存有指定规则
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

    //Save 写全部非默认规则空规则集写空 compound
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
