using NetCraft.Codec;
using NetCraft.Game.World.Level.LevelGen.Synth;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util;
using NetCraft.Util.Collection;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl;

//NoiseBasedStateProvider 噪声方块状态提供者基类 对应原版 stateproviders.NoiseBasedStateProvider
//构造期按种子建好噪声实例 子类按坐标取噪声值决定放哪个状态
public abstract class NoiseBasedStateProvider : BlockStateProvider
{
    //Seed 噪声种子 对应 JSON seed 字段
    public long Seed { get; }

    //Parameters 主噪声参数 对应 JSON noise 字段
    public NoiseParameters Parameters { get; }

    //Scale 主噪声坐标缩放 对应 JSON scale 字段
    public float Scale { get; }

    //Noise 构造期建好的主噪声实例 取状态时只查表不重建
    protected NormalNoise Noise { get; }

    protected NoiseBasedStateProvider(long seed, NoiseParameters parameters, float scale)
    {
        Seed = seed;
        Parameters = parameters;
        Scale = scale;
        //原版用 WorldgenRandom(new LegacyRandomSource(seed)) 包装 行为与直接构造一致
        Noise = NormalNoise.Create(new LegacyRandomSource(seed), parameters);
    }

    //GetNoiseValue 坐标乘缩放后取噪声值 对应原版 getNoiseValue
    protected double GetNoiseValue(BlockPos pos, double scale)
        => Noise.GetValue(pos.X * scale, pos.Y * scale, pos.Z * scale);
}

//NoiseProviderFields 噪声提供者的公共字段 对应原版 NoiseBasedStateProvider.noiseCodec
//seed/noise/scale 三字段在主提供者与阈值提供者里共用 这里泛型化成单字段组合器
internal static class NoiseProviderFields
{
    public static FieldCodec<P, long> Seed<P>() where P : NoiseBasedStateProvider
        => Codecs.Long.FieldOf("seed").ForGetter<P, long>(p => p.Seed);

    public static FieldCodec<P, NoiseParameters> Noise<P>() where P : NoiseBasedStateProvider
        => NoiseParameters.Codec.FieldOf("noise").ForGetter<P, NoiseParameters>(p => p.Parameters);

    public static FieldCodec<P, float> Scale<P>() where P : NoiseBasedStateProvider
        => Codecs.Float.FieldOf("scale").ForGetter<P, float>(p => p.Scale);
}

//NoiseProvider 噪声状态提供者 注册名 noise_provider 对应原版 NoiseProvider
//噪声值归一化后映射到 states 列表下标
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

    //States 候选状态列表 对应 JSON states 字段
    public IReadOnlyList<BlockState> States { get; }

    public NoiseProvider(long seed, NoiseParameters parameters, float scale, IReadOnlyList<BlockState> states)
        : base(seed, parameters, scale) => States = states;

    public override BlockStateProviderType Type => NoiseProviderTypes.Noise;

    public override BlockState GetState(WorldGenRegion level, RandomSource random, BlockPos pos)
        => GetRandomState(States, pos, Scale);

    protected BlockState GetRandomState(IReadOnlyList<BlockState> states, BlockPos pos, double scale)
        => GetRandomState(states, GetNoiseValue(pos, scale));

    //GetRandomState 噪声值映射到 [0,1) 后取列表下标 对应原版 getRandomState
    protected BlockState GetRandomState(IReadOnlyList<BlockState> states, double noiseValue)
    {
        var placementValue = Mth.Clamp((1.0 + noiseValue) / 2.0, 0.0, 0.9999);
        return states[(int)(placementValue * states.Count)];
    }
}

//DualNoiseProvider 双噪声状态提供者 注册名 dual_noise_provider 对应原版 DualNoiseProvider
//慢噪声决定候选状态条数 主噪声再在候选里选一个
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

    //Variety 候选状态条数区间 对应 JSON variety 字段
    public IntRange Variety { get; }

    //SlowNoiseParameters 慢噪声参数 对应 JSON slow_noise 字段
    public NoiseParameters SlowNoiseParameters { get; }

    //SlowScale 慢噪声坐标缩放 对应 JSON slow_scale 字段
    public float SlowScale { get; }

    private readonly NormalNoise _slowNoise;

    public DualNoiseProvider(IntRange variety, NoiseParameters slowNoiseParameters, float slowScale,
        long seed, NoiseParameters parameters, float scale, IReadOnlyList<BlockState> states)
        : base(seed, parameters, scale, states)
    {
        Variety = variety;
        SlowNoiseParameters = slowNoiseParameters;
        SlowScale = slowScale;
        //慢噪声与主噪声同种子 对应原版构造期各建一个实例
        _slowNoise = NormalNoise.Create(new LegacyRandomSource(seed), slowNoiseParameters);
    }

    public override BlockStateProviderType Type => NoiseProviderTypes.DualNoise;

    //GetState 慢噪声定条数 逐条按偏移坐标取候选 主噪声再挑一个 原版不消耗 random
    public override BlockState GetState(WorldGenRegion level, RandomSource random, BlockPos pos)
    {
        var varietyNoise = GetSlowNoiseValue(pos);
        var localVariety = (int)Mth.ClampedMap(varietyNoise, -1.0, 1.0, Variety.Min, Variety.Max + 1);
        var possibleStates = new List<BlockState>(localVariety);
        for (var i = 0; i < localVariety; i++)
            possibleStates.Add(GetRandomState(States, GetSlowNoiseValue(pos.Offset(i * 54545, 0, i * 34234))));
        return GetRandomState(possibleStates, pos, Scale);
    }

    //GetSlowNoiseValue 慢噪声按坐标乘缩放取值 对应原版 getSlowNoiseValue
    protected double GetSlowNoiseValue(BlockPos pos)
        => _slowNoise.GetValue(pos.X * SlowScale, pos.Y * SlowScale, pos.Z * SlowScale);
}

//NoiseThresholdProvider 噪声阈值状态提供者 注册名 noise_threshold_provider 对应原版 NoiseThresholdProvider
//低于阈值取 low_states 否则按 high_chance 随机取 high_states 都落空用 default_state
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

    //Threshold 低值判定阈值 对应 JSON threshold 字段
    public float Threshold { get; }

    //HighChance 取 high_states 的概率 对应 JSON high_chance 字段
    public float HighChance { get; }

    //DefaultState 两路都没命中时用的状态 对应 JSON default_state 字段
    public BlockState DefaultState { get; }

    //LowStates 低于阈值时的候选 对应 JSON low_states 字段
    public IReadOnlyList<BlockState> LowStates { get; }

    //HighStates 高于阈值时的候选 对应 JSON high_states 字段
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

    //GetState 先比阈值 再掷一次 nextFloat 顺序与原版一致
    public override BlockState GetState(WorldGenRegion level, RandomSource random, BlockPos pos)
    {
        var localValue = GetNoiseValue(pos, Scale);
        if (localValue < Threshold) return RandomCollections.GetRandom(LowStates, random);
        if (random.NextFloat() < HighChance) return RandomCollections.GetRandom(HighStates, random);
        return DefaultState;
    }
}

//IntRange 闭区间整数区间 对应原版 InclusiveRange<Integer>
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

//IntRangeCodec 整数闭区间编解码 对应原版 InclusiveRange.codec(Codec.INT, min, max)
//JSON 形态为两元素数组 [min, max]
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
                    return DataResult<IntRange>.Error(() => "区间元素必须是数字");
                bounds.Add((int)value.GetOrThrow());
            }
            return bounds.Count == 2
                ? DataResult<IntRange>.Success(new IntRange(bounds[0], bounds[1]))
                : DataResult<IntRange>.Error(() => "区间必须是两元素数组");
        });

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, IntRange value)
        => DataResult<U>.Success(ops.CreateList(new[] { ops.CreateInt(value.Min), ops.CreateInt(value.Max) }));
}

//NoiseProviderTypes 三个噪声提供者类型单例 对应原版 BlockStateProviderType 的静态字段
public static class NoiseProviderTypes
{
    public static readonly BlockStateProviderType<NoiseProvider> Noise =
        Register("noise_provider", NoiseProvider.MapCodec);

    public static readonly BlockStateProviderType<DualNoiseProvider> DualNoise =
        Register("dual_noise_provider", DualNoiseProvider.MapCodec);

    public static readonly BlockStateProviderType<NoiseThresholdProvider> NoiseThreshold =
        Register("noise_threshold_provider", NoiseThresholdProvider.MapCodec);

    //Register 登记进 BLOCKSTATE_PROVIDER_TYPE 并返回类型单例
    private static BlockStateProviderType<T> Register<T>(string path, MapCodec<T> codec)
        where T : BlockStateProvider
    {
        var type = new SimpleBlockStateProviderType<T>(path, codec);
        Registry<NetCraft.Registry.BlockStateProviderType<object>>.Register(
            BuiltInRegistries.BLOCKSTATE_PROVIDER_TYPE, type.Id, type);
        return type;
    }
}

//NoiseProviderBootstrap 噪声提供者注册入口
//触碰三个类型单例触发静态注册 数据加载前先跑一次即可
public static class NoiseProviderBootstrap
{
    public static void RegisterAll()
    {
        _ = NoiseProviderTypes.Noise;
        _ = NoiseProviderTypes.DualNoise;
        _ = NoiseProviderTypes.NoiseThreshold;
    }
}
