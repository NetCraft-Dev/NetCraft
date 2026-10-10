using NetCraft.Codec;
using NetCraft.Logging;
using NetCraft.Registry;
using NetCraft.Resources;

namespace NetCraft.Game.World.Loot;

//LootTables the loaded loot table set, maps to the loot table half of vanilla ReloadableRegistries
public sealed class LootTables
{
    //Active the set the running server reads, swapped wholesale on every reload
    public static LootTables Active { get; set; } = new(new Dictionary<Identifier, LootTable>());

    private readonly Dictionary<Identifier, LootTable> _tables;

    private LootTables(Dictionary<Identifier, LootTable> tables) => _tables = tables;

    public int Count => _tables.Count;

    //Get returns the table registered under id, null when absent
    public LootTable? Get(Identifier id) => _tables.GetValueOrDefault(id);

    //Load reads every data/<namespace>/loot_table/**.json into a table set, maps to vanilla LootTableManager.apply
    //A single malformed table is reported and skipped so one bad file cannot take down the reload
    public static LootTables Load(ResourceManager resources)
    {
        const string folder = "loot_table";
        var tables = new Dictionary<Identifier, LootTable>();
        var failed = 0;
        foreach (var ns in resources.GetNamespaces(PackType.ServerData))
        {
            foreach (var resource in resources.ListResources(PackType.ServerData, ns, folder))
            {
                var path = resource.Location.Path;
                if (!path.EndsWith(".json", StringComparison.Ordinal)) continue;
                var id = resource.Location.WithPath(path[(folder.Length + 1)..^".json".Length]);
                try
                {
                    using var stream = resource.Open();
                    var node = JsonOps.Parse(stream).GetOrThrow();
                    tables[id] = LootTable.CODEC.Parse(JsonOps.Instance, node).GetOrThrow();
                }
                catch (Exception e)
                {
                    failed++;
                    Log.Warning($"Loot table {id} failed to load: {e.Message}");
                }
            }
        }
        Log.Info($"Loaded {tables.Count} loot table(s), {failed} failed");
        return new LootTables(tables);
    }
}
