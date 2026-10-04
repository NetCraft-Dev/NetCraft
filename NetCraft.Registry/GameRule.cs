namespace NetCraft.Registry;

//GameRuleType 规则值类型对应原版 GameRuleType
public enum GameRuleType
{
    Int,
    Bool,
}

//GameRule 游戏规则定义对应原版 net.minecraft.world.level.gamerules.GameRule
//值按名字存在 GameRuleMapData 本类只承载命令与运行时需要的元数据
//T 固定取 object 让全部规则共用 BuiltInRegistries.GAME_RULE 一张表
public sealed class GameRule<T>
{
    public GameRule(Identifier id, GameRuleType type, T defaultValue, int min = 0, int max = int.MaxValue)
    {
        Id = id;
        Type = type;
        DefaultValue = defaultValue;
        Min = min;
        Max = max;
    }

    //Id 规则标识符 短名形式 minecraft:max_block_modifications
    public Identifier Id { get; }

    public GameRuleType Type { get; }

    //DefaultValue 未设置时的取值 布尔规则装箱为 bool 整数规则装箱为 int
    public T DefaultValue { get; }

    //Min/Max 只对整数规则生效 对应原版 IntegerArgumentType.integer(min, max)
    public int Min { get; }

    public int Max { get; }

    public override string ToString() => Id.ToShortString();
}
