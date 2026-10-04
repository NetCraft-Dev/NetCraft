using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen;

//Climate 多噪声气候参数系统对应原版 net.minecraft.world.level.levelgen.Climate
//持有 6 个维度参数temperature/humidity/continentalness/erosion/depth/weirdness
//MultiNoiseBiomeSource 用 ParameterPoint 与目标点距离查找最近 Biome
public static class Climate
{
    //Parameter 单维度参数范围对应原版 Climate.Parameter
    //持有 min/max 表示参数取值范围用于参数空间距离计算
    public readonly struct Parameter
    {
        public long Min { get; }
        public long Max { get; }

        public Parameter(long min, long max)
        {
            Min = min;
            Max = max;
        }

        //Single 单值参数 min == max
        public static Parameter Single(long value) => new(value, value);

        //Point 单值参数对应原版 Parameter.point 浮点先量化
        public static Parameter Point(float value) => Span(value, value);

        //Span 浮点区间参数对应原版 Parameter.span(float,float)
        public static Parameter Span(float min, float max)
        {
            if (min > max)
                throw new ArgumentException($"min > max: {min} {max}");
            return new Parameter(QuantizeCoord(min), QuantizeCoord(max));
        }

        //Span 参数区间合并对应原版 Parameter.span(Parameter,Parameter)
        public static Parameter Span(Parameter min, Parameter max)
        {
            if (min.Min > max.Max)
                throw new ArgumentException($"min > max: {min} {max}");
            return new Parameter(min.Min, max.Max);
        }

        //Merge 与另一区间求并集对应原版 Parameter.span(Parameter)
        //只用于 RTree 合并孩子包围盒 不要求两个区间有先后关系
        public Parameter Merge(Parameter other)
            => new(Math.Min(Min, other.Min), Math.Max(Max, other.Max));

        //Value 中心值用于参数空间距离计算对应原版 parameter.spaceToBlock
        public long Value => (Min + Max) >> 1;

        //Range 参数范围跨度
        public long Range => Max - Min;

        public override string ToString() => Min == Max ? $"[{Min}]" : $"[{Min}..{Max}]";
    }

    //ParameterPoint 多维度参数点对应原版 Climate.ParameterPoint
    //持有 6 个 Climate.Parameter 与 offset 用于 MultiNoiseBiomeSource 距离查找
    public sealed class ParameterPoint
    {
        public Parameter Temperature { get; }
        public Parameter Humidity { get; }
        public Parameter Continentalness { get; }
        public Parameter Erosion { get; }
        public Parameter Depth { get; }
        public Parameter Weirdness { get; }
        public long Offset { get; }

        public ParameterPoint(
            Parameter temperature,
            Parameter humidity,
            Parameter continentalness,
            Parameter erosion,
            Parameter depth,
            Parameter weirdness,
            long offset)
        {
            Temperature = temperature;
            Humidity = humidity;
            Continentalness = continentalness;
            Erosion = erosion;
            Depth = depth;
            Weirdness = weirdness;
            Offset = offset;
        }

        //Fitness 参数点与目标气候的适应度对应原版 Climate.ParameterPoint.fitness
        //6 维取区间距离平方和 offset 项原版取自身平方 结果越小越贴合目标
        public long Fitness(TargetPoint target)
            => Square(ParameterDistance(Temperature, target.Temperature))
             + Square(ParameterDistance(Humidity, target.Humidity))
             + Square(ParameterDistance(Continentalness, target.Continentalness))
             + Square(ParameterDistance(Erosion, target.Erosion))
             + Square(ParameterDistance(Depth, target.Depth))
             + Square(ParameterDistance(Weirdness, target.Weirdness))
             + Square(Offset);
    }

    //TargetPoint 某坐标采样出的 6 维气候量化值对应原版 Climate.TargetPoint
    //出生点气候搜索拿它当目标 搜索时深度维固定归零
    public readonly struct TargetPoint
    {
        public long Temperature { get; }
        public long Humidity { get; }
        public long Continentalness { get; }
        public long Erosion { get; }
        public long Depth { get; }
        public long Weirdness { get; }

        public TargetPoint(long temperature, long humidity, long continentalness,
            long erosion, long depth, long weirdness)
        {
            Temperature = temperature;
            Humidity = humidity;
            Continentalness = continentalness;
            Erosion = erosion;
            Depth = depth;
            Weirdness = weirdness;
        }

        //ZeroDepth 深度维归零对应原版出生点搜索里的 zeroDepthTargetPoint
        public TargetPoint ZeroDepth()
            => new(Temperature, Humidity, Continentalness, Erosion, 0L, Weirdness);
    }

    //Sampler 噪声采样器接口对应原版 Climate.Sampler
    //MultiNoiseBiomeSource 用此接口按坐标采样 6 维度参数
    public interface Sampler
    {
        Parameter Temperature(int x, int y, int z);
        Parameter Humidity(int x, int y, int z);
        Parameter Continentalness(int x, int y, int z);
        Parameter Erosion(int x, int y, int z);
        Parameter Depth(int x, int y, int z);
        Parameter Weirdness(int x, int y, int z);
    }

    //ConstantSampler 常量采样器所有维度返回固定参数测试用
    public sealed class ConstantSampler : Sampler
    {
        public Parameter TemperatureValue { get; }
        public Parameter HumidityValue { get; }
        public Parameter ContinentalnessValue { get; }
        public Parameter ErosionValue { get; }
        public Parameter DepthValue { get; }
        public Parameter WeirdnessValue { get; }

        public ConstantSampler(
            Parameter temperature, Parameter humidity, Parameter continentalness,
            Parameter erosion, Parameter depth, Parameter weirdness)
        {
            TemperatureValue = temperature;
            HumidityValue = humidity;
            ContinentalnessValue = continentalness;
            ErosionValue = erosion;
            DepthValue = depth;
            WeirdnessValue = weirdness;
        }

        public Parameter Temperature(int x, int y, int z) => TemperatureValue;
        public Parameter Humidity(int x, int y, int z) => HumidityValue;
        public Parameter Continentalness(int x, int y, int z) => ContinentalnessValue;
        public Parameter Erosion(int x, int y, int z) => ErosionValue;
        public Parameter Depth(int x, int y, int z) => DepthValue;
        public Parameter Weirdness(int x, int y, int z) => WeirdnessValue;
    }

    //NoiseRouterSampler 基于 NoiseRouter 6 个气候维度密度函数的真实采样器
    //对应原版 Climate.Sampler 的 NoiseRouterData 实现
    //把密度值 double 量化为 long 后包装为 Parameter.Single
    //量化精度对齐原版 Climate.quantize 把 double 映射到 long 空间
    public sealed class NoiseRouterSampler : Sampler
    {
        private readonly NoiseRouter _router;

        public NoiseRouterSampler(NoiseRouter router) => _router = router;

        public Parameter Temperature(int x, int y, int z)
            => Parameter.Single(Quantize(_router.Temperature, x, y, z));
        public Parameter Humidity(int x, int y, int z)
            => Parameter.Single(Quantize(_router.Vegetation, x, y, z));
        public Parameter Continentalness(int x, int y, int z)
            => Parameter.Single(Quantize(_router.Continents, x, y, z));
        public Parameter Erosion(int x, int y, int z)
            => Parameter.Single(Quantize(_router.Erosion, x, y, z));
        public Parameter Depth(int x, int y, int z)
            => Parameter.Single(Quantize(_router.Depth, x, y, z));
        public Parameter Weirdness(int x, int y, int z)
            => Parameter.Single(Quantize(_router.Ridges, x, y, z));

        //Quantize 采样密度值后量化为 long 对应原版 Climate.quantizeCoord
        //系数必须与 QuantizeCoord 一致 原版是 ×10000 写成 ×1000 会让采样点整体小十倍 距离全偏
        private static long Quantize(DensityFunction function, int x, int y, int z)
        {
            var ctx = ReusableContext.Set(x, y, z);
            return QuantizeCoord((float)function.Compute(ctx));
        }

        //ReusableContext 逐点复用的采样上下文
        //原版每个采样点 new 一个 SinglePointContext 靠 JIT 标量替换消掉 .NET 的逃逸分析不处理堆对象
        //一次气候采样 6 个维度要 new 6 次 每块近十万次 是分配表里最大的一处
        //采样器被多个生成线程共享 所以按线程各持一个 被调用的密度函数不会把上下文留存
        [ThreadStatic] private static SinglePointContext? _reusableContext;

        private static SinglePointContext ReusableContext
            => _reusableContext ??= new SinglePointContext(0, 0, 0);
    }

    //Distance 计算采样点与参数表条目的参数空间距离 对应原版 Climate.RTree.Node.distance
    //逐维度取区间距离再求平方和 落在条目区间内的维度贡献 0 这正是原版最近邻语义
    //7 个维度都参与 offset 按单值区间处理
    public static long Distance(ParameterPoint target, ParameterPoint point)
    {
        return Square(ParameterDistance(point.Temperature, target.Temperature.Min))
             + Square(ParameterDistance(point.Humidity, target.Humidity.Min))
             + Square(ParameterDistance(point.Continentalness, target.Continentalness.Min))
             + Square(ParameterDistance(point.Erosion, target.Erosion.Min))
             + Square(ParameterDistance(point.Depth, target.Depth.Min))
             + Square(ParameterDistance(point.Weirdness, target.Weirdness.Min))
             + Square(ParameterDistance(Parameter.Single(point.Offset), target.Offset));
    }

    //ParameterDistance 单维度区间到取值的距离 对应原版 Parameter.distance(long)
    //取值大于区间上界取上界差 小于下界取下界差 落在区间内为 0
    private static long ParameterDistance(Parameter span, long target)
    {
        var above = target - span.Max;
        if (above > 0) return above;
        return Math.Max(span.Min - target, 0L);
    }

    private static long Square(long value) => value * value;

    //SpawnSearchMaxRadius 出生点搜索最大半径对应原版 Climate$SpawnFinder.MAX_RADIUS
    private const long SpawnSearchMaxRadius = 2048;

    //FindSpawnPosition 出生点气候径向搜索对应原版 Climate.findSpawnPosition
    //先算原点适应度 再从 512 到 2048 与 32 到 512 各绕一圈 圈上取到更贴合的即替换
    //适应度含到原点的平方距离偏置 同样贴合时优先离原点近的
    public static BlockPos FindSpawnPosition(IReadOnlyList<ParameterPoint> targetClimates, Sampler sampler)
    {
        var best = SpawnCandidateAt(targetClimates, sampler, 0, 0);
        RadialSearch(targetClimates, sampler, 2048f, 512f, ref best);
        RadialSearch(targetClimates, sampler, 512f, 32f, ref best);
        return best.Location;
    }

    //RadialSearch 绕当前最优候选做环形采样对应原版 Climate$SpawnFinder.radialSearch
    //角度步进取半径增量的比值 保证每圈采样点数与半径成比例
    private static void RadialSearch(IReadOnlyList<ParameterPoint> targetClimates, Sampler sampler,
        float maxRadius, float radiusIncrement, ref SpawnCandidate best)
    {
        var angle = 0f;
        var radius = radiusIncrement;
        var origin = best.Location;
        while (radius <= maxRadius)
        {
            var x = origin.X + (int)(Math.Sin(angle) * radius);
            var z = origin.Z + (int)(Math.Cos(angle) * radius);
            var candidate = SpawnCandidateAt(targetClimates, sampler, x, z);
            if (candidate.Fitness < best.Fitness) best = candidate;
            angle += radiusIncrement / radius;
            if (angle > Math.Tau)
            {
                angle = 0f;
                radius += radiusIncrement;
            }
        }
    }

    //SpawnCandidateAt 采样一列气候取与各目标点的最小适应度 对应原版 getSpawnPositionAndFitness
    //深度维固定 0 适应度乘最大半径平方再加到原点的平方距离 距离偏置让搜索别跑太远
    private static SpawnCandidate SpawnCandidateAt(
        IReadOnlyList<ParameterPoint> targetClimates, Sampler sampler, int blockX, int blockZ)
    {
        var zeroDepth = SampleTarget(sampler, blockX, blockZ).ZeroDepth();
        var minFitness = long.MaxValue;
        for (var i = 0; i < targetClimates.Count; i++)
            minFitness = Math.Min(minFitness, targetClimates[i].Fitness(zeroDepth));
        var fitness = minFitness * Square(SpawnSearchMaxRadius) + Square(blockX) + Square(blockZ);
        return new SpawnCandidate(new BlockPos(blockX, 0, blockZ), fitness);
    }

    //SampleTarget 按 block 坐标采样 6 维气候对应原版 Climate.Sampler.sample
    //原版按 quart 采样这里先折算到 quart 对齐的 block 坐标 y 固定 0
    private static TargetPoint SampleTarget(Sampler sampler, int blockX, int blockZ)
    {
        var x = QuartPos.ToBlock(QuartPos.FromBlock(blockX));
        var z = QuartPos.ToBlock(QuartPos.FromBlock(blockZ));
        return new TargetPoint(
            sampler.Temperature(x, 0, z).Min,
            sampler.Humidity(x, 0, z).Min,
            sampler.Continentalness(x, 0, z).Min,
            sampler.Erosion(x, 0, z).Min,
            sampler.Depth(x, 0, z).Min,
            sampler.Weirdness(x, 0, z).Min);
    }

    //SpawnCandidate 出生点候选 记坐标与适应度对应原版 Climate$SpawnFinder$Result
    private readonly struct SpawnCandidate
    {
        public BlockPos Location { get; }
        public long Fitness { get; }

        public SpawnCandidate(BlockPos location, long fitness)
        {
            Location = location;
            Fitness = fitness;
        }
    }

    //QuantizeCoord 浮点参数映射到 long 参数空间对应原版 Climate.quantizeCoord
    public static long QuantizeCoord(float coord) => (long)(coord * 10000.0f);

    //UnquantizeCoord long 参数还原为浮点对应原版 Climate.unquantizeCoord
    public static float UnquantizeCoord(long coord) => coord / 10000.0f;

    //Parameters 六维度浮点单值参数点对应原版 Climate.parameters(float ...)
    public static ParameterPoint Parameters(
        float temperature, float humidity, float continentalness,
        float erosion, float depth, float weirdness, float offset)
        => new(
            Parameter.Point(temperature), Parameter.Point(humidity), Parameter.Point(continentalness),
            Parameter.Point(erosion), Parameter.Point(depth), Parameter.Point(weirdness),
            QuantizeCoord(offset));

    //Parameters 六维度区间参数点对应原版 Climate.parameters(Parameter ...)
    public static ParameterPoint Parameters(
        Parameter temperature, Parameter humidity, Parameter continentalness,
        Parameter erosion, Parameter depth, Parameter weirdness, float offset)
        => new(temperature, humidity, continentalness, erosion, depth, weirdness, QuantizeCoord(offset));

    //ParameterCodec 单维度参数范围 codec 对应原版 Climate.Parameter.CODEC
    //单浮点写为单值范围双元素列表写为范围
    public static readonly Codec<Parameter> ParameterCodec = new ClimateParameterCodec();

    //ParameterPointCodec 六维度参数点 codec 对应原版 Climate.ParameterPoint.CODEC
    public static readonly Codec<ParameterPoint> ParameterPointCodec = BuildParameterPointCodec();

    //BuildParameterPointCodec 六参数 + offset 字段
    private static Codec<ParameterPoint> BuildParameterPointCodec()
        => RecordCodecBuilder.Of7(
            ParameterCodec.FieldOf("temperature").ForGetter<ParameterPoint, Parameter>(p => p.Temperature),
            ParameterCodec.FieldOf("humidity").ForGetter<ParameterPoint, Parameter>(p => p.Humidity),
            ParameterCodec.FieldOf("continentalness").ForGetter<ParameterPoint, Parameter>(p => p.Continentalness),
            ParameterCodec.FieldOf("erosion").ForGetter<ParameterPoint, Parameter>(p => p.Erosion),
            ParameterCodec.FieldOf("depth").ForGetter<ParameterPoint, Parameter>(p => p.Depth),
            ParameterCodec.FieldOf("weirdness").ForGetter<ParameterPoint, Parameter>(p => p.Weirdness),
            Codecs.Double.FieldOf("offset").ForGetter<ParameterPoint, double>(p => UnquantizeCoord(p.Offset)),
            (temperature, humidity, continentalness, erosion, depth, weirdness, offset)
                => new ParameterPoint(temperature, humidity, continentalness, erosion, depth, weirdness,
                    QuantizeCoord((float)offset)));
}

//ClimateParameterCodec 单维度参数 codec 接受单浮点或 [min,max] 列表
internal sealed class ClimateParameterCodec : ScalarCodec<Climate.Parameter>
{
    public override DataResult<Climate.Parameter> Parse<U>(DynamicOps<U> ops, U input)
    {
        var single = ops.GetNumberValue(input);
        if (single.Result().IsPresent)
            return DataResult<Climate.Parameter>.Success(
                Climate.Parameter.Single(Climate.QuantizeCoord((float)single.GetOrThrow())));
        var stream = ops.GetStream(input);
        if (!stream.Result().IsPresent)
            return DataResult<Climate.Parameter>.Error(() => "Climate.Parameter must be a number or a [min,max] list");
        var values = stream.GetOrThrow().ToList();
        if (values.Count != 2)
            return DataResult<Climate.Parameter>.Error(() => "Climate.Parameter list requires exactly 2 elements");
        var min = ops.GetNumberValue(values[0]);
        var max = ops.GetNumberValue(values[1]);
        if (!min.Result().IsPresent || !max.Result().IsPresent)
            return DataResult<Climate.Parameter>.Error(() => "Climate.Parameter bounds must be numbers");
        return DataResult<Climate.Parameter>.Success(new Climate.Parameter(
            Climate.QuantizeCoord((float)min.GetOrThrow()),
            Climate.QuantizeCoord((float)max.GetOrThrow())));
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Climate.Parameter value)
    {
        if (value.Min == value.Max)
            return DataResult<U>.Success(ops.CreateFloat(Climate.UnquantizeCoord(value.Min)));
        return DataResult<U>.Success(ops.CreateList(new[]
        {
            ops.CreateFloat(Climate.UnquantizeCoord(value.Min)),
            ops.CreateFloat(Climate.UnquantizeCoord(value.Max))
        }));
    }
}

//MultiNoiseBiomeSourceParameterList 多噪声生物群系参数列表对应原版 MultiNoiseBiomeSourceParameterList
//持有 ParameterPoint -> Holder<Biome> 映射MultiNoiseBiomeSource 用此列表查找最近 Biome
public sealed class MultiNoiseBiomeSourceParameterList
{
    public IReadOnlyList<(Climate.ParameterPoint Point, Holder<Biome> Biome)> Entries { get; }

    //Preset 生成此表的预设 null 表示手工构造
    public MultiNoisePreset? Preset { get; }

    //Index 最近邻搜索树 条目有几千条 线性遍历会让每次采样都扫全表
    private readonly ClimateRTree<Holder<Biome>>? _index;

    //Codec 参数表 JSON 编解码对应原版 DIRECT_CODEC
    public static readonly Codec<MultiNoiseBiomeSourceParameterList> Codec =
        MultiNoiseBiomeSourceParameterListCodec.Instance;

    public MultiNoiseBiomeSourceParameterList(
        IEnumerable<(Climate.ParameterPoint, Holder<Biome>)> entries)
        : this(null, entries)
    {
    }

    public MultiNoiseBiomeSourceParameterList(
        MultiNoisePreset? preset,
        IEnumerable<(Climate.ParameterPoint, Holder<Biome>)> entries)
    {
        Preset = preset;
        Entries = entries.ToList();
        _index = Entries.Count == 0 ? null : ClimateRTree<Holder<Biome>>.Create(Entries);
    }

    //FindClosest 查找参数空间距离最近的 Biome Holder 对应原版 parameterList.findValue
    public Holder<Biome> FindClosest(Climate.ParameterPoint target)
        => _index?.Search(target) ?? throw new InvalidOperationException("parameter list is empty");
}
