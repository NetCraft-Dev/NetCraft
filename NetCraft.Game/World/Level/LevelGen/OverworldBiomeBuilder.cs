using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen;

//OverworldBiomeBuilder maps overworld climate parameter points to biomes, maps to vanilla net.minecraft.world.level.biome.OverworldBiomeBuilder
//Made up of three groups: offshore, the 13 weirdness inland slices, and caves; the data source for MultiNoisePreset.Overworld
public sealed class OverworldBiomeBuilder
{
    private const float LOW_START = 0.26666668f;
    public const float HIGH_START = 0.4f;
    private const float HIGH_END = 0.93333334f;
    public const float PEAK_START = 0.56666666f;
    private const float PEAK_END = 0.7666667f;
    public const float NEAR_INLAND_START = -0.11f;
    public const float MID_INLAND_START = 0.03f;
    public const float FAR_INLAND_START = 0.3f;
    public const float EROSION_INDEX_1_START = -0.78f;
    public const float EROSION_INDEX_2_START = -0.375f;

    private readonly Climate.Parameter FULL_RANGE = Climate.Parameter.Span(-1.0f, 1.0f);

    private readonly Climate.Parameter[] temperatures =
    {
        Climate.Parameter.Span(-1.0f, -0.45f),
        Climate.Parameter.Span(-0.45f, -0.15f),
        Climate.Parameter.Span(-0.15f, 0.2f),
        Climate.Parameter.Span(0.2f, 0.55f),
        Climate.Parameter.Span(0.55f, 1.0f)
    };

    private readonly Climate.Parameter[] humidities =
    {
        Climate.Parameter.Span(-1.0f, -0.35f),
        Climate.Parameter.Span(-0.35f, -0.1f),
        Climate.Parameter.Span(-0.1f, 0.1f),
        Climate.Parameter.Span(0.1f, 0.3f),
        Climate.Parameter.Span(0.3f, 1.0f)
    };

    private readonly Climate.Parameter[] erosions =
    {
        Climate.Parameter.Span(-1.0f, -0.78f),
        Climate.Parameter.Span(-0.78f, -0.375f),
        Climate.Parameter.Span(-0.375f, -0.2225f),
        Climate.Parameter.Span(-0.2225f, 0.05f),
        Climate.Parameter.Span(0.05f, 0.45f),
        Climate.Parameter.Span(0.45f, 0.55f),
        Climate.Parameter.Span(0.55f, 1.0f)
    };

    private readonly Climate.Parameter FROZEN_RANGE;
    private readonly Climate.Parameter UNFROZEN_RANGE;
    private readonly Climate.Parameter mushroomFieldsContinentalness = Climate.Parameter.Span(-1.2f, -1.05f);
    private readonly Climate.Parameter deepOceanContinentalness = Climate.Parameter.Span(-1.05f, -0.455f);
    private readonly Climate.Parameter oceanContinentalness = Climate.Parameter.Span(-0.455f, -0.19f);
    private readonly Climate.Parameter coastContinentalness = Climate.Parameter.Span(-0.19f, -0.11f);
    private readonly Climate.Parameter inlandContinentalness = Climate.Parameter.Span(-0.11f, 0.55f);
    private readonly Climate.Parameter nearInlandContinentalness = Climate.Parameter.Span(-0.11f, 0.03f);
    private readonly Climate.Parameter midInlandContinentalness = Climate.Parameter.Span(0.03f, 0.3f);
    private readonly Climate.Parameter farInlandContinentalness = Climate.Parameter.Span(0.3f, 1.0f);

    private readonly ResourceKey<Biome>[][] OCEANS =
    {
        new[]
        {
            Biomes.DEEP_FROZEN_OCEAN, Biomes.DEEP_COLD_OCEAN, Biomes.DEEP_OCEAN,
            Biomes.DEEP_LUKEWARM_OCEAN, Biomes.WARM_OCEAN
        },
        new[]
        {
            Biomes.FROZEN_OCEAN, Biomes.COLD_OCEAN, Biomes.OCEAN,
            Biomes.LUKEWARM_OCEAN, Biomes.WARM_OCEAN
        }
    };

    private readonly ResourceKey<Biome>[][] MIDDLE_BIOMES =
    {
        new[] { Biomes.SNOWY_PLAINS, Biomes.SNOWY_PLAINS, Biomes.SNOWY_PLAINS, Biomes.SNOWY_TAIGA, Biomes.TAIGA },
        new[] { Biomes.PLAINS, Biomes.PLAINS, Biomes.FOREST, Biomes.TAIGA, Biomes.OLD_GROWTH_SPRUCE_TAIGA },
        new[] { Biomes.FLOWER_FOREST, Biomes.PLAINS, Biomes.FOREST, Biomes.BIRCH_FOREST, Biomes.DARK_FOREST },
        new[] { Biomes.SAVANNA, Biomes.SAVANNA, Biomes.FOREST, Biomes.JUNGLE, Biomes.JUNGLE },
        new[] { Biomes.DESERT, Biomes.DESERT, Biomes.DESERT, Biomes.DESERT, Biomes.DESERT }
    };

    private readonly ResourceKey<Biome>?[][] MIDDLE_BIOMES_VARIANT =
    {
        new ResourceKey<Biome>?[] { Biomes.ICE_SPIKES, null, Biomes.SNOWY_TAIGA, null, null },
        new ResourceKey<Biome>?[] { null, null, null, null, Biomes.OLD_GROWTH_PINE_TAIGA },
        new ResourceKey<Biome>?[] { Biomes.SUNFLOWER_PLAINS, null, null, Biomes.OLD_GROWTH_BIRCH_FOREST, null },
        new ResourceKey<Biome>?[] { null, null, Biomes.PLAINS, Biomes.SPARSE_JUNGLE, Biomes.BAMBOO_JUNGLE },
        new ResourceKey<Biome>?[] { null, null, null, null, null }
    };

    private readonly ResourceKey<Biome>[][] PLATEAU_BIOMES =
    {
        new[] { Biomes.SNOWY_PLAINS, Biomes.SNOWY_PLAINS, Biomes.SNOWY_PLAINS, Biomes.SNOWY_TAIGA, Biomes.SNOWY_TAIGA },
        new[] { Biomes.MEADOW, Biomes.MEADOW, Biomes.FOREST, Biomes.TAIGA, Biomes.OLD_GROWTH_SPRUCE_TAIGA },
        new[] { Biomes.MEADOW, Biomes.MEADOW, Biomes.MEADOW, Biomes.MEADOW, Biomes.PALE_GARDEN },
        new[] { Biomes.SAVANNA_PLATEAU, Biomes.SAVANNA_PLATEAU, Biomes.FOREST, Biomes.FOREST, Biomes.JUNGLE },
        new[] { Biomes.BADLANDS, Biomes.BADLANDS, Biomes.BADLANDS, Biomes.WOODED_BADLANDS, Biomes.WOODED_BADLANDS }
    };

    private readonly ResourceKey<Biome>?[][] PLATEAU_BIOMES_VARIANT =
    {
        new ResourceKey<Biome>?[] { Biomes.ICE_SPIKES, null, null, null, null },
        new ResourceKey<Biome>?[] { Biomes.CHERRY_GROVE, null, Biomes.MEADOW, Biomes.MEADOW, Biomes.OLD_GROWTH_PINE_TAIGA },
        new ResourceKey<Biome>?[] { Biomes.CHERRY_GROVE, Biomes.CHERRY_GROVE, Biomes.FOREST, Biomes.BIRCH_FOREST, null },
        new ResourceKey<Biome>?[] { null, null, null, null, null },
        new ResourceKey<Biome>?[] { Biomes.ERODED_BADLANDS, Biomes.ERODED_BADLANDS, null, null, null }
    };

    private readonly ResourceKey<Biome>?[][] SHATTERED_BIOMES =
    {
        new ResourceKey<Biome>?[]
        {
            Biomes.WINDSWEPT_GRAVELLY_HILLS, Biomes.WINDSWEPT_GRAVELLY_HILLS, Biomes.WINDSWEPT_HILLS,
            Biomes.WINDSWEPT_FOREST, Biomes.WINDSWEPT_FOREST
        },
        new ResourceKey<Biome>?[]
        {
            Biomes.WINDSWEPT_GRAVELLY_HILLS, Biomes.WINDSWEPT_GRAVELLY_HILLS, Biomes.WINDSWEPT_HILLS,
            Biomes.WINDSWEPT_FOREST, Biomes.WINDSWEPT_FOREST
        },
        new ResourceKey<Biome>?[]
        {
            Biomes.WINDSWEPT_HILLS, Biomes.WINDSWEPT_HILLS, Biomes.WINDSWEPT_HILLS,
            Biomes.WINDSWEPT_FOREST, Biomes.WINDSWEPT_FOREST
        },
        new ResourceKey<Biome>?[] { null, null, null, null, null },
        new ResourceKey<Biome>?[] { null, null, null, null, null }
    };

    public OverworldBiomeBuilder()
    {
        FROZEN_RANGE = temperatures[0];
        UNFROZEN_RANGE = Climate.Parameter.Span(temperatures[1], temperatures[4]);
    }

    //SpawnTarget parameter points searched for the spawn point, maps to vanilla spawnTarget
    public IReadOnlyList<Climate.ParameterPoint> SpawnTarget()
    {
        var surfaceDepth = Climate.Parameter.Point(0.0f);
        return new[]
        {
            new Climate.ParameterPoint(FULL_RANGE, FULL_RANGE,
                Climate.Parameter.Span(inlandContinentalness, FULL_RANGE), FULL_RANGE, surfaceDepth,
                Climate.Parameter.Span(-1.0f, -0.16f), 0L),
            new Climate.ParameterPoint(FULL_RANGE, FULL_RANGE,
                Climate.Parameter.Span(inlandContinentalness, FULL_RANGE), FULL_RANGE, surfaceDepth,
                Climate.Parameter.Span(0.16f, 1.0f), 0L)
        };
    }

    //AddBiomes emits every overworld parameter point entry, maps to vanilla addBiomes
    public void AddBiomes(Action<(Climate.ParameterPoint Point, ResourceKey<Biome> Biome)> consumer)
    {
        AddOffCoastBiomes(consumer);
        AddInlandBiomes(consumer);
        AddUndergroundBiomes(consumer);
    }

    //NetherBiomes the five hard-coded parameters of the Nether preset, maps to vanilla Preset.NETHER
    public static IReadOnlyList<(Climate.ParameterPoint Point, ResourceKey<Biome> Biome)> NetherBiomes()
        => new[]
        {
            (Climate.Parameters(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f), Biomes.NETHER_WASTES),
            (Climate.Parameters(0.0f, -0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f), Biomes.SOUL_SAND_VALLEY),
            (Climate.Parameters(0.4f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f), Biomes.CRIMSON_FOREST),
            (Climate.Parameters(0.0f, 0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 0.375f), Biomes.WARPED_FOREST),
            (Climate.Parameters(-0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.175f), Biomes.BASALT_DELTAS)
        };

    //IsDeepDarkRegion whether the sample point falls in the deep dark region, maps to vanilla isDeepDarkRegion
    //When the aquifer computes fluid levels, hitting this region drives floodedness far negative and rules out fluid entirely
    public static bool IsDeepDarkRegion(DensityFunction erosion, DensityFunction depth, FunctionContext context)
        => erosion.Compute(context) < -0.22499999403953552 && depth.Compute(context) > 0.8999999761581421;

    private void AddOffCoastBiomes(Action<(Climate.ParameterPoint Point, ResourceKey<Biome> Biome)> biomes)
    {
        AddSurfaceBiome(biomes, FULL_RANGE, FULL_RANGE, mushroomFieldsContinentalness, FULL_RANGE,
            FULL_RANGE, 0.0f, Biomes.MUSHROOM_FIELDS);
        for (var temperatureIndex = 0; temperatureIndex < temperatures.Length; temperatureIndex++)
        {
            var temperature = temperatures[temperatureIndex];
            AddSurfaceBiome(biomes, temperature, FULL_RANGE, deepOceanContinentalness, FULL_RANGE,
                FULL_RANGE, 0.0f, OCEANS[0][temperatureIndex]);
            AddSurfaceBiome(biomes, temperature, FULL_RANGE, oceanContinentalness, FULL_RANGE,
                FULL_RANGE, 0.0f, OCEANS[1][temperatureIndex]);
        }
    }

    private void AddInlandBiomes(Action<(Climate.ParameterPoint Point, ResourceKey<Biome> Biome)> biomes)
    {
        AddMidSlice(biomes, Climate.Parameter.Span(-1.0f, -0.93333334f));
        AddHighSlice(biomes, Climate.Parameter.Span(-0.93333334f, -0.7666667f));
        AddPeaks(biomes, Climate.Parameter.Span(-0.7666667f, -0.56666666f));
        AddHighSlice(biomes, Climate.Parameter.Span(-0.56666666f, -0.4f));
        AddMidSlice(biomes, Climate.Parameter.Span(-0.4f, -0.26666668f));
        AddLowSlice(biomes, Climate.Parameter.Span(-0.26666668f, -0.05f));
        AddValleys(biomes, Climate.Parameter.Span(-0.05f, 0.05f));
        AddLowSlice(biomes, Climate.Parameter.Span(0.05f, LOW_START));
        AddMidSlice(biomes, Climate.Parameter.Span(LOW_START, 0.4f));
        AddHighSlice(biomes, Climate.Parameter.Span(0.4f, 0.56666666f));
        AddPeaks(biomes, Climate.Parameter.Span(0.56666666f, PEAK_END));
        AddHighSlice(biomes, Climate.Parameter.Span(PEAK_END, HIGH_END));
        AddMidSlice(biomes, Climate.Parameter.Span(HIGH_END, 1.0f));
    }

    private void AddPeaks(Action<(Climate.ParameterPoint Point, ResourceKey<Biome> Biome)> biomes,
        Climate.Parameter weirdness)
    {
        for (var temperatureIndex = 0; temperatureIndex < temperatures.Length; temperatureIndex++)
        {
            var temperature = temperatures[temperatureIndex];
            for (var humidityIndex = 0; humidityIndex < humidities.Length; humidityIndex++)
            {
                var humidity = humidities[humidityIndex];
                var middleBiome = PickMiddleBiome(temperatureIndex, humidityIndex, weirdness);
                var middleBiomeOrBadlandsIfHot = PickMiddleBiomeOrBadlandsIfHot(temperatureIndex, humidityIndex, weirdness);
                var middleBiomeOrBadlandsIfHotOrSlopeIfCold =
                    PickMiddleBiomeOrBadlandsIfHotOrSlopeIfCold(temperatureIndex, humidityIndex, weirdness);
                var plateauBiome = PickPlateauBiome(temperatureIndex, humidityIndex, weirdness);
                var shatteredBiome = PickShatteredBiome(temperatureIndex, humidityIndex, weirdness);
                var shatteredBiomeOrWindsweptSavanna =
                    MaybePickWindsweptSavannaBiome(temperatureIndex, humidityIndex, weirdness, shatteredBiome);
                var peakBiome = PickPeakBiome(temperatureIndex, humidityIndex, weirdness);
                AddSurfaceBiome(biomes, temperature, humidity,
                    Climate.Parameter.Span(coastContinentalness, farInlandContinentalness), erosions[0],
                    weirdness, 0.0f, peakBiome);
                AddSurfaceBiome(biomes, temperature, humidity,
                    Climate.Parameter.Span(coastContinentalness, nearInlandContinentalness), erosions[1],
                    weirdness, 0.0f, middleBiomeOrBadlandsIfHotOrSlopeIfCold);
                AddSurfaceBiome(biomes, temperature, humidity,
                    Climate.Parameter.Span(midInlandContinentalness, farInlandContinentalness), erosions[1],
                    weirdness, 0.0f, peakBiome);
                AddSurfaceBiome(biomes, temperature, humidity,
                    Climate.Parameter.Span(coastContinentalness, nearInlandContinentalness),
                    Climate.Parameter.Span(erosions[2], erosions[3]), weirdness, 0.0f, middleBiome);
                AddSurfaceBiome(biomes, temperature, humidity,
                    Climate.Parameter.Span(midInlandContinentalness, farInlandContinentalness), erosions[2],
                    weirdness, 0.0f, plateauBiome);
                AddSurfaceBiome(biomes, temperature, humidity, midInlandContinentalness, erosions[3],
                    weirdness, 0.0f, middleBiomeOrBadlandsIfHot);
                AddSurfaceBiome(biomes, temperature, humidity, farInlandContinentalness, erosions[3],
                    weirdness, 0.0f, plateauBiome);
                AddSurfaceBiome(biomes, temperature, humidity,
                    Climate.Parameter.Span(coastContinentalness, farInlandContinentalness), erosions[4],
                    weirdness, 0.0f, middleBiome);
                AddSurfaceBiome(biomes, temperature, humidity,
                    Climate.Parameter.Span(coastContinentalness, nearInlandContinentalness), erosions[5],
                    weirdness, 0.0f, shatteredBiomeOrWindsweptSavanna);
                AddSurfaceBiome(biomes, temperature, humidity,
                    Climate.Parameter.Span(midInlandContinentalness, farInlandContinentalness), erosions[5],
                    weirdness, 0.0f, shatteredBiome);
                AddSurfaceBiome(biomes, temperature, humidity,
                    Climate.Parameter.Span(coastContinentalness, farInlandContinentalness), erosions[6],
                    weirdness, 0.0f, middleBiome);
            }
        }
    }

    private void AddHighSlice(Action<(Climate.ParameterPoint Point, ResourceKey<Biome> Biome)> biomes,
        Climate.Parameter weirdness)
    {
        for (var temperatureIndex = 0; temperatureIndex < temperatures.Length; temperatureIndex++)
        {
            var temperature = temperatures[temperatureIndex];
            for (var humidityIndex = 0; humidityIndex < humidities.Length; humidityIndex++)
            {
                var humidity = humidities[humidityIndex];
                var middleBiome = PickMiddleBiome(temperatureIndex, humidityIndex, weirdness);
                var middleBiomeOrBadlandsIfHot = PickMiddleBiomeOrBadlandsIfHot(temperatureIndex, humidityIndex, weirdness);
                var middleBiomeOrBadlandsIfHotOrSlopeIfCold =
                    PickMiddleBiomeOrBadlandsIfHotOrSlopeIfCold(temperatureIndex, humidityIndex, weirdness);
                var plateauBiome = PickPlateauBiome(temperatureIndex, humidityIndex, weirdness);
                var shatteredBiome = PickShatteredBiome(temperatureIndex, humidityIndex, weirdness);
                var middleBiomeOrWindsweptSavanna =
                    MaybePickWindsweptSavannaBiome(temperatureIndex, humidityIndex, weirdness, middleBiome);
                var slopeBiome = PickSlopeBiome(temperatureIndex, humidityIndex, weirdness);
                var peakBiome = PickPeakBiome(temperatureIndex, humidityIndex, weirdness);
                AddSurfaceBiome(biomes, temperature, humidity, coastContinentalness,
                    Climate.Parameter.Span(erosions[0], erosions[1]), weirdness, 0.0f, middleBiome);
                AddSurfaceBiome(biomes, temperature, humidity, nearInlandContinentalness, erosions[0],
                    weirdness, 0.0f, slopeBiome);
                AddSurfaceBiome(biomes, temperature, humidity,
                    Climate.Parameter.Span(midInlandContinentalness, farInlandContinentalness), erosions[0],
                    weirdness, 0.0f, peakBiome);
                AddSurfaceBiome(biomes, temperature, humidity, nearInlandContinentalness, erosions[1],
                    weirdness, 0.0f, middleBiomeOrBadlandsIfHotOrSlopeIfCold);
                AddSurfaceBiome(biomes, temperature, humidity,
                    Climate.Parameter.Span(midInlandContinentalness, farInlandContinentalness), erosions[1],
                    weirdness, 0.0f, slopeBiome);
                AddSurfaceBiome(biomes, temperature, humidity,
                    Climate.Parameter.Span(coastContinentalness, nearInlandContinentalness),
                    Climate.Parameter.Span(erosions[2], erosions[3]), weirdness, 0.0f, middleBiome);
                AddSurfaceBiome(biomes, temperature, humidity,
                    Climate.Parameter.Span(midInlandContinentalness, farInlandContinentalness), erosions[2],
                    weirdness, 0.0f, plateauBiome);
                AddSurfaceBiome(biomes, temperature, humidity, midInlandContinentalness, erosions[3],
                    weirdness, 0.0f, middleBiomeOrBadlandsIfHot);
                AddSurfaceBiome(biomes, temperature, humidity, farInlandContinentalness, erosions[3],
                    weirdness, 0.0f, plateauBiome);
                AddSurfaceBiome(biomes, temperature, humidity,
                    Climate.Parameter.Span(coastContinentalness, farInlandContinentalness), erosions[4],
                    weirdness, 0.0f, middleBiome);
                AddSurfaceBiome(biomes, temperature, humidity,
                    Climate.Parameter.Span(coastContinentalness, nearInlandContinentalness), erosions[5],
                    weirdness, 0.0f, middleBiomeOrWindsweptSavanna);
                AddSurfaceBiome(biomes, temperature, humidity,
                    Climate.Parameter.Span(midInlandContinentalness, farInlandContinentalness), erosions[5],
                    weirdness, 0.0f, shatteredBiome);
                AddSurfaceBiome(biomes, temperature, humidity,
                    Climate.Parameter.Span(coastContinentalness, farInlandContinentalness), erosions[6],
                    weirdness, 0.0f, middleBiome);
            }
        }
    }

    private void AddMidSlice(Action<(Climate.ParameterPoint Point, ResourceKey<Biome> Biome)> biomes,
        Climate.Parameter weirdness)
    {
        AddSurfaceBiome(biomes, FULL_RANGE, FULL_RANGE, coastContinentalness,
            Climate.Parameter.Span(erosions[0], erosions[2]), weirdness, 0.0f, Biomes.STONY_SHORE);
        AddSurfaceBiome(biomes, Climate.Parameter.Span(temperatures[1], temperatures[2]), FULL_RANGE,
            Climate.Parameter.Span(nearInlandContinentalness, farInlandContinentalness), erosions[6],
            weirdness, 0.0f, Biomes.SWAMP);
        AddSurfaceBiome(biomes, Climate.Parameter.Span(temperatures[3], temperatures[4]), FULL_RANGE,
            Climate.Parameter.Span(nearInlandContinentalness, farInlandContinentalness), erosions[6],
            weirdness, 0.0f, Biomes.MANGROVE_SWAMP);
        for (var temperatureIndex = 0; temperatureIndex < temperatures.Length; temperatureIndex++)
        {
            var temperature = temperatures[temperatureIndex];
            for (var humidityIndex = 0; humidityIndex < humidities.Length; humidityIndex++)
            {
                var humidity = humidities[humidityIndex];
                var middleBiome = PickMiddleBiome(temperatureIndex, humidityIndex, weirdness);
                var middleBiomeOrBadlandsIfHot = PickMiddleBiomeOrBadlandsIfHot(temperatureIndex, humidityIndex, weirdness);
                var middleBiomeOrBadlandsIfHotOrSlopeIfCold =
                    PickMiddleBiomeOrBadlandsIfHotOrSlopeIfCold(temperatureIndex, humidityIndex, weirdness);
                var shatteredBiome = PickShatteredBiome(temperatureIndex, humidityIndex, weirdness);
                var plateauBiome = PickPlateauBiome(temperatureIndex, humidityIndex, weirdness);
                var beachBiome = PickBeachBiome(temperatureIndex, humidityIndex);
                var middleBiomeOrWindsweptSavanna =
                    MaybePickWindsweptSavannaBiome(temperatureIndex, humidityIndex, weirdness, middleBiome);
                var shatteredCoastBiome = PickShatteredCoastBiome(temperatureIndex, humidityIndex, weirdness);
                var slopeBiome = PickSlopeBiome(temperatureIndex, humidityIndex, weirdness);
                AddSurfaceBiome(biomes, temperature, humidity,
                    Climate.Parameter.Span(nearInlandContinentalness, farInlandContinentalness), erosions[0],
                    weirdness, 0.0f, slopeBiome);
                AddSurfaceBiome(biomes, temperature, humidity,
                    Climate.Parameter.Span(nearInlandContinentalness, midInlandContinentalness), erosions[1],
                    weirdness, 0.0f, middleBiomeOrBadlandsIfHotOrSlopeIfCold);
                AddSurfaceBiome(biomes, temperature, humidity, farInlandContinentalness, erosions[1],
                    weirdness, 0.0f, temperatureIndex == 0 ? slopeBiome : plateauBiome);
                AddSurfaceBiome(biomes, temperature, humidity, nearInlandContinentalness, erosions[2],
                    weirdness, 0.0f, middleBiome);
                AddSurfaceBiome(biomes, temperature, humidity, midInlandContinentalness, erosions[2],
                    weirdness, 0.0f, middleBiomeOrBadlandsIfHot);
                AddSurfaceBiome(biomes, temperature, humidity, farInlandContinentalness, erosions[2],
                    weirdness, 0.0f, plateauBiome);
                AddSurfaceBiome(biomes, temperature, humidity,
                    Climate.Parameter.Span(coastContinentalness, nearInlandContinentalness), erosions[3],
                    weirdness, 0.0f, middleBiome);
                AddSurfaceBiome(biomes, temperature, humidity,
                    Climate.Parameter.Span(midInlandContinentalness, farInlandContinentalness), erosions[3],
                    weirdness, 0.0f, middleBiomeOrBadlandsIfHot);
                if (weirdness.Max < 0)
                {
                    AddSurfaceBiome(biomes, temperature, humidity, coastContinentalness, erosions[4],
                        weirdness, 0.0f, beachBiome);
                    AddSurfaceBiome(biomes, temperature, humidity,
                        Climate.Parameter.Span(nearInlandContinentalness, farInlandContinentalness), erosions[4],
                        weirdness, 0.0f, middleBiome);
                }
                else
                {
                    AddSurfaceBiome(biomes, temperature, humidity,
                        Climate.Parameter.Span(coastContinentalness, farInlandContinentalness), erosions[4],
                        weirdness, 0.0f, middleBiome);
                }
                AddSurfaceBiome(biomes, temperature, humidity, coastContinentalness, erosions[5],
                    weirdness, 0.0f, shatteredCoastBiome);
                AddSurfaceBiome(biomes, temperature, humidity, nearInlandContinentalness, erosions[5],
                    weirdness, 0.0f, middleBiomeOrWindsweptSavanna);
                AddSurfaceBiome(biomes, temperature, humidity,
                    Climate.Parameter.Span(midInlandContinentalness, farInlandContinentalness), erosions[5],
                    weirdness, 0.0f, shatteredBiome);
                if (weirdness.Max < 0)
                {
                    AddSurfaceBiome(biomes, temperature, humidity, coastContinentalness, erosions[6],
                        weirdness, 0.0f, beachBiome);
                }
                else
                {
                    AddSurfaceBiome(biomes, temperature, humidity, coastContinentalness, erosions[6],
                        weirdness, 0.0f, middleBiome);
                }
                if (temperatureIndex == 0)
                {
                    AddSurfaceBiome(biomes, temperature, humidity,
                        Climate.Parameter.Span(nearInlandContinentalness, farInlandContinentalness), erosions[6],
                        weirdness, 0.0f, middleBiome);
                }
            }
        }
    }

    private void AddLowSlice(Action<(Climate.ParameterPoint Point, ResourceKey<Biome> Biome)> biomes,
        Climate.Parameter weirdness)
    {
        AddSurfaceBiome(biomes, FULL_RANGE, FULL_RANGE, coastContinentalness,
            Climate.Parameter.Span(erosions[0], erosions[2]), weirdness, 0.0f, Biomes.STONY_SHORE);
        AddSurfaceBiome(biomes, Climate.Parameter.Span(temperatures[1], temperatures[2]), FULL_RANGE,
            Climate.Parameter.Span(nearInlandContinentalness, farInlandContinentalness), erosions[6],
            weirdness, 0.0f, Biomes.SWAMP);
        AddSurfaceBiome(biomes, Climate.Parameter.Span(temperatures[3], temperatures[4]), FULL_RANGE,
            Climate.Parameter.Span(nearInlandContinentalness, farInlandContinentalness), erosions[6],
            weirdness, 0.0f, Biomes.MANGROVE_SWAMP);
        for (var temperatureIndex = 0; temperatureIndex < temperatures.Length; temperatureIndex++)
        {
            var temperature = temperatures[temperatureIndex];
            for (var humidityIndex = 0; humidityIndex < humidities.Length; humidityIndex++)
            {
                var humidity = humidities[humidityIndex];
                var middleBiome = PickMiddleBiome(temperatureIndex, humidityIndex, weirdness);
                var middleBiomeOrBadlandsIfHot = PickMiddleBiomeOrBadlandsIfHot(temperatureIndex, humidityIndex, weirdness);
                var middleBiomeOrBadlandsIfHotOrSlopeIfCold =
                    PickMiddleBiomeOrBadlandsIfHotOrSlopeIfCold(temperatureIndex, humidityIndex, weirdness);
                var beachBiome = PickBeachBiome(temperatureIndex, humidityIndex);
                var middleBiomeOrWindsweptSavanna =
                    MaybePickWindsweptSavannaBiome(temperatureIndex, humidityIndex, weirdness, middleBiome);
                var shatteredCoastBiome = PickShatteredCoastBiome(temperatureIndex, humidityIndex, weirdness);
                AddSurfaceBiome(biomes, temperature, humidity, nearInlandContinentalness,
                    Climate.Parameter.Span(erosions[0], erosions[1]), weirdness, 0.0f, middleBiomeOrBadlandsIfHot);
                AddSurfaceBiome(biomes, temperature, humidity,
                    Climate.Parameter.Span(midInlandContinentalness, farInlandContinentalness),
                    Climate.Parameter.Span(erosions[0], erosions[1]), weirdness, 0.0f,
                    middleBiomeOrBadlandsIfHotOrSlopeIfCold);
                AddSurfaceBiome(biomes, temperature, humidity, nearInlandContinentalness,
                    Climate.Parameter.Span(erosions[2], erosions[3]), weirdness, 0.0f, middleBiome);
                AddSurfaceBiome(biomes, temperature, humidity,
                    Climate.Parameter.Span(midInlandContinentalness, farInlandContinentalness),
                    Climate.Parameter.Span(erosions[2], erosions[3]), weirdness, 0.0f, middleBiomeOrBadlandsIfHot);
                AddSurfaceBiome(biomes, temperature, humidity, coastContinentalness,
                    Climate.Parameter.Span(erosions[3], erosions[4]), weirdness, 0.0f, beachBiome);
                AddSurfaceBiome(biomes, temperature, humidity,
                    Climate.Parameter.Span(nearInlandContinentalness, farInlandContinentalness), erosions[4],
                    weirdness, 0.0f, middleBiome);
                AddSurfaceBiome(biomes, temperature, humidity, coastContinentalness, erosions[5],
                    weirdness, 0.0f, shatteredCoastBiome);
                AddSurfaceBiome(biomes, temperature, humidity, nearInlandContinentalness, erosions[5],
                    weirdness, 0.0f, middleBiomeOrWindsweptSavanna);
                AddSurfaceBiome(biomes, temperature, humidity,
                    Climate.Parameter.Span(midInlandContinentalness, farInlandContinentalness), erosions[5],
                    weirdness, 0.0f, middleBiome);
                AddSurfaceBiome(biomes, temperature, humidity, coastContinentalness, erosions[6],
                    weirdness, 0.0f, beachBiome);
                if (temperatureIndex == 0)
                {
                    AddSurfaceBiome(biomes, temperature, humidity,
                        Climate.Parameter.Span(nearInlandContinentalness, farInlandContinentalness), erosions[6],
                        weirdness, 0.0f, middleBiome);
                }
            }
        }
    }

    private void AddValleys(Action<(Climate.ParameterPoint Point, ResourceKey<Biome> Biome)> biomes,
        Climate.Parameter weirdness)
    {
        AddSurfaceBiome(biomes, FROZEN_RANGE, FULL_RANGE, coastContinentalness,
            Climate.Parameter.Span(erosions[0], erosions[1]), weirdness, 0.0f,
            weirdness.Max < 0 ? Biomes.STONY_SHORE : Biomes.FROZEN_RIVER);
        AddSurfaceBiome(biomes, UNFROZEN_RANGE, FULL_RANGE, coastContinentalness,
            Climate.Parameter.Span(erosions[0], erosions[1]), weirdness, 0.0f,
            weirdness.Max < 0 ? Biomes.STONY_SHORE : Biomes.RIVER);
        AddSurfaceBiome(biomes, FROZEN_RANGE, FULL_RANGE, nearInlandContinentalness,
            Climate.Parameter.Span(erosions[0], erosions[1]), weirdness, 0.0f, Biomes.FROZEN_RIVER);
        AddSurfaceBiome(biomes, UNFROZEN_RANGE, FULL_RANGE, nearInlandContinentalness,
            Climate.Parameter.Span(erosions[0], erosions[1]), weirdness, 0.0f, Biomes.RIVER);
        AddSurfaceBiome(biomes, FROZEN_RANGE, FULL_RANGE,
            Climate.Parameter.Span(coastContinentalness, farInlandContinentalness),
            Climate.Parameter.Span(erosions[2], erosions[5]), weirdness, 0.0f, Biomes.FROZEN_RIVER);
        AddSurfaceBiome(biomes, UNFROZEN_RANGE, FULL_RANGE,
            Climate.Parameter.Span(coastContinentalness, farInlandContinentalness),
            Climate.Parameter.Span(erosions[2], erosions[5]), weirdness, 0.0f, Biomes.RIVER);
        AddSurfaceBiome(biomes, FROZEN_RANGE, FULL_RANGE, coastContinentalness, erosions[6],
            weirdness, 0.0f, Biomes.FROZEN_RIVER);
        AddSurfaceBiome(biomes, UNFROZEN_RANGE, FULL_RANGE, coastContinentalness, erosions[6],
            weirdness, 0.0f, Biomes.RIVER);
        AddSurfaceBiome(biomes, Climate.Parameter.Span(temperatures[1], temperatures[2]), FULL_RANGE,
            Climate.Parameter.Span(inlandContinentalness, farInlandContinentalness), erosions[6],
            weirdness, 0.0f, Biomes.SWAMP);
        AddSurfaceBiome(biomes, Climate.Parameter.Span(temperatures[3], temperatures[4]), FULL_RANGE,
            Climate.Parameter.Span(inlandContinentalness, farInlandContinentalness), erosions[6],
            weirdness, 0.0f, Biomes.MANGROVE_SWAMP);
        AddSurfaceBiome(biomes, FROZEN_RANGE, FULL_RANGE,
            Climate.Parameter.Span(inlandContinentalness, farInlandContinentalness), erosions[6],
            weirdness, 0.0f, Biomes.FROZEN_RIVER);
        for (var temperatureIndex = 0; temperatureIndex < temperatures.Length; temperatureIndex++)
        {
            var temperature = temperatures[temperatureIndex];
            for (var humidityIndex = 0; humidityIndex < humidities.Length; humidityIndex++)
            {
                var humidity = humidities[humidityIndex];
                var middleBiomeOrBadlandsIfHot =
                    PickMiddleBiomeOrBadlandsIfHot(temperatureIndex, humidityIndex, weirdness);
                AddSurfaceBiome(biomes, temperature, humidity,
                    Climate.Parameter.Span(midInlandContinentalness, farInlandContinentalness),
                    Climate.Parameter.Span(erosions[0], erosions[1]), weirdness, 0.0f, middleBiomeOrBadlandsIfHot);
            }
        }
    }

    private void AddUndergroundBiomes(Action<(Climate.ParameterPoint Point, ResourceKey<Biome> Biome)> biomes)
    {
        AddUndergroundBiome(biomes, FULL_RANGE, FULL_RANGE, Climate.Parameter.Span(0.8f, 1.0f),
            FULL_RANGE, FULL_RANGE, 0.0f, Biomes.DRIPSTONE_CAVES);
        AddUndergroundBiome(biomes, FULL_RANGE, Climate.Parameter.Span(0.7f, 1.0f), FULL_RANGE,
            FULL_RANGE, FULL_RANGE, 0.0f, Biomes.LUSH_CAVES);
        AddUndergroundBiome(biomes, FULL_RANGE, FULL_RANGE,
            Climate.Parameter.Span(coastContinentalness, inlandContinentalness),
            Climate.Parameter.Span(erosions[5], erosions[6]), Climate.Parameter.Span(-1.1f, -0.85f),
            0.0f, Biomes.SULFUR_CAVES);
        AddBottomBiome(biomes, FULL_RANGE, FULL_RANGE, FULL_RANGE,
            Climate.Parameter.Span(erosions[0], erosions[1]), FULL_RANGE, 0.0f, Biomes.DEEP_DARK);
    }

    private ResourceKey<Biome> PickMiddleBiome(int temperatureIndex, int humidityIndex, Climate.Parameter weirdness)
    {
        if (weirdness.Max < 0)
            return MIDDLE_BIOMES[temperatureIndex][humidityIndex];
        var variant = MIDDLE_BIOMES_VARIANT[temperatureIndex][humidityIndex];
        return variant ?? MIDDLE_BIOMES[temperatureIndex][humidityIndex];
    }

    private ResourceKey<Biome> PickMiddleBiomeOrBadlandsIfHot(int temperatureIndex, int humidityIndex,
        Climate.Parameter weirdness)
        => temperatureIndex == 4
            ? PickBadlandsBiome(humidityIndex, weirdness)
            : PickMiddleBiome(temperatureIndex, humidityIndex, weirdness);

    private ResourceKey<Biome> PickMiddleBiomeOrBadlandsIfHotOrSlopeIfCold(int temperatureIndex, int humidityIndex,
        Climate.Parameter weirdness)
        => temperatureIndex == 0
            ? PickSlopeBiome(temperatureIndex, humidityIndex, weirdness)
            : PickMiddleBiomeOrBadlandsIfHot(temperatureIndex, humidityIndex, weirdness);

    private static ResourceKey<Biome> MaybePickWindsweptSavannaBiome(int temperatureIndex, int humidityIndex,
        Climate.Parameter weirdness, ResourceKey<Biome> underlyingBiome)
        => temperatureIndex > 1 && humidityIndex < 4 && weirdness.Max >= 0
            ? Biomes.WINDSWEPT_SAVANNA
            : underlyingBiome;

    private ResourceKey<Biome> PickShatteredCoastBiome(int temperatureIndex, int humidityIndex,
        Climate.Parameter weirdness)
    {
        var beachOrMiddleBiome = weirdness.Max >= 0
            ? PickMiddleBiome(temperatureIndex, humidityIndex, weirdness)
            : PickBeachBiome(temperatureIndex, humidityIndex);
        return MaybePickWindsweptSavannaBiome(temperatureIndex, humidityIndex, weirdness, beachOrMiddleBiome);
    }

    private static ResourceKey<Biome> PickBeachBiome(int temperatureIndex, int humidityIndex)
    {
        if (temperatureIndex == 0)
            return Biomes.SNOWY_BEACH;
        if (temperatureIndex == 4)
            return Biomes.DESERT;
        return Biomes.BEACH;
    }

    private static ResourceKey<Biome> PickBadlandsBiome(int humidityIndex, Climate.Parameter weirdness)
    {
        if (humidityIndex < 2)
            return weirdness.Max < 0 ? Biomes.BADLANDS : Biomes.ERODED_BADLANDS;
        if (humidityIndex < 3)
            return Biomes.BADLANDS;
        return Biomes.WOODED_BADLANDS;
    }

    private ResourceKey<Biome> PickPlateauBiome(int temperatureIndex, int humidityIndex, Climate.Parameter weirdness)
    {
        var variant = PLATEAU_BIOMES_VARIANT[temperatureIndex][humidityIndex];
        if (weirdness.Max >= 0 && variant is not null)
            return variant;
        return PLATEAU_BIOMES[temperatureIndex][humidityIndex];
    }

    private static ResourceKey<Biome> PickPeakBiome(int temperatureIndex, int humidityIndex,
        Climate.Parameter weirdness)
    {
        if (temperatureIndex <= 2)
            return weirdness.Max < 0 ? Biomes.JAGGED_PEAKS : Biomes.FROZEN_PEAKS;
        if (temperatureIndex == 3)
            return Biomes.STONY_PEAKS;
        return PickBadlandsBiome(humidityIndex, weirdness);
    }

    private ResourceKey<Biome> PickSlopeBiome(int temperatureIndex, int humidityIndex, Climate.Parameter weirdness)
    {
        if (temperatureIndex >= 3)
            return PickPlateauBiome(temperatureIndex, humidityIndex, weirdness);
        if (humidityIndex <= 1)
            return Biomes.SNOWY_SLOPES;
        return Biomes.GROVE;
    }

    private ResourceKey<Biome> PickShatteredBiome(int temperatureIndex, int humidityIndex, Climate.Parameter weirdness)
    {
        var biome = SHATTERED_BIOMES[temperatureIndex][humidityIndex];
        return biome ?? PickMiddleBiome(temperatureIndex, humidityIndex, weirdness);
    }

    private static void AddSurfaceBiome(Action<(Climate.ParameterPoint Point, ResourceKey<Biome> Biome)> biomes,
        Climate.Parameter temperature, Climate.Parameter humidity, Climate.Parameter continentalness,
        Climate.Parameter erosion, Climate.Parameter weirdness, float offset, ResourceKey<Biome> second)
    {
        biomes((Climate.Parameters(temperature, humidity, continentalness, erosion,
            Climate.Parameter.Point(0.0f), weirdness, offset), second));
        biomes((Climate.Parameters(temperature, humidity, continentalness, erosion,
            Climate.Parameter.Point(1.0f), weirdness, offset), second));
    }

    private static void AddUndergroundBiome(Action<(Climate.ParameterPoint Point, ResourceKey<Biome> Biome)> biomes,
        Climate.Parameter temperature, Climate.Parameter humidity, Climate.Parameter continentalness,
        Climate.Parameter erosion, Climate.Parameter weirdness, float offset, ResourceKey<Biome> biome)
    {
        biomes((Climate.Parameters(temperature, humidity, continentalness, erosion,
            Climate.Parameter.Span(0.2f, 0.9f), weirdness, offset), biome));
    }

    private static void AddBottomBiome(Action<(Climate.ParameterPoint Point, ResourceKey<Biome> Biome)> biomes,
        Climate.Parameter temperature, Climate.Parameter humidity, Climate.Parameter continentalness,
        Climate.Parameter erosion, Climate.Parameter weirdness, float offset, ResourceKey<Biome> biome)
    {
        biomes((Climate.Parameters(temperature, humidity, continentalness, erosion,
            Climate.Parameter.Point(1.1f), weirdness, offset), biome));
    }
}
