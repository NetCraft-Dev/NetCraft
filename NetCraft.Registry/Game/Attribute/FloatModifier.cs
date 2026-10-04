using NetCraft.Codec;
using NetCraft.Util;

namespace NetCraft.Registry.Environment;

//FloatModifier 浮点修饰符对应原版 FloatModifier
public static class FloatModifier
{
    public static readonly AttributeModifier<float, FloatWithAlpha> AlphaBlend = new AlphaBlendModifier();

    public static readonly AttributeModifier<float, float> Add = new SimpleFloatModifier((subject, argument) => subject + argument);

    public static readonly AttributeModifier<float, float> Subtract = new SimpleFloatModifier((subject, argument) => subject - argument);

    public static readonly AttributeModifier<float, float> Multiply = new SimpleFloatModifier((subject, argument) => subject * argument);

    public static readonly AttributeModifier<float, float> Minimum = new SimpleFloatModifier(MathF.Min);

    public static readonly AttributeModifier<float, float> Maximum = new SimpleFloatModifier(MathF.Max);

    //SimpleFloatModifier 参数即浮点数的修饰符
    private sealed class SimpleFloatModifier : AttributeModifier<float, float>
    {
        private readonly Func<float, float, float> _function;

        public SimpleFloatModifier(Func<float, float, float> function) { _function = function; }

        public float Apply(float subject, float argument) => _function(subject, argument);

        public Codec<float> ArgumentCodec(EnvironmentAttribute<float> attribute) => Codecs.Float;
    }

    //AlphaBlendModifier 按 alpha 在主体与参数间插值
    private sealed class AlphaBlendModifier : AttributeModifier<float, FloatWithAlpha>
    {
        public float Apply(float subject, FloatWithAlpha argument) => Mth.Lerp(argument.Alpha, subject, argument.Value);

        public Codec<FloatWithAlpha> ArgumentCodec(EnvironmentAttribute<float> attribute) => FloatWithAlpha.Codec;
    }
}

//FloatWithAlpha 带 alpha 的浮点参数对应原版 FloatWithAlpha
public sealed class FloatWithAlpha
{
    public static readonly Codec<FloatWithAlpha> Codec = new FloatWithAlphaCodec();

    private static readonly Codec<FloatWithAlpha> FullCodec = RecordCodecBuilder.Of2(
        Codecs.Float.FieldOf("value").ForGetter((FloatWithAlpha v) => v.Value),
        AttributeValueCodecs.UnitFloat.OptionalFieldOf("alpha", 1.0f).ForGetter((FloatWithAlpha v) => v.Alpha),
        (value, alpha) => new FloatWithAlpha(value, alpha));

    public float Value { get; }

    public float Alpha { get; }

    public FloatWithAlpha(float value) : this(value, 1.0f) { }

    public FloatWithAlpha(float value, float alpha)
    {
        Value = value;
        Alpha = alpha;
    }

    //FloatWithAlphaCodec 单浮点即 alpha=1 的简写形态
    private sealed class FloatWithAlphaCodec : ScalarCodec<FloatWithAlpha>
    {
        public override DataResult<FloatWithAlpha> Parse<U>(DynamicOps<U> ops, U input)
        {
            var simple = Codecs.Float.Parse(ops, input);
            if (simple.Result().IsPresent)
                return DataResult<FloatWithAlpha>.Success(new FloatWithAlpha(simple.GetOrThrow()));
            return FullCodec.Parse(ops, input);
        }

        public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, FloatWithAlpha value)
            => value.Alpha == 1.0f ? Codecs.Float.EncodeStart(ops, value.Value) : FullCodec.EncodeStart(ops, value);
    }
}
