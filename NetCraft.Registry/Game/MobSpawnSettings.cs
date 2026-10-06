using NetCraft.Codec;
using NetCraft.Registry.Codec;

namespace NetCraft.Registry;

//MobCategory mob category, maps to vanilla net.minecraft.world.entity.MobCategory
public enum MobCategory
{
    Monster,
    Creature,
    Ambient,
    Axolotls,
    UndergroundWaterCreature,
    WaterCreature,
    WaterAmbient,
    Misc
}

//SpawnerData single spawn entry, maps to vanilla MobSpawnSettings.SpawnerData
//In the real JSON, weight is flattened with type/minCount/maxCount and provided by the weighted list wrapper
//The vanilla constructor replaces MISC-category entities with pig; EntityType here does not hold MobCategory and cannot decide, so the original id is preserved
public sealed class SpawnerData
{
    public static readonly Codec<SpawnerData> Codec = new ValidatedCodec<SpawnerData>(
        RecordCodecBuilder.Of4<SpawnerData, Identifier, int, int, int>(
            IdentifierCodec.Instance.FieldOf("type").ForGetter<SpawnerData, Identifier>(d => d.Type),
            BiomeCodecs.PositiveInt.FieldOf("minCount").ForGetter<SpawnerData, int>(d => d.MinCount),
            BiomeCodecs.PositiveInt.FieldOf("maxCount").ForGetter<SpawnerData, int>(d => d.MaxCount),
            BiomeCodecs.NonNegativeInt.FieldOf("weight").ForGetter<SpawnerData, int>(d => d.Weight),
            (type, minCount, maxCount, weight) => new SpawnerData(type, minCount, maxCount, weight)),
        spawner => spawner.MinCount <= spawner.MaxCount
            ? DataResult<SpawnerData>.Success(spawner)
            : DataResult<SpawnerData>.Error(() => "minCount needs to be smaller or equal to maxCount"));

    public Identifier Type { get; }

    public int MinCount { get; }

    public int MaxCount { get; }

    public int Weight { get; }

    public SpawnerData(Identifier type, int minCount, int maxCount, int weight)
    {
        Type = type;
        MinCount = minCount;
        MaxCount = maxCount;
        Weight = weight;
    }
}

//MobSpawnCost spawn cost, maps to vanilla MobSpawnSettings.MobSpawnCost
public sealed class MobSpawnCost
{
    public static readonly Codec<MobSpawnCost> Codec = RecordCodecBuilder.Of2<MobSpawnCost, double, double>(
        Codecs.Double.FieldOf("energy_budget").ForGetter<MobSpawnCost, double>(c => c.EnergyBudget),
        Codecs.Double.FieldOf("charge").ForGetter<MobSpawnCost, double>(c => c.Charge),
        (energyBudget, charge) => new MobSpawnCost(energyBudget, charge));

    public double EnergyBudget { get; }

    public double Charge { get; }

    public MobSpawnCost(double energyBudget, double charge)
    {
        EnergyBudget = energyBudget;
        Charge = charge;
    }
}

//MobSpawnSettings mob spawn settings, maps to vanilla MobSpawnSettings
public sealed class MobSpawnSettings
{
    public const float DefaultCreatureSpawnProbability = 0.1f;

    public static readonly MobSpawnSettings Empty = new(
        DefaultCreatureSpawnProbability,
        new Dictionary<MobCategory, IReadOnlyList<SpawnerData>>(),
        new Dictionary<Identifier, MobSpawnCost>());

    //CategoryCodec mob category enum codec; key names match vanilla MobCategory serialized names
    public static readonly Codec<MobCategory> CategoryCodec = new StringEnumCodec<MobCategory>(
        (MobCategory.Monster, "monster"),
        (MobCategory.Creature, "creature"),
        (MobCategory.Ambient, "ambient"),
        (MobCategory.Axolotls, "axolotls"),
        (MobCategory.UndergroundWaterCreature, "underground_water_creature"),
        (MobCategory.WaterCreature, "water_creature"),
        (MobCategory.WaterAmbient, "water_ambient"),
        (MobCategory.Misc, "misc"));

    //SpawnersCodec weighted spawn list by category, the spawners field
    public static readonly Codec<IReadOnlyDictionary<MobCategory, IReadOnlyList<SpawnerData>>> SpawnersCodec =
        new SimpleMapCodec<MobCategory, IReadOnlyList<SpawnerData>>(CategoryCodec, SpawnerData.Codec.ListOf());

    //SpawnCostsCodec spawn cost by entity type id, the spawn_costs field
    public static readonly Codec<IReadOnlyDictionary<Identifier, MobSpawnCost>> SpawnCostsCodec =
        new SimpleMapCodec<Identifier, MobSpawnCost>(IdentifierCodec.Instance, MobSpawnCost.Codec);

    private static readonly Codec<float> CreatureSpawnProbabilityCodec = new FloatRangeCodec(0.0f, 0.9999999f);

    public static readonly Codec<MobSpawnSettings> Codec = RecordCodecBuilder.Of3<MobSpawnSettings, float, IReadOnlyDictionary<MobCategory, IReadOnlyList<SpawnerData>>, IReadOnlyDictionary<Identifier, MobSpawnCost>>(
        CreatureSpawnProbabilityCodec.OptionalFieldOf("creature_spawn_probability", DefaultCreatureSpawnProbability)
            .ForGetter<MobSpawnSettings, float>(s => s.CreatureSpawnProbability),
        SpawnersCodec.FieldOf("spawners")
            .ForGetter<MobSpawnSettings, IReadOnlyDictionary<MobCategory, IReadOnlyList<SpawnerData>>>(s => s.Spawners),
        SpawnCostsCodec.FieldOf("spawn_costs")
            .ForGetter<MobSpawnSettings, IReadOnlyDictionary<Identifier, MobSpawnCost>>(s => s.SpawnCosts),
        (creatureSpawnProbability, spawners, spawnCosts) =>
            new MobSpawnSettings(creatureSpawnProbability, spawners, spawnCosts));

    public float CreatureSpawnProbability { get; }

    public IReadOnlyDictionary<MobCategory, IReadOnlyList<SpawnerData>> Spawners { get; }

    public IReadOnlyDictionary<Identifier, MobSpawnCost> SpawnCosts { get; }

    public MobSpawnSettings(float creatureSpawnProbability,
        IReadOnlyDictionary<MobCategory, IReadOnlyList<SpawnerData>> spawners,
        IReadOnlyDictionary<Identifier, MobSpawnCost> spawnCosts)
    {
        CreatureSpawnProbability = creatureSpawnProbability;
        Spawners = spawners;
        SpawnCosts = spawnCosts;
    }
}
