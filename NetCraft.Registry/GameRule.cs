namespace NetCraft.Registry;

//GameRuleType rule value type, maps to vanilla GameRuleType
public enum GameRuleType
{
    Int,
    Bool,
}

//GameRule game rule definition, maps to vanilla net.minecraft.world.level.gamerules.GameRule
//Values are stored by name in GameRuleMapData; this class only carries the metadata needed by commands and runtime
//T is fixed to object so all rules share the single BuiltInRegistries.GAME_RULE table
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

    //Id rule identifier, in short-name form minecraft:max_block_modifications
    public Identifier Id { get; }

    public GameRuleType Type { get; }

    //DefaultValue taken when unset; boolean rules box a bool and integer rules box an int
    public T DefaultValue { get; }

    //Min/Max apply only to integer rules; maps to vanilla IntegerArgumentType.integer(min, max)
    public int Min { get; }

    public int Max { get; }

    public override string ToString() => Id.ToShortString();
}
