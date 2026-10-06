using NetCraft.Util;

namespace NetCraft.Registry.Environment;

//Argb color channel bit operations, porting the parts of vanilla ARGB used by environment attributes
internal static class Argb
{
    public static int Alpha(int color) => (int)((uint)color >> 24);

    public static int Red(int color) => (color >> 16) & 255;

    public static int Green(int color) => (color >> 8) & 255;

    public static int Blue(int color) => color & 255;

    public static int Color(int alpha, int red, int green, int blue)
        => ((alpha & 255) << 24) | ((red & 255) << 16) | ((green & 255) << 8) | (blue & 255);

    //Opaque fills the alpha channel to full
    public static int Opaque(int color) => color | unchecked((int)0xFF000000);

    //Transparent keeps only the rgb channels
    public static int Transparent(int color) => color & 16777215;

    public static int Multiply(int lhs, int rhs)
    {
        if (lhs == -1) return rhs;
        if (rhs == -1) return lhs;
        return Color(
            (Alpha(lhs) * Alpha(rhs)) / 255,
            (Red(lhs) * Red(rhs)) / 255,
            (Green(lhs) * Green(rhs)) / 255,
            (Blue(lhs) * Blue(rhs)) / 255);
    }

    //AddRgb adds channel by channel with saturation
    public static int AddRgb(int lhs, int rhs)
        => Color(Alpha(lhs), Math.Min(Red(lhs) + Red(rhs), 255), Math.Min(Green(lhs) + Green(rhs), 255), Math.Min(Blue(lhs) + Blue(rhs), 255));

    //SubtractRgb subtracts channel by channel, clamped to 0
    public static int SubtractRgb(int lhs, int rhs)
        => Color(Alpha(lhs), Math.Max(Red(lhs) - Red(rhs), 0), Math.Max(Green(lhs) - Green(rhs), 0), Math.Max(Blue(lhs) - Blue(rhs), 0));

    public static int ScaleRgb(int color, float scale) => ScaleRgb(color, scale, scale, scale);

    public static int ScaleRgb(int color, float scaleRed, float scaleGreen, float scaleBlue)
        => Color(
            Alpha(color),
            Math.Clamp((int)(Red(color) * scaleRed), 0, 255),
            Math.Clamp((int)(Green(color) * scaleGreen), 0, 255),
            Math.Clamp((int)(Blue(color) * scaleBlue), 0, 255));

    //Greyscale converts to gray using perceptual weights
    public static int Greyscale(int color)
    {
        var greyscale = (int)((Red(color) * 0.3f) + (Green(color) * 0.59f) + (Blue(color) * 0.11f));
        return Color(Alpha(color), greyscale, greyscale, greyscale);
    }

    //AlphaBlend composites the source onto the destination by source alpha
    public static int AlphaBlend(int destination, int source)
    {
        var destinationAlpha = Alpha(destination);
        var sourceAlpha = Alpha(source);
        if (sourceAlpha == 255) return source;
        if (sourceAlpha == 0) return destination;
        var alpha = sourceAlpha + ((destinationAlpha * (255 - sourceAlpha)) / 255);
        return Color(
            alpha,
            AlphaBlendChannel(alpha, sourceAlpha, Red(destination), Red(source)),
            AlphaBlendChannel(alpha, sourceAlpha, Green(destination), Green(source)),
            AlphaBlendChannel(alpha, sourceAlpha, Blue(destination), Blue(source)));
    }

    private static int AlphaBlendChannel(int resultAlpha, int sourceAlpha, int destination, int source)
        => ((source * sourceAlpha) + (destination * (resultAlpha - sourceAlpha))) / resultAlpha;

    //SrgbLerp linear interpolation channel by channel
    public static int SrgbLerp(float alpha, int p0, int p1)
        => Color(
            Mth.LerpInt(alpha, Alpha(p0), Alpha(p1)),
            Mth.LerpInt(alpha, Red(p0), Red(p1)),
            Mth.LerpInt(alpha, Green(p0), Green(p1)),
            Mth.LerpInt(alpha, Blue(p0), Blue(p1)));
}
