using NetCraft.Primitives;
using NetCraft.Util.Random;
using System.Runtime.CompilerServices;

namespace NetCraft.Util;

//数学工具集对应原版net.minecraft.util.Mth
//移植三角函数查表/插值/角度/位运算/随机等纯数学方法
//不移植依赖游戏类型的方法rayIntersectsAABB/lerp(Vec3)/rotationAroundAxis/mulAndTruncate
public static partial class Mth
{
    //常量对应原版PI/HALF_PI/TWO_PI/DEG_TO_RAD/RAD_TO_DEG/EPSILON
    public const float Pi = 3.1415927f;
    public const float HalfPi = 1.5707964f;
    public const float TwoPi = 6.2831855f;
    public const float DegToRad = 0.017453292f;
    public const float RadToDeg = 57.295776f;
    public const float Epsilon = 1.0E-5f;

    //SIN查表参数对应原版SIN_QUANTIZATION/SIN_MASK/COS_OFFSET/SIN_SCALE
    private const int SinQuantization = 65536;
    private const int SinMask = 65535;
    private const int CosOffset = 16384;
    private const double SinScale = 10430.378350470453d;
    private const double OneSixth = 0.16666666666666666d;
    private const double FracBias = 4.805340802404319232E-308d;

    //LUT_SIZE查表大小对应原版LUT_SIZE
    private const int LutSize = 257;

    //UUID版本与变体掩码对应原版UUID_VERSION/UUID_VERSION_TYPE_4/UUID_VARIANT/UUID_VARIANT_2
    private const long UuidVersion = 61440;
    private const long UuidVersionType4 = 16384;
    private const long UuidVariant = -4611686018427387904L;
    private const long UuidVariant2 = long.MinValue;

    public static readonly float SqrtOfTwo = (float)Math.Sqrt(2.0f);

    //SIN查表对应原版SIN数组按角度索引取正弦值
    //字段名带下划线避免与Sin方法同名冲突C#不允许字段和方法同名
    private static readonly float[] _sin = BuildSinTable();

    //DeBruijn位顺序表对应原版MULTIPLY_DE_BRUIJN_BIT_POSITION用于ceillog2
    private static readonly int[] MultiplyDeBruijnBitPosition =
        { 0, 1, 28, 2, 29, 14, 24, 3, 30, 22, 20, 15, 25, 17, 4, 8, 31, 27, 13, 23, 21, 19, 16, 7, 26, 12, 18, 6, 11, 5, 10, 9 };

    //ASIN/COS查表对应原版ASIN_TAB/COS_TAB用于atan2快速近似
    private static readonly double[] AsinTab = new double[LutSize];
    private static readonly double[] CosTab = new double[LutSize];

    static Mth()
    {
        for (var ind = 0; ind < LutSize; ind++)
        {
            var v = ind / 256.0d;
            var asinv = Math.Asin(v);
            CosTab[ind] = Math.Cos(asinv);
            AsinTab[ind] = asinv;
        }
    }

    //buildSinTable构造SIN查表对应原版Util.make(new float[65536])
    private static float[] BuildSinTable()
    {
        var sin = new float[SinQuantization];
        for (var i = 0; i < sin.Length; i++)
            sin[i] = (float)Math.Sin(i / SinScale);
        return sin;
    }

    //sin查表法正弦对应原版sin(double)
    //角度转索引按位与避免越界
    public static float Sin(double i)
        => _sin[(int)(((long)(i * SinScale)) & SinMask)];

    //cos查表法余弦对应原版cos(double)
    //cos(x)=sin(x+pi/2)用偏移COS_OFFSET实现
    public static float Cos(double i)
        => _sin[(int)(((long)((i * SinScale) + CosOffset)) & SinMask)];

    //sqrt平方根对应原版sqrt(float)
    public static float Sqrt(float x) => (float)Math.Sqrt(x);

    //floor向下取整对应原版floor(float)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Floor(float v) => (int)Math.Floor(v);

    //floor向下取整对应原版floor(double)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Floor(double v) => (int)Math.Floor(v);

    //lfloor long向下取整对应原版lfloor
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long LFloor(double v) => (long)Math.Floor(v);

    //abs绝对值对应原版abs(float)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Abs(float v) => Math.Abs(v);

    //abs绝对值对应原版abs(int)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Abs(int v) => Math.Abs(v);

    //ceil向上取整对应原版ceil(float)
    public static int Ceil(float v) => (int)Math.Ceiling(v);

    //ceil向上取整对应原版ceil(double)
    public static int Ceil(double v) => (int)Math.Ceiling(v);

    //ceilLong向上取整返回long对应原版ceilLong
    public static long CeilLong(double v) => (long)Math.Ceiling(v);

    //clamp int范围限制对应原版clamp(int,int,int)
    public static int Clamp(int value, int min, int max)
        => Math.Min(Math.Max(value, min), max);

    //clamp long范围限制对应原版clamp(long,long,long)
    public static long Clamp(long value, long min, long max)
        => Math.Min(Math.Max(value, min), max);

    //clamp float范围限制对应原版clamp(float,float,float)
    public static float Clamp(float value, float min, float max)
        => value < min ? min : Math.Min(value, max);

    //clamp double范围限制对应原版clamp(double,double,double)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Clamp(double value, double min, double max)
        => value < min ? min : Math.Min(value, max);

    //clampedLerp带边界限制的线性插值对应原版clampedLerp(double)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ClampedLerp(double factor, double min, double max)
    {
        if (factor < 0.0d) return min;
        if (factor > 1.0d) return max;
        return Lerp(factor, min, max);
    }

    //clampedLerp带边界限制的线性插值对应原版clampedLerp(float)
    public static float ClampedLerp(float factor, float min, float max)
    {
        if (factor < 0.0f) return min;
        if (factor > 1.0f) return max;
        return Lerp(factor, min, max);
    }

    //absMax取两数绝对值较大者对应原版absMax(int)
    public static int AbsMax(int a, int b) => Math.Max(Math.Abs(a), Math.Abs(b));

    //absMax取两数绝对值较大者对应原版absMax(float)
    public static float AbsMax(float a, float b) => Math.Max(Math.Abs(a), Math.Abs(b));

    //absMax取两数绝对值较大者对应原版absMax(double)
    public static double AbsMax(double a, double b) => Math.Max(Math.Abs(a), Math.Abs(b));

    //chessboardDistance棋盘距离对应原版chessboardDistance
    public static int ChessboardDistance(int x0, int z0, int x1, int z1)
        => AbsMax(x1 - x0, z1 - z0);

    //floorDiv向下整除对应原版floorDiv(int,int)
    //Java Math.floorDiv 对负数向下取整 C# 用 (a - (b-1)) / b 当 a%b!=0 时降1
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int FloorDiv(int a, int b)
    {
        var q = a / b;
        if ((a ^ b) < 0 && (q * b != a)) q--;
        return q;
    }

    //nextInt区间随机整数对应原版nextInt(RandomSource,int,int)
    public static int NextInt(RandomSource random, int minInclusive, int maxInclusive)
    {
        if (minInclusive >= maxInclusive) return minInclusive;
        return random.NextInt(maxInclusive - minInclusive + 1) + minInclusive;
    }

    //nextFloat区间随机浮点对应原版nextFloat
    public static float NextFloat(RandomSource random, float min, float max)
    {
        if (min >= max) return min;
        return random.NextFloat() * (max - min) + min;
    }

    //nextDouble区间随机双精度对应原版nextDouble
    public static double NextDouble(RandomSource random, double min, double max)
    {
        if (min >= max) return min;
        return random.NextDouble() * (max - min) + min;
    }

    //equal近似相等对应原版equal(float,float)
    public static bool Equal(float a, float b) => Math.Abs(b - a) < 1.0E-5f;

    //equal近似相等对应原版equal(double,double)
    public static bool Equal(double a, double b) => Math.Abs(b - a) < 9.999999747378752E-6d;

    //positiveModulo正模运算对应原版positiveModulo(int)
    //Java Math.floorMod C# 用 ((a % b) + b) % b
    public static int PositiveModulo(int input, int mod)
        => ((input % mod) + mod) % mod;

    //positiveModulo正模运算对应原版positiveModulo(float)
    public static float PositiveModulo(float input, float mod)
        => ((input % mod) + mod) % mod;

    //positiveModulo正模运算对应原版positiveModulo(double)
    public static double PositiveModulo(double input, double mod)
        => ((input % mod) + mod) % mod;

    //isMultipleOf判断整除对应原版isMultipleOf
    public static bool IsMultipleOf(int dividend, int divisor)
        => dividend % divisor == 0;

    //packDegrees角度压缩到byte对应原版packDegrees
    public static byte PackDegrees(float angle)
        => (byte)Floor(angle * 256.0f / 360.0f);

    //unpackDegrees解压byte到角度对应原版unpackDegrees
    public static float UnpackDegrees(byte rot)
        => rot * 360 / 256.0f;

    //wrapDegrees角度归一化到[-180,180)对应原版wrapDegrees(int)
    public static int WrapDegrees(int angle)
    {
        var n = angle % 360;
        if (n >= 180) n -= 360;
        if (n < -180) n += 360;
        return n;
    }

    //wrapDegrees角度归一化对应原版wrapDegrees(long)
    public static float WrapDegrees(long angle)
    {
        var n = (float)(angle % 360);
        if (n >= 180.0f) n -= 360.0f;
        if (n < -180.0f) n += 360.0f;
        return n;
    }

    //wrapDegrees角度归一化对应原版wrapDegrees(float)
    public static float WrapDegrees(float angle)
    {
        var n = angle % 360.0f;
        if (n >= 180.0f) n -= 360.0f;
        if (n < -180.0f) n += 360.0f;
        return n;
    }

    //wrapDegrees角度归一化对应原版wrapDegrees(double)
    public static double WrapDegrees(double angle)
    {
        var n = angle % 360.0d;
        if (n >= 180.0d) n -= 360.0d;
        if (n < -180.0d) n += 360.0d;
        return n;
    }

    //wrapDegrees90角度归一化到[-45,45)对应原版wrapDegrees90
    public static float WrapDegrees90(float angle)
    {
        var n = angle % 90.0f;
        if (n >= 45.0f) n -= 90.0f;
        if (n < -45.0f) n += 90.0f;
        return n;
    }

    //degreesDifference角度差归一化对应原版degreesDifference
    public static float DegreesDifference(float fromAngle, float toAngle)
        => WrapDegrees(toAngle - fromAngle);

    //degreesDifferenceAbs角度差绝对值对应原版degreesDifferenceAbs
    public static float DegreesDifferenceAbs(float angleA, float angleB)
        => Abs(DegreesDifference(angleA, angleB));

    //rotateIfNecessary按最大角差限制旋转对应原版rotateIfNecessary
    public static float RotateIfNecessary(float baseAngle, float targetAngle, float maxAngleDiff)
    {
        var delta = DegreesDifference(baseAngle, targetAngle);
        var clamped = Clamp(delta, -maxAngleDiff, maxAngleDiff);
        return targetAngle - clamped;
    }

    //approach逐步接近目标值对应原版approach
    public static float Approach(float current, float target, float increment)
    {
        var inc = Abs(increment);
        if (current < target)
            return Clamp(current + inc, current, target);
        return Clamp(current - inc, target, current);
    }

    //approachDegrees角度逐步接近对应原版approachDegrees
    public static float ApproachDegrees(float current, float target, float increment)
    {
        var difference = DegreesDifference(current, target);
        return Approach(current, current + difference, increment);
    }

    //getInt安全解析整数对应原版getInt
    //原版用NumberUtils.toInt C# 用 int.TryParse
    public static int GetInt(string input, int def)
        => int.TryParse(input, out var v) ? v : def;

    //smallestEncompassingPowerOfTwo最小包含2的幂对应原版smallestEncompassingPowerOfTwo
    public static int SmallestEncompassingPowerOfTwo(int input)
    {
        var result = input - 1;
        result |= result >> 1;
        result |= result >> 2;
        result |= result >> 4;
        result |= result >> 8;
        return (result | (result >> 16)) + 1;
    }

    //smallestSquareSide最小正方形边长对应原版smallestSquareSide
    public static int SmallestSquareSide(int itemCount)
    {
        if (itemCount < 0)
            throw new ArgumentException("itemCount must be greater than or equal to zero");
        return Ceil(Math.Sqrt(itemCount));
    }

    //isPowerOfTwo判断2的幂对应原版isPowerOfTwo(int)
    public static bool IsPowerOfTwo(int input)
        => input != 0 && (input & (input - 1)) == 0;

    //isPowerOfTwo判断2的幂对应原版isPowerOfTwo(long)
    public static bool IsPowerOfTwo(long input)
        => input != 0 && (input & (input - 1)) == 0;

    //ceillog2向上log2对应原版ceillog2
    public static int CeilLog2(int input)
    {
        var v = IsPowerOfTwo(input) ? input : SmallestEncompassingPowerOfTwo(input);
        return MultiplyDeBruijnBitPosition[(int)(((long)v * 125613361) >> 27) & 31];
    }

    //log2向下log2对应原版log2
    public static int Log2(int input)
        => CeilLog2(input) - (IsPowerOfTwo(input) ? 0 : 1);

    //frac取小数部分对应原版frac(float)
    public static float Frac(float num) => num - Floor(num);

    //frac取小数部分对应原版frac(double)
    public static double Frac(double num) => num - LFloor(num);

    //getSeed按位置生成稳定种子对应原版getSeed(int,int,int)
    //原版算术依赖int乘法wrap再扩展到long再long乘法wrap默认unchecked保证一致
    public static long GetSeed(int x, int y, int z)
    {
        unchecked
        {
            int mixed = (x * 3129871) ^ (z * 116129781) ^ y;
            var seed = (long)mixed;
            return ((seed * seed) * 42317861L + seed * 11L) >> 16;
        }
    }

    //getSeed按Vec3i生成种子对应原版getSeed(Vec3i)
    public static long GetSeed(Vec3i vec) => GetSeed(vec.X, vec.Y, vec.Z);

    //createInsecureUUID生成不安全UUID对应原版createInsecureUUID
    //C# Guid内部两个long字段直接构造
    public static Guid CreateInsecureUuid(RandomSource random)
    {
        var most = (random.NextLong() & (~UuidVersion)) | UuidVersionType4;
        var least = (random.NextLong() & 4611686018427387903L) | UuidVariant2;
        return new Guid((int)(most >> 32), (short)(most >> 16), (short)most,
            (byte)(least >> 56), (byte)(least >> 48), (byte)(least >> 40),
            (byte)(least >> 32), (byte)(least >> 24), (byte)(least >> 16), (byte)(least >> 8), (byte)least);
    }

    //inverseLerp反向插值对应原版inverseLerp(double)
    public static double InverseLerp(double value, double min, double max)
        => (value - min) / (max - min);

    //inverseLerp反向插值对应原版inverseLerp(float)
    public static float InverseLerp(float value, float min, float max)
        => (value - min) / (max - min);

    //atan2快速反正切对应原版atan2
    //用ASIN_TAB/COS_TAB查表+一次Newton迭代
    public static double Atan2(double y, double x)
    {
        var d2 = x * x + y * y;
        if (double.IsNaN(d2)) return double.NaN;
        var negY = y < 0.0d;
        if (negY) y = -y;
        var negX = x < 0.0d;
        if (negX) x = -x;
        var steep = y > x;
        if (steep) (x, y) = (y, x);
        var rinv = FastInvSqrt(d2);
        var x2 = x * rinv;
        var y2 = y * rinv;
        var yp = FracBias + y2;
        var index = (int)BitConverter.DoubleToInt64Bits(yp);
        var phi = AsinTab[index];
        var cPhi = CosTab[index];
        var sPhi = yp - FracBias;
        var sd = y2 * cPhi - x2 * sPhi;
        var d = (6.0d + sd * sd) * sd * OneSixth;
        var theta = phi + d;
        if (steep) theta = 1.5707963267948966d - theta;
        if (negX) theta = 3.141592653589793d - theta;
        if (negY) theta = -theta;
        return theta;
    }

    //invSqrt平方根倒数对应原版invSqrt(float)
    //C#无Math.invsqrt用1/sqrt替代
    public static float InvSqrt(float x) => 1.0f / (float)Math.Sqrt(x);

    //invSqrt平方根倒数对应原版invSqrt(double)
    public static double InvSqrt(double x) => 1.0 / Math.Sqrt(x);

    //fastInvSqrt快速平方根倒数对应原版fastInvSqrt
    //位运算魔数近似Newton迭代一次
    public static double FastInvSqrt(double x)
    {
        var xhalf = 0.5d * x;
        var i = BitConverter.DoubleToInt64Bits(x);
        var x2 = BitConverter.Int64BitsToDouble(6910469410427058090L - (i >> 1));
        return x2 * (1.5d - xhalf * x2 * x2);
    }

    //fastInvCubeRoot快速立方根倒数对应原版fastInvCubeRoot
    public static float FastInvCubeRoot(float x)
    {
        var i = BitConverter.SingleToInt32Bits(x);
        var y = BitConverter.Int32BitsToSingle(1419967116 - (i / 3));
        var y2 = 0.6666667f * y + 1.0f / (3.0f * y * y * x);
        return 0.6666667f * y2 + 1.0f / (3.0f * y2 * y2 * x);
    }

    //hsvToRgb HSV转RGB int对应原版hsvToRgb
    //alpha固定0委托hsvToArgb
    public static int HsvToRgb(float hue, float saturation, float value)
        => HsvToArgb(hue, saturation, value, 0);

    //hsvToArgb HSV转ARGB int对应原版hsvToArgb
    //手动展开6种情况ARGB.color内联计算
    public static int HsvToArgb(float hue, float saturation, float value, int alpha)
    {
        var h = ((int)(hue * 6.0f)) % 6;
        var f = hue * 6.0f - h;
        var p = value * (1.0f - saturation);
        var q = value * (1.0f - f * saturation);
        var t = value * (1.0f - (1.0f - f) * saturation);
        float red, green, blue;
        switch (h)
        {
            case 0: red = value; green = t; blue = p; break;
            case 1: red = q; green = value; blue = p; break;
            case 2: red = p; green = value; blue = t; break;
            case 3: red = p; green = q; blue = value; break;
            case 4: red = t; green = p; blue = value; break;
            case 5: red = value; green = p; blue = q; break;
            default: throw new InvalidOperationException($"HSV to RGB failed: {hue}, {saturation}, {value}");
        }
        var r = Clamp((int)(red * 255.0f), 0, 255);
        var g = Clamp((int)(green * 255.0f), 0, 255);
        var b = Clamp((int)(blue * 255.0f), 0, 255);
        return (alpha << 24) | (r << 16) | (g << 8) | b;
    }

    //murmurHash3Mixer MurmurHash3混合器对应原版murmurHash3Mixer
    public static int MurmurHash3Mixer(int hash)
    {
        unchecked
        {
            var hash2 = (hash ^ (hash >>> 16)) * -2048144789;
            var hash3 = (hash2 ^ (hash2 >>> 13)) * -1028477387;
            return hash3 ^ (hash3 >>> 16);
        }
    }

    //binarySearch二分查找对应原版binarySearch
    //condition.test为true时左移否则右移
    public static int BinarySearch(int from, int to, Func<int, bool> condition)
    {
        var i = to - from;
        while (i > 0)
        {
            var half = i / 2;
            var middle = from + half;
            if (condition(middle))
                i = half;
            else
            {
                from = middle + 1;
                i = i - (half + 1);
            }
        }
        return from;
    }

    //lerpInt整数线性插值对应原版lerpInt
    public static int LerpInt(float alpha, int p0, int p1)
        => p0 + Floor(alpha * (p1 - p0));

    //lerpDiscrete整数离散插值对应原版lerpDiscrete
    public static int LerpDiscrete(float alpha, int p0, int p1)
    {
        var delta = p1 - p0;
        return p0 + Floor(alpha * (delta - 1)) + (alpha > 0.0f ? 1 : 0);
    }

    //lerp float线性插值对应原版lerp(float)
    public static float Lerp(float alpha, float p0, float p1)
        => p0 + alpha * (p1 - p0);

    //lerp double线性插值对应原版lerp(double)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Lerp(double alpha, double p0, double p1)
        => p0 + alpha * (p1 - p0);

    //lerp2 双线性插值对应原版lerp2
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Lerp2(double alpha1, double alpha2, double x00, double x10, double x01, double x11)
        => Lerp(alpha2, Lerp(alpha1, x00, x10), Lerp(alpha1, x01, x11));

    //lerp3 三线性插值对应原版lerp3
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Lerp3(double a1, double a2, double a3,
        double x000, double x100, double x010, double x110,
        double x001, double x101, double x011, double x111)
        => Lerp(a3, Lerp2(a1, a2, x000, x100, x010, x110), Lerp2(a1, a2, x001, x101, x011, x111));

    //catmullrom Catmull-Rom样条插值对应原版catmullrom
    public static float CatmullRom(float alpha, float p0, float p1, float p2, float p3)
        => 0.5f * (2.0f * p1 + (p2 - p0) * alpha +
            ((((2.0f * p0) - (5.0f * p1) + (4.0f * p2) - p3) * alpha * alpha)) +
            ((((3.0f * p1) - p0 - (3.0f * p2) + p3) * alpha * alpha * alpha)));

    //smoothstep平滑插值对应原版smoothstep
    public static double Smoothstep(double x)
        => x * x * x * ((x * ((x * 6.0d) - 15.0d)) + 10.0d);

    //smoothstepDerivative平滑插值导数对应原版smoothstepDerivative
    public static double SmoothstepDerivative(double x)
        => 30.0d * x * x * (x - 1.0d) * (x - 1.0d);

    //sign符号函数对应原版sign
    public static int Sign(double number)
    {
        if (number == 0.0d) return 0;
        return number > 0.0d ? 1 : -1;
    }

    //rotLerp旋转角度线性插值对应原版rotLerp(float)
    public static float RotLerp(float a, float from, float to)
        => from + a * WrapDegrees(to - from);

    //rotLerp旋转角度线性插值对应原版rotLerp(double)
    public static double RotLerp(double a, double from, double to)
        => from + a * WrapDegrees(to - from);

    //rotLerpRad弧度旋转线性插值对应原版rotLerpRad
    //循环归一化到[-pi,pi)
    public static float RotLerpRad(float a, float from, float to)
    {
        var f = to - from;
        while (f < -Pi) f += TwoPi;
        while (f >= Pi) f -= TwoPi;
        return from + a * f;
    }

    //triangleWave三角波对应原版triangleWave
    public static float TriangleWave(float index, float period)
        => (Math.Abs(index % period - period * 0.5f) - period * 0.25f) / (period * 0.25f);

    //square平方对应原版square(float)
    public static float Square(float x) => x * x;

    //cube立方对应原版cube
    public static float Cube(float x) => x * x * x;

    //square平方对应原版square(double)
    public static double Square(double x) => x * x;

    //square平方对应原版square(int)
    public static int Square(int x) => x * x;

    //square平方对应原版square(long)
    public static long Square(long x) => x * x;

    //clampedMap带边界范围映射对应原版clampedMap(double)
    public static double ClampedMap(double value, double fromMin, double fromMax, double toMin, double toMax)
        => ClampedLerp(InverseLerp(value, fromMin, fromMax), toMin, toMax);

    //clampedMap带边界范围映射对应原版clampedMap(float)
    public static float ClampedMap(float value, float fromMin, float fromMax, float toMin, float toMax)
        => ClampedLerp(InverseLerp(value, fromMin, fromMax), toMin, toMax);

    //map范围映射对应原版map(double)
    public static double Map(double value, double fromMin, double fromMax, double toMin, double toMax)
        => Lerp(InverseLerp(value, fromMin, fromMax), toMin, toMax);

    //map范围映射对应原版map(float)
    public static float Map(float value, float fromMin, float fromMax, float toMin, float toMax)
        => Lerp(InverseLerp(value, fromMin, fromMax), toMin, toMax);

    //wobble坐标抖动对应原版wobble
    //原版用createThreadLocalInstance C# 简化为RandomSource.Create按坐标种子确定
    public static double Wobble(double coord)
    {
        var r = RandomSource.Create(Floor(coord * 3000.0d));
        return coord + ((2.0d * r.NextDouble() - 1.0d) * 1.0E-7d) / 2.0d;
    }

    //roundToward向上取整到倍数对应原版roundToward(int)
    public static int RoundToward(int input, int multiple)
        => PositiveCeilDiv(input, multiple) * multiple;

    //roundToward向上取整到倍数对应原版roundToward(long)
    public static long RoundToward(long input, long multiple)
        => PositiveCeilDiv(input, multiple) * multiple;

    //positiveCeilDiv正向上整除对应原版positiveCeilDiv(int)
    public static int PositiveCeilDiv(int input, int divisor)
        => -FloorDiv(-input, divisor);

    //positiveCeilDiv正向上整除对应原版positiveCeilDiv(long)
    public static long PositiveCeilDiv(long input, long divisor)
        => -FloorDivLong(-input, divisor);

    //floorDivLong long向下整除对应原版Math.floorDiv(long,long)
    private static long FloorDivLong(long a, long b)
    {
        var q = a / b;
        if ((a ^ b) < 0 && (q * b != a)) q--;
        return q;
    }

    //randomBetweenInclusive闭区间随机整数对应原版randomBetweenInclusive
    public static int RandomBetweenInclusive(RandomSource random, int min, int maxInclusive)
        => random.NextInt(maxInclusive - min + 1) + min;

    //randomBetween区间随机浮点对应原版randomBetween
    public static float RandomBetween(RandomSource random, float min, float maxExclusive)
        => random.NextFloat() * (maxExclusive - min) + min;

    //normal正态分布随机对应原版normal
    public static float Normal(RandomSource random, float mean, float deviation)
        => mean + (float)random.NextGaussian() * deviation;

    //lengthSquared 2D长度平方对应原版lengthSquared(double,double)
    public static double LengthSquared(double x, double y) => x * x + y * y;

    //length 2D长度对应原版length(double,double)
    public static double Length(double x, double y) => Math.Sqrt(LengthSquared(x, y));

    //length 2D float长度对应原版length(float,float)
    public static float Length(float x, float y) => (float)Math.Sqrt(LengthSquared(x, y));

    //lengthSquared 3D长度平方对应原版lengthSquared(double,double,double)
    public static double LengthSquared(double x, double y, double z) => x * x + y * y + z * z;

    //length 3D长度对应原版length(double,double,double)
    public static double Length(double x, double y, double z) => Math.Sqrt(LengthSquared(x, y, z));

    //lengthSquared 3D float长度平方对应原版lengthSquared(float,float,float)
    public static float LengthSquared(float x, float y, float z) => x * x + y * y + z * z;

    //quantize按分辨率量化对应原版quantize
    public static int Quantize(double value, int quantizeResolution)
        => Floor(value / quantizeResolution) * quantizeResolution;

    //outFromOrigin从原点向外迭代对应原版outFromOrigin
    //原版用IntStream.iterate C# 用yield return模拟
    public static IEnumerable<int> OutFromOrigin(int origin, int lowerBound, int upperBound, int stepSize = 1)
    {
        if (lowerBound > upperBound)
            throw new ArgumentException($"upperBound {upperBound} expected to be > lowerBound {lowerBound}");
        if (stepSize < 1)
            throw new ArgumentException($"step size expected to be >= 1, was {stepSize}");
        var clampedOrigin = Clamp(origin, lowerBound, upperBound);
        var cursor = clampedOrigin;
        var wentNegative = false;
        yield return cursor;
        while (true)
        {
            var previousWasNegative = cursor <= clampedOrigin;
            var distance = Math.Abs(clampedOrigin - cursor);
            var canMovePositive = clampedOrigin + distance + stepSize <= upperBound;
            int next;
            if (!previousWasNegative || !canMovePositive)
            {
                var attempted = clampedOrigin - distance - (previousWasNegative ? stepSize : 0);
                if (attempted >= lowerBound)
                    next = attempted;
                else
                    next = clampedOrigin + distance + stepSize;
            }
            else
                next = clampedOrigin + distance + stepSize;
            if (next == cursor) yield break;
            cursor = next;
            yield return cursor;
        }
    }
}
