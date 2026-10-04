using System.Runtime.CompilerServices;
using NetCraft.Util;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Synth;

//SimplexNoise 单纯形噪声对应原版 net.minecraft.world.level.levelgen.synth.SimplexNoise
//Stefan Gustavson 版本3D 单纯形算法int[512] 排列数组
//GRADIENT 表与 ImprovedNoise 共享用 ImprovedNoise.GRADIENT
public sealed class SimplexNoise
{
    private const double Sqrt3 = 1.7320508075688772;
    //F2/G2 二维单纯形形变常数对应原版 SimplexNoise.F2/G2
    private const double F2 = 0.5 * (Sqrt3 - 1.0);
    private const double G2 = (3.0 - Sqrt3) / 6.0;

    private readonly int[] _p = new int[512];
    public double Xo { get; }
    public double Yo { get; }
    public double Zo { get; }

    public SimplexNoise(RandomSource random)
    {
        Xo = random.NextDouble() * 256;
        Yo = random.NextDouble() * 256;
        Zo = random.NextDouble() * 256;
        var perm = new int[256];
        for (var i = 0; i < 256; i++)
            perm[i] = i;
        for (var i = 0; i < 256; i++)
        {
            var offset = random.NextInt(256 - i);
            var swap = perm[i];
            perm[i] = perm[i + offset];
            perm[i + offset] = swap;
        }
        for (var i = 0; i < 512; i++)
            _p[i] = perm[i & 255];
    }

    //3D Simplex 噪声主入口
    public double GetValue(double xin, double yin, double zin)
    {
        var skew = (xin + yin + zin) / 3.0;
        var i = MthFloorSimplex(xin + skew);
        var j = MthFloorSimplex(yin + skew);
        var k = MthFloorSimplex(zin + skew);
        var unskew = (i + j + k) / 6.0;
        var x0 = xin - (i - unskew);
        var y0 = yin - (j - unskew);
        var z0 = zin - (k - unskew);

        int i1, j1, k1, i2, j2, k2;
        if (x0 >= y0)
        {
            if (y0 >= z0) { i1 = 1; j1 = 0; k1 = 0; i2 = 1; j2 = 1; k2 = 0; }
            else if (x0 >= z0) { i1 = 1; j1 = 0; k1 = 0; i2 = 1; j2 = 0; k2 = 1; }
            else { i1 = 0; j1 = 0; k1 = 1; i2 = 1; j2 = 0; k2 = 1; }
        }
        else
        {
            if (y0 < z0) { i1 = 0; j1 = 0; k1 = 1; i2 = 0; j2 = 1; k2 = 1; }
            else if (x0 < z0) { i1 = 0; j1 = 1; k1 = 0; i2 = 0; j2 = 1; k2 = 1; }
            else { i1 = 0; j1 = 1; k1 = 0; i2 = 1; j2 = 1; k2 = 0; }
        }

        var x1 = x0 - i1 + 1.0 / 6.0;
        var y1 = y0 - j1 + 1.0 / 6.0;
        var z1 = z0 - k1 + 1.0 / 6.0;
        var x2 = x0 - i2 + 2.0 / 6.0;
        var y2 = y0 - j2 + 2.0 / 6.0;
        var z2 = z0 - k2 + 2.0 / 6.0;
        var x3 = x0 - 1 + 3.0 / 6.0;
        var y3 = y0 - 1 + 3.0 / 6.0;
        var z3 = z0 - 1 + 3.0 / 6.0;

        var ii = i & 255;
        var jj = j & 255;
        var kk = k & 255;
        var gi0 = _p[ii + _p[jj + _p[kk]]] % 12;
        var gi1 = _p[ii + i1 + _p[jj + j1 + _p[kk + k1]]] % 12;
        var gi2 = _p[ii + i2 + _p[jj + j2 + _p[kk + k2]]] % 12;
        var gi3 = _p[ii + 1 + _p[jj + 1 + _p[kk + 1]]] % 12;

        var n0 = CornerContribution(x0, y0, z0, gi0, 0.6);
        var n1 = CornerContribution(x1, y1, z1, gi1, 0.6);
        var n2 = CornerContribution(x2, y2, z2, gi2, 0.6);
        var n3 = CornerContribution(x3, y3, z3, gi3, 0.6);
        return 32.0 * (n0 + n1 + n2 + n3);
    }

    //GetValue 二维单纯形噪声对应原版 SimplexNoise.getValue(xin, yin)
    //EndIslandDensityFunction 用于末地岛屿形状采样
    public double GetValue(double xin, double yin)
    {
        var s = (xin + yin) * F2;
        var i = Mth.Floor(xin + s);
        var j = Mth.Floor(yin + s);
        var t = (i + j) * G2;
        var x0 = xin - (i - t);
        var y0 = yin - (j - t);
        int i1, j1;
        if (x0 > y0) { i1 = 1; j1 = 0; }
        else { i1 = 0; j1 = 1; }
        var x1 = (x0 - i1) + G2;
        var y1 = (y0 - j1) + G2;
        var x2 = (x0 - 1.0) + (2.0 * G2);
        var y2 = (y0 - 1.0) + (2.0 * G2);
        var ii = i & 255;
        var jj = j & 255;
        var gi0 = P(ii + P(jj)) % 12;
        var gi1 = P((ii + i1) + P(jj + j1)) % 12;
        var gi2 = P((ii + 1) + P(jj + 1)) % 12;
        var n0 = CornerContribution(x0, y0, 0.0, gi0, 0.5);
        var n1 = CornerContribution(x1, y1, 0.0, gi1, 0.5);
        var n2 = CornerContribution(x2, y2, 0.0, gi2, 0.5);
        return 70.0 * (n0 + n1 + n2);
    }

    //P 排列数组索引对应原版 SimplexNoise.p(int)
    private int P(int x) => _p[x & 255];

    //CornerContribution 角点贡献对应原版 getCornerNoise3D
    //base 为贡献衰减阈值2D 用 0.5 3D 用 0.6
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CornerContribution(double x, double y, double z, int gradIndex, double baseValue)
    {
        var t = baseValue - x * x - y * y - z * z;
        if (t < 0) return 0;
        t *= t;
        var gi = (gradIndex & 15) * 3;
        //跨类取 ImprovedNoise.GRADIENT 次次都要过类初始化检查与静态基址获取 一次取好引用
        var gradient = ImprovedNoise.GRADIENT;
        return t * t * (gradient[gi] * x + gradient[gi + 1] * y + gradient[gi + 2] * z);
    }

    private static int MthFloorSimplex(double v) => (int)Math.Floor(v);
}
