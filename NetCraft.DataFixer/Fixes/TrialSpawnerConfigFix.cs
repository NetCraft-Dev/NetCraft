using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;

namespace NetCraft.DataFixer.Fixes;

//审判台配置修复对应原版TrialSpawnerConfigFix
//1.20.5把审判台spawn_range等9个字段移到normal_config子map
public class TrialSpawnerConfigFix : NamedEntityWriteReadFix
{
    private static readonly string[] KEYS_TO_MOVE = {
        "spawn_range", "total_mobs", "simultaneous_mobs",
        "total_mobs_added_per_player", "simultaneous_mobs_added_per_player",
        "ticks_between_spawn", "spawn_potentials",
        "loot_tables_to_eject", "items_to_drop_when_ominous"
    };

    public TrialSpawnerConfigFix(Schema outputSchema)
        : base(outputSchema, true, "Trial Spawner config tag fixer", References.BlockEntity, "minecraft:trial_spawner") { }

    //moveToConfigTag按字段列表收集非空值移到normal_config子map
    private static Dynamic<object> MoveToConfigTag(Dynamic<object> input)
    {
        var map = new List<Pair<Dynamic<object>, Dynamic<object>>>();
        foreach (var key in KEYS_TO_MOVE)
        {
            var maybeValueForKey = input.Get(key).Result();
            if (maybeValueForKey.IsPresent)
            {
                map.Add(new Pair<Dynamic<object>, Dynamic<object>>(input.CreateString(key), maybeValueForKey.Get()));
                input = input.Remove(key);
            }
        }
        return map.Count == 0 ? input : input.Set("normal_config", input.CreateMap(map));
    }

    protected override Dynamic<object> Fix(Dynamic<object> input) => MoveToConfigTag(input);
}
