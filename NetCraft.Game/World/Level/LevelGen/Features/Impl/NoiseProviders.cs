using NetCraft.Codec;
using NetCraft.Game.World.Level.LevelGen.Synth;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util;
using NetCraft.Util.Collection;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl;

//NoiseBasedStateProvider noise block state provider base, maps to vanilla stateproviders.NoiseBasedStateProvider
//Builds the noise instance from the seed at construction; subclasses sample it by coordinate to pick a state
public abstract class NoiseBasedStateProvider : BlockStateProvider
{
    //Seed noise seed, matching the JSON seed field
    public long Seed { get; }

    //Parameters main noise parameters, matching the JSON noise field
    public NoiseParameters Parameters { get; }

    //Scale main noise coordinate scale, matching the JSON scale field
    public float Scale { get; }

    //Noise the main noise instance built at construction; picking a state only samples it, never rebuilds
    protected NormalNoise Noise { get; }

    protected NoiseBasedStateProvider(long seed, NoiseParameters parameters, float scale)
    {
        Seed = seed;
        Parameters = parameters;
        Scale = scale;
        //Vanilla wraps WorldgenRandom(new LegacyRandomSource(seed)); it behaves the same as constructing directly
        Noise = NormalNoise.Create(new LegacyRandomSource(seed), parameters);
    }

    //GetNoiseValue sample the noise with coordinates multiplied by the scale, maps to vanilla getNoiseValue
    protected double GetNoiseValue(BlockPos pos, double scale)
        => Noise.GetValue(pos.X * scale, pos.Y * scale, pos.Z * scale);
}

//NoiseProviderFields the shared fields of noise providers, maps to vanilla NoiseBasedStateProvider.noiseCodec
//seed/noise/scale are shared by the main and threshold providers; generalized here into a field combinator
internal static class NoiseProviderFields
{
    public static FieldCodec<P, long> Seed<P>() where P : NoiseBasedStateProvider
        => Codecs.Long.FieldOf("seed").ForGetter<P, long>(p => p.Seed);

    public static FieldCodec<P, NoiseParameters> Noise<P>() where P : NoiseBasedStateProvider
        => NoiseParameters.Codec.FieldOf("noise").ForGetter<P, NoiseParameters>(p => p.Parameters);

    public static FieldCodec<P, float> Scale<P>() where P : NoiseBasedStateProvider
        => Codecs.Float.FieldOf("scale").ForGetter<P, float>(p => p.Scale);
}

//NoiseProvider noise state provider, registered as noise_provider, maps to vanilla NoiseProvider
//The noise value is normalized and mapped to an index in the states list
public class NoiseProvider : NoiseBasedStateProvider
{
    public static readonly MapCodec<NoiseProvider> MapCodec =
        RecordCodecBuilder.Of4<NoiseProvider, long, NoiseParameters, float, IReadOnlyList<BlockState>>(
            NoiseProviderFields.Seed<NoiseProvider>(),
            NoiseProviderFields.Noise<NoiseProvider>(),
            NoiseProviderFields.Scale<NoiseProvider>(),
            BlockStateCodec.Instance.ListOf().FieldOf("states")
                .ForGetter<NoiseProvider, IReadOnlyList<BlockState>>(p => p.States),
            (seed, parameters, scale, states) => new NoiseProvider(seed, parameters, scale, states));

    //States candidate state list, matching the JSON states field
    public IReadOnlyList<BlockState> States { get; }

    public NoiseProvider(long seed, NoiseParameters parameters, float scale, IReadOnlyList<BlockState> states)
        : base(seed, parameters, scale) => States = states;

    public override BlockStateProviderType Type => NoiseProviderTypes.Noise;

    public override BlockState GetState(WorldGenRegion level, RandomSource random, BlockPos pos)
        => GetRandomState(States, pos, Scale);

    protected BlockState GetRandomState(IReadOnlyList<BlockState> states, BlockPos pos, double scale)
        => GetRandomState(states, GetNoiseValue(pos, scale));

    //GetRandomState map the noise value to [0,1) then index the list, maps to vanilla getRandomState
    protected BlockState GetRandomState(IReadOnlyList<BlockState> states, double noiseValue)
    {
        var placementValue = Mth.Clamp((1.0 + noiseValue) / 2.0, 0.0, 0.9999);
        return states[(int)(placementValue * states.Count)];
    }
}

//DualNoiseProvider dual noise state provider, registered as dual_noise_provider, maps to vanilla DualNoiseProvider
//The slow noise sets how many states are candidates, then the main noise picks one of them
public class DualNoiseProvider : NoiseProvider
{
    public static readonly MapCodec<DualNoiseProvider> MapCodec =
        RecordCodecBuilder.Of7<DualNoiseProvider, IntRange, NoiseParameters, float, long, NoiseParameters,
            float, IReadOnlyList<BlockState>>(
            IntRangeCodec.Instance.FieldOf("variety")
                .ForGetter<DualNoiseProvider, IntRange>(p => p.Variety),
            NoiseParameters.Codec.FieldOf("slow_noise")
                .ForGetter<DualNoiseProvider, NoiseParameters>(p => p.SlowNoiseParameters),
            Codecs.Float.FieldOf("slow_scale").ForGetter<DualNoiseProvider, float>(p => p.SlowScale),
            NoiseProviderFields.Seed<DualNoiseProvider>(),
            NoiseProviderFields.Noise<DualNoiseProvider>(),
            NoiseProviderFields.Scale<DualNoiseProvider>(),
            BlockStateCodec.Instance.ListOf().FieldOf("states")
                .ForGetter<DualNoiseProvider, IReadOnlyList<BlockState>>(p => p.States),
            (variety, slowNoiseParameters, slowScale, seed, parameters, scale, states) =>
                new DualNoiseProvider(variety, slowNoiseParameters, slowScale, seed, parameters, scale, states));

    //Variety candidate state count range, matching the JSON variety field
    public IntRange Variety { get; }

    //SlowNoiseParameters slow noise parameters, matching the JSON slow_noise field
    public NoiseParameters SlowNoiseParameters { get; }

    //SlowScale slow noise coordinate scale, matching the JSON slow_scale field
    public float SlowScale { get; }

    private readonly NormalNoise _slowNoise;

    public DualNoiseProvider(IntRange variety, NoiseParameters slowNoiseParameters, float slowScale,
        long seed, NoiseParameters parameters, float scale, IReadOnlyList<BlockState> states)
        : base(seed, parameters, scale, states)
    {
        Variety = variety;
        SlowNoiseParameters = slowNoiseParameters;
        SlowScale = slowScale;
        //Slow and main noise share the seed, matching vanilla building one instance of each at construction
        _slowNoise = NormalNoise.Create(new LegacyRandomSource(seed), slowNoiseParameters);
    }

    public override BlockStateProviderType Type => NoiseProviderTypes.DualNoise;

    //GetState the slow noise sets the count, each candidate is sampled at an offset coordinate, then the main noise picks one; vanilla consumes no random here
    public override BlockState GetState(WorldGenRegion level, RandomSource random, BlockPos pos)
    {
        var varietyNoise = GetSlowNoiseValue(pos);
        var localVariety = (int)Mth.ClampedMap(varietyNoise, -1.0, 1.0, Variety.Min, Variety.Max + 1);
        var possibleStates = new List<BlockState>(localVariety);
        for (var i = 0; i < localVariety; i++)
            possibleStates.Add(GetRandomState(States, GetSlowNoiseValue(pos.Offset(i * 54545, 0, i * 34234))));
        return GetRandomState(possibleStates, pos, Scale);
    }

    //GetSlowNoiseValue sample the slow noise with coordinates multiplied by the scale, maps to vanilla getSlowNoiseValue
    protected double GetSlowNoiseValue(BlockPos pos)
        => _slowNoise.GetValue(pos.X * SlowScale, pos.Y * SlowScale, pos.Z * SlowScale);
}

//NoiseThresholdProvider noise threshold state provider, registered as noise_threshold_provider, maps to vanilla NoiseThresholdProvider
//Below the threshold pick from low_states, otherwise roll high_chance for high_states; if both miss, use default_state
public sealed class NoiseThresholdProvider : NoiseBasedStateProvider
{
    public static readonly MapCodec<NoiseThresholdProvider> MapCodec =
        RecordCodecBuilder.Of8<NoiseThresholdProvider, long, NoiseParameters, float, float, float,
            BlockState, IReadOnlyList<BlockState>, IReadOnlyList<BlockState>>(
            NoiseProviderFields.Seed<NoiseThresholdProvider>(),
            NoiseProviderFields.Noise<NoiseThresholdProvider>(),
            NoiseProviderFields.Scale<NoiseThresholdProvider>(),
            Codecs.Float.FieldOf("threshold").ForGetter<NoiseThresholdProvider, float>(p => p.Threshold),
            Codecs.Float.FieldOf("high_chance").ForGetter<NoiseThresholdProvider, float>(p => p.HighChance),
            BlockStateCodec.Instance.FieldOf("default_state")
                .ForGetter<NoiseThresholdProvider, BlockState>(p => p.DefaultState),
            BlockStateCodec.Instance.ListOf().FieldOf("low_states")
                .ForGetter<NoiseThresholdProvider, IReadOnlyList<BlockState>>(p => p.LowStates),
            BlockStateCodec.Instance.ListOf().FieldOf("high_states")
                .ForGetter<NoiseThresholdProvider, IReadOnlyList<BlockState>>(p => p.HighStates),
            (seed, parameters, scale, threshold, highChance, defaultState, lowStates, highStates) =>
                new NoiseThresholdProvider(seed, parameters, scale, threshold, highChance, defaultState,
                    lowStates, highStates));

    //Threshold threshold for the low branch, matching the JSON threshold field
    public float Threshold { get; }

    //HighChance probability of picking high_states, matching the JSON high_chance field
    public float HighChance { get; }

    //DefaultState state used when neither branch hits, matching the JSON default_state field
    public BlockState DefaultState { get; }

    //LowStates candidates below the threshold, matching the JSON low_states field
    public IReadOnlyList<BlockState> LowStates { get; }

    //HighStates candidates above the threshold, matching the JSON high_states field
    public IReadOnlyList<BlockState> HighStates { get; }

    public NoiseThresholdProvider(long seed, NoiseParameters parameters, float scale, float threshold,
        float highChance, BlockState defaultState, IReadOnlyList<BlockState> lowStates,
        IReadOnlyList<BlockState> highStates)
        : base(seed, parameters, scale)
    {
        Threshold = threshold;
        HighChance = highChance;
        DefaultState = defaultState;
        LowStates = lowStates;
        HighStates = highStates;
    }

    public override BlockStateProviderType Type => NoiseProviderTypes.NoiseThreshold;

    //GetState compare against the threshold first then roll nextFloat once, same order as vanilla
    public override BlockState GetState(WorldGenRegion level, RandomSource random, BlockPos pos)
    {
        var localValue = GetNoiseValue(pos, Scale);
        if (localValue < Threshold) return RandomCollections.GetRandom(LowStates, random);
        if (random.NextFloat() < HighChance) return RandomCollections.GetRandom(HighStates, random);
        return DefaultState;
    }
}

//IntRange closed integer range, maps to vanilla InclusiveRange<Integer>
public sealed class IntRange
{
    public int Min { get; }
    public int Max { get; }

    public IntRange(int min, int max)
    {
        Min = min;
        Max = max;
    }
}

//IntRangeCodec integer closed range codec, maps to vanilla InclusiveRange.codec(Codec.INT, min, max)
//JSON form is a two-element array [min, max]
internal sealed class IntRangeCodec : ScalarCodec<IntRange>
{
    public static readonly IntRangeCodec Instance = new();

    public override DataResult<IntRange> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetStream(input).FlatMap(stream =>
        {
            var bounds = new List<int>();
            foreach (var element in stream)
            {
                var value = ops.GetNumberValue(element);
                if (!value.Result().IsPresent)
                    return DataResult<IntRange>.Error(() => "range elements must be numbers");
                bounds.Add((int)value.GetOrThrow());
            }
            return bounds.Count == 2
                ? DataResult<IntRange>.Success(new IntRange(bounds[0], bounds[1]))
                : DataResult<IntRange>.Error(() => "range must be a two-element array");
        });

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, IntRange value)
        => DataResult<U>.Success(ops.CreateList(new[] { ops.CreateInt(value.Min), ops.CreateInt(value.Max) }));
}

//NoiseProviderTypes the three noise provider type singletons, maps to the static fields of vanilla BlockStateProviderType
public static class NoiseProviderTypes
{
    public static readonly BlockStateProviderType<NoiseProvider> Noise =
        Register("noise_provider", NoiseProvider.MapCodec);

    public static readonly BlockStateProviderType<DualNoiseProvider> DualNoise =
        Register("dual_noise_provider", DualNoiseProvider.MapCodec);

    public static readonly BlockStateProviderType<NoiseThresholdProvider> NoiseThreshold =
        Register("noise_threshold_provider", NoiseThresholdProvider.MapCodec);

    //Register register into BLOCKSTATE_PROVIDER_TYPE and return the type singleton
    private static BlockStateProviderType<T> Register<T>(string path, MapCodec<T> codec)
        where T : BlockStateProvider
    {
        var type = new SimpleBlockStateProviderType<T>(path, codec);
        Registry<NetCraft.Registry.BlockStateProviderType<object>>.Register(
            BuiltInRegistries.BLOCKSTATE_PROVIDER_TYPE, type.Id, type);
        return type;
    }
}

//NoiseProviderBootstrap noise provider registration entry
//Touching the three type singletons triggers static registration; run once before data loading
public static class NoiseProviderBootstrap
{
    public static void RegisterAll()
    {
        _ = NoiseProviderTypes.Noise;
        _ = NoiseProviderTypes.DualNoise;
        _ = NoiseProviderTypes.NoiseThreshold;
    }
}
