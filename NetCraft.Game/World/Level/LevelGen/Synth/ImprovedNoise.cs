using System.Runtime.CompilerServices;
using NetCraft.Util;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Synth;

//ImprovedNoise improved Perlin noise, maps to vanilla net.minecraft.world.level.levelgen.synth.ImprovedNoise
//Classic Perlin noise implementation: a byte[256] permutation array plus trilinear interpolation over 8 corners
//The GRADIENT table is shared with SimplexNoise and lives in this class
public sealed class ImprovedNoise
{
    private const float ShiftUpEpsilon = 1.0E-7f;

    //GRADIENT 16 3D gradient vectors, aligned with vanilla SimplexNoise.GRADIENT
    //Flattened to 1D: the original int[][] had to fetch a sub-array before indexing in every gradDot, while here index * 3 reads directly
    internal static readonly int[] GRADIENT =
    {
        1, 1, 0, -1, 1, 0, 1, -1, 0, -1, -1, 0,
        1, 0, 1, -1, 0, 1, 1, 0, -1, -1, 0, -1,
        0, 1, 1, 0, -1, 1, 0, 1, -1, 0, -1, -1,
        1, 1, 0, 0, -1, 1, -1, 1, 0, 0, -1, -1
    };

    private readonly byte[] _p = new byte[256];
    //Xo/Yo/Zo sampling offsets; vanilla exposes them as public final fields, so use fields here instead of auto-properties
    //Property getters add a layer on this per-block hot path that the JIT may not inline, and three are read per sample
    public readonly double Xo;
    public readonly double Yo;
    public readonly double Zo;

    public ImprovedNoise(RandomSource random)
    {
        Xo = random.NextDouble() * 256;
        Yo = random.NextDouble() * 256;
        Zo = random.NextDouble() * 256;
        for (var i = 0; i < 256; i++)
            _p[i] = (byte)i;
        for (var i = 0; i < 256; i++)
        {
            var offset = random.NextInt(256 - i);
            var swap = _p[i];
            _p[i] = _p[i + offset];
            _p[i + offset] = swap;
        }
    }

    public double Noise(double x, double y, double z)
    {
        var dx = x + Xo;
        var dy = y + Yo;
        var dz = z + Zo;
        var xi = (int)Math.Floor(dx);
        var yi = (int)Math.Floor(dy);
        var zi = (int)Math.Floor(dz);
        var xf = dx - xi;
        var yf = dy - yi;
        var zf = dz - zi;
        var u = xf * xf * xf * ((xf * ((xf * 6.0d) - 15.0d)) + 10.0d);
        var v = yf * yf * yf * ((yf * ((yf * 6.0d) - 15.0d)) + 10.0d);
        var w = zf * zf * zf * ((zf * ((zf * 6.0d) - 15.0d)) + 10.0d);
        return SampleAndLerp(xi, yi, zi, xf, yf, zf, u, v, w);
    }

    //2D overload with z=0 for convenience
    public double Noise(double x, double y) => Noise(x, y, 0);

    //5-argument Noise, maps to vanilla noise(x,y,z,yScale,yFudge)
    //BlendedNoise uses yScale/yFudge to step-offset in y, making low-octave noise repeat within the same y range
    public double Noise(double x, double y, double z, double yScale, double yFudge)
    {
        var dx = x + Xo;
        var dy = y + Yo;
        var dz = z + Zo;
        var xi = (int)Math.Floor(dx);
        var yi = (int)Math.Floor(dy);
        var zi = (int)Math.Floor(dz);
        var xr = dx - xi;
        var yr = dy - yi;
        var zr = dz - zi;
        double yrFudge;
        if (yScale != 0.0)
        {
            //fudgeLimit takes the smaller of yFudge and yr to keep yrFudge inside the current y cell
            var fudgeLimit = yFudge >= 0.0 && yFudge < yr ? yFudge : yr;
            yrFudge = (int)Math.Floor(fudgeLimit / yScale + 1.0000000116860974E-7) * yScale;
        }
        else
        {
            yrFudge = 0.0;
        }
        return SampleAndLerpFudge(xi, yi, zi, xr, yr - yrFudge, zr, yr);
    }

    //SampleAndLerpFudge 7-argument version, maps to vanilla sampleAndLerp(yrOriginal)
    //yrOriginal feeds the yAlpha smooth interpolation, while yr has yrFudge subtracted for the gradient dot product
    private double SampleAndLerpFudge(int xi, int yi, int zi, double xr, double yr, double zr, double yrOriginal)
    {
        //Cache the gradient and permutation tables once; a single sample touches them 8 times, and repeated static and instance field access would add an extra indirection each time
        var gradient = GRADIENT;
        var p = _p;
        var n000 = GradDot(gradient, P(p, xi, yi, zi), xr, yr, zr);
        var n100 = GradDot(gradient, P(p, xi + 1, yi, zi), xr - 1, yr, zr);
        var n010 = GradDot(gradient, P(p, xi, yi + 1, zi), xr, yr - 1, zr);
        var n110 = GradDot(gradient, P(p, xi + 1, yi + 1, zi), xr - 1, yr - 1, zr);
        var n001 = GradDot(gradient, P(p, xi, yi, zi + 1), xr, yr, zr - 1);
        var n101 = GradDot(gradient, P(p, xi + 1, yi, zi + 1), xr - 1, yr, zr - 1);
        var n011 = GradDot(gradient, P(p, xi, yi + 1, zi + 1), xr, yr - 1, zr - 1);
        var n111 = GradDot(gradient, P(p, xi + 1, yi + 1, zi + 1), xr - 1, yr - 1, zr - 1);
        var xAlpha = xr * xr * xr * ((xr * ((xr * 6.0d) - 15.0d)) + 10.0d);
        var yAlpha = yrOriginal * yrOriginal * yrOriginal * ((yrOriginal * ((yrOriginal * 6.0d) - 15.0d)) + 10.0d);
        var zAlpha = zr * zr * zr * ((zr * ((zr * 6.0d) - 15.0d)) + 10.0d);
        var nx00 = n000 + xAlpha * (n100 - n000);
        var nx10 = n010 + xAlpha * (n110 - n010);
        var nx01 = n001 + xAlpha * (n101 - n001);
        var nx11 = n011 + xAlpha * (n111 - n011);
        var nxy0 = nx00 + yAlpha * (nx10 - nx00);
        var nxy1 = nx01 + yAlpha * (nx11 - nx01);
        return nxy0 + zAlpha * (nxy1 - nxy0);
    }

    private double SampleAndLerp(int xi, int yi, int zi, double xf, double yf, double zf, double u, double v, double w)
    {
        //Same as above: the 8 corners of one sample share the same table references
        var gradient = GRADIENT;
        var p = _p;
        var n000 = GradDot(gradient, P(p, xi, yi, zi), xf, yf, zf);
        var n100 = GradDot(gradient, P(p, xi + 1, yi, zi), xf - 1, yf, zf);
        var n010 = GradDot(gradient, P(p, xi, yi + 1, zi), xf, yf - 1, zf);
        var n110 = GradDot(gradient, P(p, xi + 1, yi + 1, zi), xf - 1, yf - 1, zf);
        var n001 = GradDot(gradient, P(p, xi, yi, zi + 1), xf, yf, zf - 1);
        var n101 = GradDot(gradient, P(p, xi + 1, yi, zi + 1), xf - 1, yf, zf - 1);
        var n011 = GradDot(gradient, P(p, xi, yi + 1, zi + 1), xf, yf - 1, zf - 1);
        var n111 = GradDot(gradient, P(p, xi + 1, yi + 1, zi + 1), xf - 1, yf - 1, zf - 1);
        var nx00 = n000 + u * (n100 - n000);
        var nx10 = n010 + u * (n110 - n010);
        var nx01 = n001 + u * (n101 - n001);
        var nx11 = n011 + u * (n111 - n011);
        var nxy0 = nx00 + v * (nx10 - nx00);
        var nxy1 = nx01 + v * (nx11 - nx01);
        return nxy0 + w * (nxy1 - nxy0);
    }

    //GradDot gradient dot product; the table is passed in by the caller, avoiding class-init checks and base address fetches on each static field access
    //Evaluation order is unchanged: the three products still accumulate as (a+b)+c
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double GradDot(int[] gradient, int hash, double x, double y, double z)
    {
        var i = (hash & 15) * 3;
        return gradient[i] * x + gradient[i + 1] * y + gradient[i + 2] * z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int P(byte[] p, int x, int y, int z)
        => p[(p[(p[x & 255] + y) & 255] + z) & 255] & 255;
}
