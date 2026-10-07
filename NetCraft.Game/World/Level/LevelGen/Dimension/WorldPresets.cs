using System.Text.Json.Nodes;
using NetCraft.Codec;
using NetCraft.Logging;
using NetCraft.Registry;
using NetCraft.Resources;

namespace NetCraft.Game.World.Level.LevelGen.Dimension;

//WorldPresets world preset loading, maps to vanilla net.minecraft.world.level.levelgen.presets.WorldPresets
//Since 26.2 level stems no longer have their own directory; they are embedded in the dimensions section of data/<ns>/worldgen/world_preset/<preset>.json
//Reads that section and registers each dimension definition into the LEVEL_STEM registry, keyed by the dimension id
public static class WorldPresets
{
    //Normal default world preset, maps to vanilla minecraft:normal
    public static readonly Identifier Normal = Identifier.WithDefaultNamespace("normal");

    //Load load every dimension definition of the given preset
    //Returns the registered dimension keys; returns an empty list without error when the pack has no such preset file
    public static IReadOnlyList<Identifier> Load(ResourceManager resourceManager, RegistryAccess context,
        Identifier presetId)
    {
        var location = Identifier.FromNamespaceAndPath(presetId.Namespace,
            $"worldgen/world_preset/{presetId.Path}.json");
        if (resourceManager.GetResource(PackType.ServerData, location) is not { } resource)
            return Array.Empty<Identifier>();

        JsonNode? node;
        using (var stream = resource.Open())
        {
            node = JsonOps.Parse(stream).GetOrThrow();
        }

        var ops = new RegistryOps<JsonNode?>(JsonOps.Instance, context);
        var stems = Parse(ops, node, presetId);
        var registry = (WritableRegistry<NetCraft.Registry.LevelStem>)BuiltInRegistries.LEVEL_STEM;
        var loaded = new List<Identifier>(stems.Count);
        foreach (var (dimensionId, stem) in stems)
        {
            //Do not overwrite an existing dimension of the same name; let the first loaded one win
            if (!registry.ContainsKey(dimensionId))
                registry.Register(ResourceKey<NetCraft.Registry.LevelStem>.Create(registry.Key, dimensionId), stem,
                    RegistrationInfo.BuiltIn);
            loaded.Add(dimensionId);
        }
        return loaded;
    }

    //Parse parse the dimensions section of the preset JSON, decoding a level stem per dimension
    //A failing dimension only degrades itself so the others still build, preventing one data issue from blocking the whole server
    public static IReadOnlyList<(Identifier Id, LevelStem Stem)> Parse(RegistryOps<JsonNode?> ops, JsonNode? root,
        Identifier presetId)
    {
        if (root is not JsonObject jsonRoot || jsonRoot["dimensions"] is not JsonObject dimensions)
            throw new InvalidDataException($"world preset {presetId} is missing the dimensions section");

        var stems = new List<(Identifier, LevelStem)>(dimensions.Count);
        foreach (var (key, value) in dimensions)
        {
            var parsedId = Identifier.TryParse(key);
            if (parsedId is null || value is null)
            {
                Log.Warning($"World preset {presetId} dimension {key} has an invalid definition, skipped");
                continue;
            }
            var parsed = LevelStem.Codec.Parse(ops, value);
            if (!parsed.Result().IsPresent)
            {
                Log.Warning($"World preset {presetId} dimension {key} failed to parse, skipped");
                continue;
            }
            stems.Add((parsedId.Value, parsed.GetOrThrow()));
        }
        return stems;
    }

    //Get get a loaded dimension definition; returns null when not loaded
    public static LevelStem? Get(Identifier dimensionId)
        => BuiltInRegistries.LEVEL_STEM.GetValue(dimensionId) as LevelStem;
}
