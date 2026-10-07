using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen;

//Climate multi-noise climate parameter system, maps to vanilla net.minecraft.world.level.levelgen.Climate
//Holds the six dimension parameters temperature/humidity/continentalness/erosion/depth/weirdness
//MultiNoiseBiomeSource uses ParameterPoint distance to a target point to find the nearest biome
public static class Climate
{
    //Parameter single-dimension parameter range, maps to vanilla Climate.Parameter
    //Holds min/max for the parameter range used in parameter-space distance computation
    public readonly struct Parameter
    {
        public long Min { get; }
        public long Max { get; }

        public Parameter(long min, long max)
        {
            Min = min;
            Max = max;
        }

        //Single single-value parameter, min == max
        public static Parameter Single(long value) => new(value, value);

        //Point single-value parameter, maps to vanilla Parameter.point; the float is quantised first
        public static Parameter Point(float value) => Span(value, value);

        //Span float range parameter, maps to vanilla Parameter.span(float,float)
        public static Parameter Span(float min, float max)
        {
            if (min > max)
                throw new ArgumentException($"min > max: {min} {max}");
            return new Parameter(QuantizeCoord(min), QuantizeCoord(max));
        }

        //Span parameter range merge, maps to vanilla Parameter.span(Parameter,Parameter)
        public static Parameter Span(Parameter min, Parameter max)
        {
            if (min.Min > max.Max)
                throw new ArgumentException($"min > max: {min} {max}");
            return new Parameter(min.Min, max.Max);
        }

        //Merge unions with another range, maps to vanilla Parameter.span(Parameter)
        //Only used by the RTree to merge child bounding boxes; the two ranges need not be ordered
        public Parameter Merge(Parameter other)
            => new(Math.Min(Min, other.Min), Math.Max(Max, other.Max));

        //Value centre value used in parameter-space distance computation, maps to vanilla parameter.spaceToBlock
        public long Value => (Min + Max) >> 1;

        //Range parameter span
        public long Range => Max - Min;

        public override string ToString() => Min == Max ? $"[{Min}]" : $"[{Min}..{Max}]";
    }

    //ParameterPoint multi-dimensional parameter point, maps to vanilla Climate.ParameterPoint
    //Holds six Climate.Parameters plus offset, used by MultiNoiseBiomeSource distance lookups
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

        //Fitness fitness of the parameter point against the target climate, maps to vanilla Climate.ParameterPoint.fitness
        //Sum of squared range distances over 6 dimensions; the offset term is squared against itself, matching vanilla; lower is a better fit
        public long Fitness(TargetPoint target)
            => Square(ParameterDistance(Temperature, target.Temperature))
             + Square(ParameterDistance(Humidity, target.Humidity))
             + Square(ParameterDistance(Continentalness, target.Continentalness))
             + Square(ParameterDistance(Erosion, target.Erosion))
             + Square(ParameterDistance(Depth, target.Depth))
             + Square(ParameterDistance(Weirdness, target.Weirdness))
             + Square(Offset);
    }

    //TargetPoint the 6-dimensional climate values sampled at a coordinate, maps to vanilla Climate.TargetPoint
    //The spawn point climate search uses it as the target and fixes depth at zero during the search
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

        //ZeroDepth zeroes the depth dimension, maps to zeroDepthTargetPoint in the vanilla spawn search
        public TargetPoint ZeroDepth()
            => new(Temperature, Humidity, Continentalness, Erosion, 0L, Weirdness);
    }

    //Sampler noise sampler interface, maps to vanilla Climate.Sampler
    //MultiNoiseBiomeSource uses it to sample the six dimension parameters by coordinate
    public interface Sampler
    {
        Parameter Temperature(int x, int y, int z);
        Parameter Humidity(int x, int y, int z);
        Parameter Continentalness(int x, int y, int z);
        Parameter Erosion(int x, int y, int z);
        Parameter Depth(int x, int y, int z);
        Parameter Weirdness(int x, int y, int z);
    }

    //ConstantSampler constant sampler returning fixed parameters for every dimension, used in tests
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

    //NoiseRouterSampler real sampler based on the six climate dimension density functions of a NoiseRouter
    //Maps to the NoiseRouterData implementation of vanilla Climate.Sampler
    //Quantises the density double to long and wraps it in Parameter.Single
    //The quantisation precision matches vanilla Climate.quantize, mapping a double into long space
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

        //Quantize samples the density and quantises it to long, maps to vanilla Climate.quantizeCoord
        //The factor must match QuantizeCoord; vanilla uses ×10000, and ×1000 would shrink every sample tenfold and skew all distances
        private static long Quantize(DensityFunction function, int x, int y, int z)
        {
            var ctx = ReusableContext.Set(x, y, z);
            return QuantizeCoord((float)function.Compute(ctx));
        }

        //ReusableContext per-point reusable sampling context
        //Vanilla allocates a SinglePointContext per sample and relies on JIT scalar replacement; .NET escape analysis does not handle heap objects
        //One climate sample needs six allocations across the six dimensions and a chunk needs nearly a hundred thousand, the biggest entry in the allocation table
        //The sampler is shared across generation threads, so each thread holds its own; the called density functions never retain the context
        [ThreadStatic] private static SinglePointContext? _reusableContext;

        private static SinglePointContext ReusableContext
            => _reusableContext ??= new SinglePointContext(0, 0, 0);
    }

    //Distance parameter-space distance between a sample point and a parameter list entry, maps to vanilla Climate.RTree.Node.distance
    //Takes the range distance per dimension then sums the squares; dimensions inside the entry's range contribute 0, which is the vanilla nearest-neighbour semantics
    //All 7 dimensions take part and offset is treated as a single-value range
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

    //ParameterDistance distance from a single-dimension range to a value, maps to vanilla Parameter.distance(long)
    //Above the max takes the max difference, below the min takes the min difference, and inside the range is 0
    private static long ParameterDistance(Parameter span, long target)
    {
        var above = target - span.Max;
        if (above > 0) return above;
        return Math.Max(span.Min - target, 0L);
    }

    private static long Square(long value) => value * value;

    //SpawnSearchMaxRadius max spawn search radius, maps to vanilla Climate$SpawnFinder.MAX_RADIUS
    private const long SpawnSearchMaxRadius = 2048;

    //FindSpawnPosition radial climate search for the spawn point, maps to vanilla Climate.findSpawnPosition
    //Computes the origin fitness, then loops from 512 to 2048 and from 32 to 512, replacing on any better fit along the ring
    //Fitness includes a squared-distance bias from the origin, so an equally fitting point nearer the origin wins
    public static BlockPos FindSpawnPosition(IReadOnlyList<ParameterPoint> targetClimates, Sampler sampler)
    {
        var best = SpawnCandidateAt(targetClimates, sampler, 0, 0);
        RadialSearch(targetClimates, sampler, 2048f, 512f, ref best);
        RadialSearch(targetClimates, sampler, 512f, 32f, ref best);
        return best.Location;
    }

    //RadialSearch samples a ring around the current best candidate, maps to vanilla Climate$SpawnFinder.radialSearch
    //The angle step is the ratio of the radius increment, keeping the number of samples per ring proportional to the radius
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

    //SpawnCandidateAt samples a climate column and takes the minimum fitness against the target points, maps to vanilla getSpawnPositionAndFitness
    //Depth is fixed at 0; fitness is multiplied by the square of the max radius and the squared distance from the origin is added, biasing the search to stay close
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

    //SampleTarget samples the 6-dimensional climate at a block coordinate, maps to vanilla Climate.Sampler.sample
    //Vanilla samples by quart; here the coordinate is folded to a quart-aligned block coordinate with y fixed at 0
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

    //SpawnCandidate spawn candidate recording location and fitness, maps to vanilla Climate$SpawnFinder$Result
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

    //QuantizeCoord maps a float parameter into long parameter space, maps to vanilla Climate.quantizeCoord
    public static long QuantizeCoord(float coord) => (long)(coord * 10000.0f);

    //UnquantizeCoord restores a long parameter to a float, maps to vanilla Climate.unquantizeCoord
    public static float UnquantizeCoord(long coord) => coord / 10000.0f;

    //Parameters six-dimension float single-value parameter point, maps to vanilla Climate.parameters(float ...)
    public static ParameterPoint Parameters(
        float temperature, float humidity, float continentalness,
        float erosion, float depth, float weirdness, float offset)
        => new(
            Parameter.Point(temperature), Parameter.Point(humidity), Parameter.Point(continentalness),
            Parameter.Point(erosion), Parameter.Point(depth), Parameter.Point(weirdness),
            QuantizeCoord(offset));

    //Parameters six-dimension range parameter point, maps to vanilla Climate.parameters(Parameter ...)
    public static ParameterPoint Parameters(
        Parameter temperature, Parameter humidity, Parameter continentalness,
        Parameter erosion, Parameter depth, Parameter weirdness, float offset)
        => new(temperature, humidity, continentalness, erosion, depth, weirdness, QuantizeCoord(offset));

    //ParameterCodec single-dimension parameter range codec, maps to vanilla Climate.Parameter.CODEC
    //A bare float writes as a single-value range and a two-element list writes as a range
    public static readonly Codec<Parameter> ParameterCodec = new ClimateParameterCodec();

    //ParameterPointCodec six-dimension parameter point codec, maps to vanilla Climate.ParameterPoint.CODEC
    public static readonly Codec<ParameterPoint> ParameterPointCodec = BuildParameterPointCodec();

    //BuildParameterPointCodec six parameters + the offset field
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

//ClimateParameterCodec single-dimension parameter codec, accepting a bare float or a [min,max] list
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

//MultiNoiseBiomeSourceParameterList multi-noise biome parameter list, maps to vanilla MultiNoiseBiomeSourceParameterList
//Holds the ParameterPoint -> Holder<Biome> mappings; MultiNoiseBiomeSource uses this list to find the nearest biome
public sealed class MultiNoiseBiomeSourceParameterList
{
    public IReadOnlyList<(Climate.ParameterPoint Point, Holder<Biome> Biome)> Entries { get; }

    //Preset the preset that generated this list; null means hand-built
    public MultiNoisePreset? Preset { get; }

    //Index nearest-neighbour search tree; with thousands of entries a linear scan would walk the whole list on every sample
    private readonly ClimateRTree<Holder<Biome>>? _index;

    //Codec parameter list JSON codec, maps to vanilla DIRECT_CODEC
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

    //FindClosest finds the biome Holder nearest in parameter space, maps to vanilla parameterList.findValue
    public Holder<Biome> FindClosest(Climate.ParameterPoint target)
        => _index?.Search(target) ?? throw new InvalidOperationException("parameter list is empty");
}
