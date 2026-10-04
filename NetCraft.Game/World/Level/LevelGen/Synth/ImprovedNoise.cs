using System.Runtime.CompilerServices;
using NetCraft.Util;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Synth;

//ImprovedNoise 改进版柏林噪声对应原版 net.minecraft.world.level.levelgen.synth.ImprovedNoise
//经典 Perlin 噪声实现byte[256] 排列数组+8 角点三线性插值
//GRADIENT 梯度表与 SimplexNoise 共享放此类内
public sealed class ImprovedNoise
{
    private const float ShiftUpEpsilon = 1.0E-7f;

    //GRADIENT 16 个 3D 梯度向量对齐原版 SimplexNoise.GRADIENT
    //扁平成一维: 原来 int[][] 每次 gradDot 要先取子数组再索引 扁平后按下标 *3 直接取
    internal static readonly int[] GRADIENT =
    {
        1, 1, 0, -1, 1, 0, 1, -1, 0, -1, -1, 0,
        1, 0, 1, -1, 0, 1, 1, 0, -1, -1, 0, -1,
        0, 1, 1, 0, -1, 1, 0, 1, -1, 0, -1, -1,
        1, 1, 0, 0, -1, 1, -1, 1, 0, 0, -1, -1
    };

    private readonly byte[] _p = new byte[256];
    //Xo/Yo/Zo 采样偏移 原版就是 public final 字段 这里对齐用字段而不是自动属性
    //属性取值器在逐格采样的热路径上每次都要多过一层 JIT 未必内联 采样里要连读三个
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

    //2D 重载 z=0 方便调用
    public double Noise(double x, double y) => Noise(x, y, 0);

    //5 参数 Noise 对应原版 noise(x,y,z,yScale,yFudge)
    //BlendedNoise 用 yScale/yFudge 在 y 方向做阶梯偏移使低倍频噪声在同一 y 区间内重复
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
            //fudgeLimit 取 yFudge 与 yr 较小者防止 yrFudge 超出当前 y 格子
            var fudgeLimit = yFudge >= 0.0 && yFudge < yr ? yFudge : yr;
            yrFudge = (int)Math.Floor(fudgeLimit / yScale + 1.0000000116860974E-7) * yScale;
        }
        else
        {
            yrFudge = 0.0;
        }
        return SampleAndLerpFudge(xi, yi, zi, xr, yr - yrFudge, zr, yr);
    }

    //SampleAndLerpFudge 7 参数版本对应原版 sampleAndLerp(yrOriginal)
    //yrOriginal 用于 yAlpha 平滑插值yr 已减去 yrFudge 用于梯度点积
    private double SampleAndLerpFudge(int xi, int yi, int zi, double xr, double yr, double zr, double yrOriginal)
    {
        //梯度表与置换表各取一次引用 一次采样要摸它们 8 次 逐次摸静态字段与实例字段都要多付一层寻址
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
        //同上 一次采样的 8 个角点共用同一份表引用
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

    //GradDot 梯度点积 表由调用方传进来 省掉每次访问静态字段的类初始化检查与基址获取
    //运算顺序与原来完全一致: 三个乘积累加依旧是 (a+b)+c
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
