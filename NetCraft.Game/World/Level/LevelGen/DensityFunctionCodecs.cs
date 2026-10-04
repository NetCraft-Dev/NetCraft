using NetCraft.Codec;
using NetCraft.Game.Util;
using NetCraft.Game.World.Level.LevelGen.Synth;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen;

//DensityFunctionCodecs 密度函数 codec 集合对应原版 DensityFunctions.CODEC 体系
//每个 type 一个 MapCodec<DensityFunction> 只负责 type 之外的字段
//AllCodecs 是 type 名到 codec 的单一事实来源bootstrap 按它注册 dispatch 查注册表
public static class DensityFunctionCodecs
{
    //ConstantCodec 常量密度函数 codec 对应原版 Constant.CODEC
    public static readonly MapCodec<DensityFunction> ConstantCodec =
        Codecs.Double.ComapFlatMap(
            v => DataResult<DensityFunction>.Success(new Constant(v)),
            df => ((Constant)df).Value).FieldOf("argument");

    //ClampCodec 钳制密度函数 codec 对应原版 Clamp.CODEC
    public static readonly MapCodec<DensityFunction> ClampCodec =
        RecordCodecBuilder.Of3(
            DensityFunctionCodecHelper.DensityFunctionField("input").ForGetter<DensityFunction, DensityFunction>(df => ((Clamp)df).Input),
            Codecs.Double.FieldOf("min").ForGetter<DensityFunction, double>(df => ((Clamp)df).Min),
            Codecs.Double.FieldOf("max").ForGetter<DensityFunction, double>(df => ((Clamp)df).Max),
            (input, min, max) => (DensityFunction)new Clamp(input, min, max));

    //YClampedGradientCodec Y 轴梯度 codec 对应原版 YClampedGradient.CODEC
    public static readonly MapCodec<DensityFunction> YClampedGradientCodec =
        RecordCodecBuilder.Of4(
            Codecs.Int.FieldOf("from_y").ForGetter<DensityFunction, int>(df => ((YClampedGradient)df).FromY),
            Codecs.Int.FieldOf("to_y").ForGetter<DensityFunction, int>(df => ((YClampedGradient)df).ToY),
            Codecs.Double.FieldOf("from_value").ForGetter<DensityFunction, double>(df => ((YClampedGradient)df).FromValue),
            Codecs.Double.FieldOf("to_value").ForGetter<DensityFunction, double>(df => ((YClampedGradient)df).ToValue),
            (fromY, toY, fromValue, toValue) => (DensityFunction)new YClampedGradient(fromY, toY, fromValue, toValue));

    //NoiseCodec 噪声密度函数 codec 对应原版 Noise.DATA_CODEC
    public static readonly MapCodec<DensityFunction> NoiseCodec =
        RecordCodecBuilder.Of3(
            NoiseHolderCodec.Instance.FieldOf("noise").ForGetter<DensityFunction, NoiseHolder>(df => ((Noise)df).NoiseData),
            Codecs.Double.FieldOf("xz_scale").ForGetter<DensityFunction, double>(df => ((Noise)df).XzScale),
            Codecs.Double.FieldOf("y_scale").ForGetter<DensityFunction, double>(df => ((Noise)df).YScale),
            (noise, xzScale, yScale) => (DensityFunction)new Noise(noise, xzScale, yScale));

    //ShiftedNoiseCodec 偏移噪声 codec 对应原版 ShiftedNoise.DATA_CODEC
    public static readonly MapCodec<DensityFunction> ShiftedNoiseCodec =
        RecordCodecBuilder.Of6(
            DensityFunctionCodecHelper.DensityFunctionField("shift_x").ForGetter<DensityFunction, DensityFunction>(df => ((ShiftedNoise)df).ShiftX),
            DensityFunctionCodecHelper.DensityFunctionField("shift_y").ForGetter<DensityFunction, DensityFunction>(df => ((ShiftedNoise)df).ShiftY),
            DensityFunctionCodecHelper.DensityFunctionField("shift_z").ForGetter<DensityFunction, DensityFunction>(df => ((ShiftedNoise)df).ShiftZ),
            Codecs.Double.FieldOf("xz_scale").ForGetter<DensityFunction, double>(df => ((ShiftedNoise)df).XzScale),
            Codecs.Double.FieldOf("y_scale").ForGetter<DensityFunction, double>(df => ((ShiftedNoise)df).YScale),
            NoiseHolderCodec.Instance.FieldOf("noise").ForGetter<DensityFunction, NoiseHolder>(df => ((ShiftedNoise)df).NoiseData),
            (shiftX, shiftY, shiftZ, xzScale, yScale, noise)
                => (DensityFunction)new ShiftedNoise(noise, xzScale, yScale, shiftX, shiftY, shiftZ));

    //RangeChoiceCodec 范围选择 codec 对应原版 RangeChoice.DATA_CODEC
    public static readonly MapCodec<DensityFunction> RangeChoiceCodec =
        RecordCodecBuilder.Of5(
            DensityFunctionCodecHelper.DensityFunctionField("input").ForGetter<DensityFunction, DensityFunction>(df => ((RangeChoice)df).Input),
            Codecs.Double.FieldOf("min_inclusive").ForGetter<DensityFunction, double>(df => ((RangeChoice)df).MinInclusive),
            Codecs.Double.FieldOf("max_exclusive").ForGetter<DensityFunction, double>(df => ((RangeChoice)df).MaxExclusive),
            DensityFunctionCodecHelper.DensityFunctionField("when_in_range").ForGetter<DensityFunction, DensityFunction>(df => ((RangeChoice)df).WhenInRange),
            DensityFunctionCodecHelper.DensityFunctionField("when_out_of_range").ForGetter<DensityFunction, DensityFunction>(df => ((RangeChoice)df).WhenOutOfRange),
            (input, min, max, whenInRange, whenOutOfRange)
                => (DensityFunction)new RangeChoice(input, min, max, whenInRange, whenOutOfRange));

    //IntervalSelectCodec 多段选择 codec 对应原版 IntervalSelect.DATA_CODEC
    public static readonly MapCodec<DensityFunction> IntervalSelectCodec = IntervalSelectDensityFunctionCodec.Instance;

    //FindTopSurfaceCodec 查找顶部表面 codec 对应原版 FindTopSurface.DATA_CODEC
    public static readonly MapCodec<DensityFunction> FindTopSurfaceCodec =
        RecordCodecBuilder.Of4(
            DensityFunctionCodecHelper.DensityFunctionField("density").ForGetter<DensityFunction, DensityFunction>(df => ((FindTopSurface)df).Density),
            DensityFunctionCodecHelper.DensityFunctionField("upper_bound").ForGetter<DensityFunction, DensityFunction>(df => ((FindTopSurface)df).UpperBound),
            Codecs.Int.FieldOf("lower_bound").ForGetter<DensityFunction, int>(df => ((FindTopSurface)df).LowerBound),
            Codecs.Int.FieldOf("cell_height").ForGetter<DensityFunction, int>(df => ((FindTopSurface)df).CellHeight),
            (density, upperBound, lowerBound, cellHeight)
                => (DensityFunction)new FindTopSurface(density, upperBound, lowerBound, cellHeight));

    //OldBlendedNoiseCodec 旧版混合噪声 codec 对应原版 BlendedNoise.DATA_CODEC
    public static readonly MapCodec<DensityFunction> OldBlendedNoiseCodec =
        RecordCodecBuilder.Of5(
            Codecs.Double.FieldOf("xz_scale").ForGetter<DensityFunction, double>(df => ((BlendedNoise)df).XzScale),
            Codecs.Double.FieldOf("y_scale").ForGetter<DensityFunction, double>(df => ((BlendedNoise)df).YScale),
            Codecs.Double.FieldOf("xz_factor").ForGetter<DensityFunction, double>(df => ((BlendedNoise)df).XzFactor),
            Codecs.Double.FieldOf("y_factor").ForGetter<DensityFunction, double>(df => ((BlendedNoise)df).YFactor),
            Codecs.Double.FieldOf("smear_scale_multiplier").ForGetter<DensityFunction, double>(df => ((BlendedNoise)df).SmearScaleMultiplier),
            (xzScale, yScale, xzFactor, yFactor, smearScaleMultiplier)
                => (DensityFunction)BlendedNoise.CreateUnseeded(xzScale, yScale, xzFactor, yFactor, smearScaleMultiplier));

    //MarkerNames 缓存标记 type 名对应原版 Marker.Type.getSerializedName
    private static readonly (DensityFunctionsExtra.MarkerType Type, string Name)[] MarkerNames =
    {
        (DensityFunctionsExtra.MarkerType.Interpolated, "interpolated"),
        (DensityFunctionsExtra.MarkerType.FlatCache, "flat_cache"),
        (DensityFunctionsExtra.MarkerType.Cache2D, "cache_2d"),
        (DensityFunctionsExtra.MarkerType.CacheOnce, "cache_once"),
        (DensityFunctionsExtra.MarkerType.CacheAllInCell, "cache_all_in_cell"),
        (DensityFunctionsExtra.MarkerType.BlendDensity, "blend_density")
    };

    //MappedNames 一元变换 type 名对应原版 Mapped.Type.getSerializedName
    private static readonly (MappedTypes.MappedType Type, string Name)[] MappedNames =
    {
        (MappedTypes.MappedType.Abs, "abs"),
        (MappedTypes.MappedType.Square, "square"),
        (MappedTypes.MappedType.Cube, "cube"),
        (MappedTypes.MappedType.HalfNegative, "half_negative"),
        (MappedTypes.MappedType.QuarterNegative, "quarter_negative"),
        (MappedTypes.MappedType.Invert, "invert"),
        (MappedTypes.MappedType.Squeeze, "squeeze")
    };

    //AllCodecs 全部 type 名到 codec 的映射 供 bootstrap 注册 新增 type 只改这里
    public static readonly IReadOnlyDictionary<string, MapCodec<DensityFunction>> AllCodecs = BuildAllCodecs();

    //MarkerName 取缓存标记 type 名
    public static string MarkerName(DensityFunctionsExtra.MarkerType type)
    {
        foreach (var (candidate, name) in MarkerNames)
            if (candidate == type) return name;
        throw new NotSupportedException($"Unsupported MarkerType: {type}");
    }

    //MappedName 取一元变换 type 名
    public static string MappedName(MappedTypes.MappedType type)
    {
        foreach (var (candidate, name) in MappedNames)
            if (candidate == type) return name;
        throw new NotSupportedException($"Unsupported MappedType: {type}");
    }

    //Ap2TypeName 取二元运算 type 名对应原版 TwoArgumentSimpleFunction.Type.getSerializedName
    public static string Ap2TypeName(Ap2.OpType type) => type switch
    {
        Ap2.OpType.Max => "max",
        Ap2.OpType.Min => "min",
        Ap2.OpType.Add => "add",
        Ap2.OpType.Mul => "mul",
        _ => throw new NotSupportedException($"Unsupported Ap2 type: {type}")
    };

    //BuildAllCodecs 构造全部 type 名到 codec 的映射 顺序与原版 bootstrap 一致
    private static IReadOnlyDictionary<string, MapCodec<DensityFunction>> BuildAllCodecs()
    {
        var map = new Dictionary<string, MapCodec<DensityFunction>>
        {
            ["blend_alpha"] = UnitCodec(() => BlendAlpha.Instance),
            ["blend_offset"] = UnitCodec(() => BlendOffset.Instance),
            ["beardifier"] = UnitCodec(() => BeardifierMarker.Instance),
            ["old_blended_noise"] = OldBlendedNoiseCodec,
            ["noise"] = NoiseCodec,
            ["end_islands"] = UnitCodec(() => new EndIslandDensityFunction(0L)),
            ["shifted_noise"] = ShiftedNoiseCodec,
            ["range_choice"] = RangeChoiceCodec,
            ["interval_select"] = IntervalSelectCodec,
            ["shift_a"] = ShiftCodec(holder => new ShiftA(holder), df => ((ShiftA)df).OffsetNoise),
            ["shift_b"] = ShiftCodec(holder => new ShiftB(holder), df => ((ShiftB)df).OffsetNoise),
            ["shift"] = ShiftCodec(holder => new Shift(holder), df => ((Shift)df).OffsetNoise),
            ["clamp"] = ClampCodec,
            ["spline"] = SplineCodec(),
            ["constant"] = ConstantCodec,
            ["y_clamped_gradient"] = YClampedGradientCodec,
            ["find_top_surface"] = FindTopSurfaceCodec,
            ["add"] = TwoArgumentCodec(Ap2.OpType.Add),
            ["mul"] = TwoArgumentCodec(Ap2.OpType.Mul),
            ["min"] = TwoArgumentCodec(Ap2.OpType.Min),
            ["max"] = TwoArgumentCodec(Ap2.OpType.Max)
        };
        foreach (var (type, name) in MarkerNames)
            map[name] = MarkerCodec(type);
        foreach (var (type, name) in MappedNames)
            map[name] = MappedCodec(type);
        return map;
    }

    //TwoArgumentCodec 二元运算 codec 对应原版 doubleFunctionArgumentCodec
    //add/mul/min/max 统一 argument1 + argument2 布局
    private static MapCodec<DensityFunction> TwoArgumentCodec(Ap2.OpType type)
        => RecordCodecBuilder.Of2(
            DensityFunctionCodecHelper.DensityFunctionField("argument1").ForGetter<DensityFunction, DensityFunction>(df => ((Ap2)df).Input1),
            DensityFunctionCodecHelper.DensityFunctionField("argument2").ForGetter<DensityFunction, DensityFunction>(df => ((Ap2)df).Input2),
            (input1, input2) => (DensityFunction)new Ap2(type, input1, input2));

    //MarkerCodec 缓存标记 codec 对应原版 Marker.Type.codec
    private static MapCodec<DensityFunction> MarkerCodec(DensityFunctionsExtra.MarkerType type)
        => new SingleArgumentCodec<DensityFunction>(
            DensityFunctionCodec.Instance,
            input => new MarkerNode(type, input),
            df => ((MarkerNode)df).Wrapped);

    //MappedCodec 一元变换 codec 对应原版 Mapped.Type.codec
    private static MapCodec<DensityFunction> MappedCodec(MappedTypes.MappedType type)
        => new SingleArgumentCodec<DensityFunction>(
            DensityFunctionCodec.Instance,
            input => MappedTypes.Create(type, input),
            df => ((Mapped)df).Input);

    //ShiftCodec 偏移噪声 codec 对应原版 ShiftA/ShiftB/Shift.CODEC
    //argument 是噪声参数引用而非密度函数
    private static MapCodec<DensityFunction> ShiftCodec(Func<NoiseHolder, DensityFunction> ctor, Func<DensityFunction, NoiseHolder> getter)
        => new SingleArgumentCodec<NoiseHolder>(NoiseHolderCodec.Instance, ctor, getter);

    //SplineCodec 样条 codec 对应原版 Spline.DATA_CODEC
    //原版字段名是 spline 不是通用的 argument
    private static MapCodec<DensityFunction> SplineCodec()
        => new SingleArgumentCodec<CubicSpline>(
            CubicSplineCodec.Instance,
            spline => new SplineFunction(spline),
            df => ((SplineFunction)df).Spline,
            "spline");

    //UnitCodec 无字段 codec 对应原版 MapCodec.unit
    private static MapCodec<DensityFunction> UnitCodec(Func<DensityFunction> factory)
        => new UnitDensityFunctionCodec(factory);
}

//DensityFunctionCodecHelper 密度函数 codec 辅助工具
//提供 DensityFunction 字段定义与 dispatch codec 入口
public static class DensityFunctionCodecHelper
{
    //DensityFunctionField 创建 DensityFunction 字段对应原版 DensityFunction.CODEC.fieldOf(name)
    //dispatch 通过 DensityFunctionCodec 单例解码子类型也支持注册表引用字符串
    public static MapCodec<DensityFunction> DensityFunctionField(string name)
        => DensityFunctionCodec.Instance.FieldOf(name);
}

//DensityFunctionCodec 密度函数 dispatch codec 对应原版 DensityFunctions.CODEC
//按 "type" 字段查 DENSITY_FUNCTION_TYPE 注册表取子类 MapCodec
//输入是字符串时按 DENSITY_FUNCTION 注册表解析引用
public sealed class DensityFunctionCodec : AbstractMapCodec<DensityFunction>
{
    public static readonly DensityFunctionCodec Instance = new();

    //Parse 字符串走注册表引用其余回落基类的 map 解码
    public override DataResult<DensityFunction> Parse<U>(DynamicOps<U> ops, U input)
    {
        var stringResult = ops.GetStringValue(input);
        if (stringResult.Result().IsPresent)
        {
            var text = stringResult.GetOrThrow();
            var id = Identifier.TryParse(text);
            if (id is null)
                return DataResult<DensityFunction>.Error(() => $"Invalid density function identifier: {text}");
            return DensityFunctionRefs.ResolveDensityFunction(ops, id.Value);
        }
        //裸数字等价于 constant 原版 DIRECT_CODEC 允许 either(double, CODEC) 真实 JSON 里 argument1 常直接写数字
        var numberResult = ops.GetNumberValue(input);
        if (numberResult.Result().IsPresent)
            return DataResult<DensityFunction>.Success(new Constant(numberResult.GetOrThrow()));
        return base.Parse(ops, input);
    }

    public override DataResult<DensityFunction> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var typeOpt = input.Get("type");
        if (!typeOpt.IsPresent)
            return DataResult<DensityFunction>.Error(() => "missing type field for DensityFunction");
        var typeResult = ops.GetStringValue(typeOpt.Get()).Result();
        if (!typeResult.IsPresent)
            return DataResult<DensityFunction>.Error(() => "type field is not a string");
        var text = typeResult.Get();
        var id = Identifier.TryParse(text);
        if (id is null)
            return DataResult<DensityFunction>.Error(() => $"invalid DensityFunction type: {text}");
        var codec = BuiltInRegistries.DENSITY_FUNCTION_TYPE.GetValue(id.Value);
        if (codec is null)
            return DataResult<DensityFunction>.Error(() => $"unknown DensityFunction type: {text}");
        return codec.Decode(ops, input).Map(o => (DensityFunction)o);
    }

    //LookupId 运行时类型到 type 字符串对应原版 dispatch 写入 type 字段
    //MulOrAdd 无独立 codec 按语义等价成 add/mul 的 Ap2 编码
    private static string? LookupId(DensityFunction df) => df switch
    {
        Constant => "constant",
        Clamp => "clamp",
        MulOrAdd m => m.Type == MulOrAdd.OpType.Add ? "add" : "mul",
        Ap2 a => DensityFunctionCodecs.Ap2TypeName(a.Type),
        YClampedGradient => "y_clamped_gradient",
        Noise => "noise",
        ShiftedNoise => "shifted_noise",
        ShiftA => "shift_a",
        ShiftB => "shift_b",
        Shift => "shift",
        SplineFunction => "spline",
        RangeChoice => "range_choice",
        IntervalSelect => "interval_select",
        MarkerNode m => DensityFunctionCodecs.MarkerName(m.Type),
        EndIslandDensityFunction => "end_islands",
        BlendAlpha => "blend_alpha",
        BlendOffset => "blend_offset",
        BeardifierMarker => "beardifier",
        BlendedNoise => "old_blended_noise",
        MappedTypes.Abs => "abs",
        MappedTypes.Square => "square",
        MappedTypes.Cube => "cube",
        MappedTypes.HalfNegative => "half_negative",
        MappedTypes.QuarterNegative => "quarter_negative",
        MappedTypes.Invert => "invert",
        MappedTypes.Squeeze => "squeeze",
        FindTopSurface => "find_top_surface",
        _ => null
    };

    //EncodeTo 先写 type 字段再把剩余字段委托给注册表里的子类 codec
    //MulOrAdd 编码为 argument1=constant(value) + argument2=input 解码回来是语义等价的 Ap2
    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, DensityFunction value, RecordBuilder<U> builder)
    {
        var id = LookupId(value);
        if (id is null)
        {
            builder.Add("type", DataResult<U>.Error(() => $"unsupported DensityFunction type: {value?.GetType().Name}").GetOrThrow());
            return builder;
        }
        var location = Identifier.Parse(id);
        var codec = BuiltInRegistries.DENSITY_FUNCTION_TYPE.GetValue(location);
        builder.Add("type", ops.CreateString(location.ToString()));
        if (codec is null)
            return builder;
        return value is MulOrAdd mulOrAdd
            ? EncodeMulOrAdd(ops, mulOrAdd, builder)
            : codec.EncodeTo(ops, value, builder);
    }

    //EncodeMulOrAdd 把 MulOrAdd 写成 add/mul 的 argument1/argument2 布局
    private static RecordBuilder<U> EncodeMulOrAdd<U>(DynamicOps<U> ops, MulOrAdd value, RecordBuilder<U> builder)
    {
        builder.Add("argument1", Instance.EncodeStart(ops, new Constant(value.Value)).GetOrThrow());
        builder.Add("argument2", Instance.EncodeStart(ops, value.Input).GetOrThrow());
        return builder;
    }
}

//SingleArgumentCodec 单 argument 字段 codec 对应原版 singleFunctionArgumentCodec
//argument 的 codec 决定它接受内联对象还是注册表引用字符串
internal sealed class SingleArgumentCodec<A> : AbstractMapCodec<DensityFunction>
{
    private readonly Codec<A> _argumentCodec;
    private readonly Func<A, DensityFunction> _ctor;
    private readonly Func<DensityFunction, A> _getter;
    //_fieldName 字段名 原版绝大多数单参数类型用 argument 只有 spline 用的是 spline
    private readonly string _fieldName;

    public SingleArgumentCodec(Codec<A> argumentCodec, Func<A, DensityFunction> ctor, Func<DensityFunction, A> getter,
        string fieldName = "argument")
    {
        _argumentCodec = argumentCodec;
        _ctor = ctor;
        _getter = getter;
        _fieldName = fieldName;
    }

    public override DataResult<DensityFunction> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var value = input.Get(_fieldName);
        if (!value.IsPresent)
            return DataResult<DensityFunction>.Error(() => $"Missing key {_fieldName}");
        return _argumentCodec.Parse(ops, value.Get()).Map(_ctor);
    }

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, DensityFunction value, RecordBuilder<U> builder)
    {
        builder.Add(_fieldName, _argumentCodec.EncodeStart(ops, _getter(value)).GetOrThrow());
        return builder;
    }
}

//UnitDensityFunctionCodec 无字段 codec 对应原版 MapCodec.unit
//解码总是构造固定实例编码不写任何字段
internal sealed class UnitDensityFunctionCodec : AbstractMapCodec<DensityFunction>
{
    private readonly Func<DensityFunction> _factory;

    public UnitDensityFunctionCodec(Func<DensityFunction> factory) { _factory = factory; }

    public override DataResult<DensityFunction> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => DataResult<DensityFunction>.Success(_factory());

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, DensityFunction value, RecordBuilder<U> builder)
        => builder;
}

//IntervalSelectDensityFunctionCodec 多段选择 codec 对应原版 IntervalSelect.DATA_CODEC
//手写解码便于阈值数量与分支数校验失败时返回 Error 而不是抛异常
internal sealed class IntervalSelectDensityFunctionCodec : AbstractMapCodec<DensityFunction>
{
    public static readonly IntervalSelectDensityFunctionCodec Instance = new();

    public override DataResult<DensityFunction> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var inputTag = input.Get("input");
        if (!inputTag.IsPresent)
            return DataResult<DensityFunction>.Error(() => "Missing key input");
        var inputResult = DensityFunctionCodec.Instance.Parse(ops, inputTag.Get());
        if (!inputResult.Result().IsPresent)
            return DataResult<DensityFunction>.Error(() => "Failed to decode interval_select input");

        var thresholdsTag = input.Get("thresholds");
        if (!thresholdsTag.IsPresent)
            return DataResult<DensityFunction>.Error(() => "Missing key thresholds");
        var thresholdsStream = ops.GetStream(thresholdsTag.Get());
        if (!thresholdsStream.Result().IsPresent)
            return DataResult<DensityFunction>.Error(() => "thresholds must be a list");
        var thresholds = new List<double>();
        foreach (var tag in thresholdsStream.GetOrThrow())
        {
            var numberResult = ops.GetNumberValue(tag);
            if (!numberResult.Result().IsPresent)
                return DataResult<DensityFunction>.Error(() => "threshold must be a number");
            thresholds.Add(numberResult.GetOrThrow());
        }

        var functionsTag = input.Get("functions");
        if (!functionsTag.IsPresent)
            return DataResult<DensityFunction>.Error(() => "Missing key functions");
        var functionsStream = ops.GetStream(functionsTag.Get());
        if (!functionsStream.Result().IsPresent)
            return DataResult<DensityFunction>.Error(() => "functions must be a list");
        var functions = new List<DensityFunction>();
        foreach (var tag in functionsStream.GetOrThrow())
        {
            var functionResult = DensityFunctionCodec.Instance.Parse(ops, tag);
            if (!functionResult.Result().IsPresent)
                return DataResult<DensityFunction>.Error(() => "Failed to decode interval_select function");
            functions.Add(functionResult.GetOrThrow());
        }

        if (functions.Count < 2)
            return DataResult<DensityFunction>.Error(() => "Interval select requires at least 2 functions");
        if (thresholds.Count != functions.Count - 1)
            return DataResult<DensityFunction>.Error(() =>
                $"Expected {functions.Count - 1} thresholds for {functions.Count} functions, but got {thresholds.Count}");
        for (var i = 1; i < thresholds.Count; i++)
            if (thresholds[i] < thresholds[i - 1])
                return DataResult<DensityFunction>.Error(() => "Threshold values must be ordered from smallest to largest");

        return DataResult<DensityFunction>.Success(
            new IntervalSelect(inputResult.GetOrThrow(), thresholds.ToArray(), functions.ToArray()));
    }

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, DensityFunction value, RecordBuilder<U> builder)
    {
        var select = (IntervalSelect)value;
        builder.Add("input", DensityFunctionCodec.Instance.EncodeStart(ops, select.Input).GetOrThrow());
        builder.Add("thresholds", ops.CreateList(select.Thresholds.Select(v => ops.CreateDouble(v))));
        builder.Add("functions", ops.CreateList(
            select.Functions.Select(f => DensityFunctionCodec.Instance.EncodeStart(ops, f).GetOrThrow())));
        return builder;
    }
}
