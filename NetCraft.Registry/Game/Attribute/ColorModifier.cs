using NetCraft.Codec;

namespace NetCraft.Registry.Environment;

//ColorModifier 颜色修饰符对应原版 ColorModifier
public static class ColorModifier
{
    public static readonly AttributeModifier<int, int> AlphaBlend = new AlphaBlendColorModifier();

    public static readonly AttributeModifier<int, int> Add = new RgbColorModifier(Argb.AddRgb);

    public static readonly AttributeModifier<int, int> Subtract = new RgbColorModifier(Argb.SubtractRgb);

    public static readonly AttributeModifier<int, int> MultiplyRgb = new RgbColorModifier(Argb.Multiply);

    public static readonly AttributeModifier<int, int> MultiplyArgb = new ArgbColorModifier(Argb.Multiply);

    public static readonly AttributeModifier<int, BlendToGray> BlendToGray = new BlendToGrayColorModifier();

    //AlphaBlendColorModifier 按参数的 alpha 叠加颜色
    private sealed class AlphaBlendColorModifier : AttributeModifier<int, int>
    {
        public int Apply(int subject, int argument) => Argb.AlphaBlend(subject, argument);

        public Codec<int> ArgumentCodec(EnvironmentAttribute<int> attribute) => HexColorCodec.StringArgb;
    }

    //RgbColorModifier 只动 rgb 通道的修饰符
    private sealed class RgbColorModifier : AttributeModifier<int, int>
    {
        private readonly Func<int, int, int> _function;

        public RgbColorModifier(Func<int, int, int> function) { _function = function; }

        public int Apply(int subject, int argument) => _function(subject, argument);

        public Codec<int> ArgumentCodec(EnvironmentAttribute<int> attribute) => HexColorCodec.StringRgb;
    }

    //ArgbColorModifier 连 alpha 一起处理的修饰符
    private sealed class ArgbColorModifier : AttributeModifier<int, int>
    {
        private readonly Func<int, int, int> _function;

        public ArgbColorModifier(Func<int, int, int> function) { _function = function; }

        public int Apply(int subject, int argument) => _function(subject, argument);

        public Codec<int> ArgumentCodec(EnvironmentAttribute<int> attribute) => ArgbArgumentCodec.Instance;
    }

    //BlendToGrayColorModifier 按亮度把颜色往灰度方向混
    private sealed class BlendToGrayColorModifier : AttributeModifier<int, BlendToGray>
    {
        public int Apply(int subject, BlendToGray argument)
            => Argb.SrgbLerp(argument.Factor, subject, Argb.ScaleRgb(Argb.Greyscale(subject), argument.Brightness));

        public Codec<BlendToGray> ArgumentCodec(EnvironmentAttribute<int> attribute) => NetCraft.Registry.Environment.BlendToGray.Codec;
    }
}

//ArgbArgumentCodec alpha 满时不透明颜色走整数形态其余走 argb 串对应原版 ArgbModifier
internal sealed class ArgbArgumentCodec : ScalarCodec<int>
{
    public static readonly ArgbArgumentCodec Instance = new();

    public override DataResult<int> Parse<U>(DynamicOps<U> ops, U input)
    {
        var argb = HexColorCodec.StringArgb.Parse(ops, input);
        return argb.Result().IsPresent ? argb : Codecs.Int.Parse(ops, input);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, int value)
        => Argb.Alpha(value) == 255
            ? Codecs.Int.EncodeStart(ops, value)
            : HexColorCodec.StringArgb.EncodeStart(ops, value);
}

//BlendToGray 往灰度混合的参数对应原版 ColorModifier.BlendToGray
public sealed class BlendToGray
{
    private static readonly Codec<float> UnitFloatCodec = AttributeValueCodecs.UnitFloat;

    public static readonly Codec<BlendToGray> Codec = RecordCodecBuilder.Of2(
        UnitFloatCodec.FieldOf("brightness").ForGetter((BlendToGray v) => v.Brightness),
        UnitFloatCodec.FieldOf("factor").ForGetter((BlendToGray v) => v.Factor),
        (brightness, factor) => new BlendToGray(brightness, factor));

    public float Brightness { get; }

    public float Factor { get; }

    public BlendToGray(float brightness, float factor)
    {
        Brightness = brightness;
        Factor = factor;
    }
}
