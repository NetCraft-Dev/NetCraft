namespace NetCraft.Game.Util;

//Fraction 精简有理数 对应原版 apache commons math 的 Fraction
//供 BundleContents 这类按重量配比计算的地方使用 只保留项目用到的运算
public sealed class Fraction
{
    public static readonly Fraction Zero = new(0, 1);
    public static readonly Fraction One = new(1, 1);

    private readonly int _numerator;
    private readonly int _denominator;

    private Fraction(int numerator, int denominator)
    {
        if (denominator == 0) throw new ArithmeticException("Denominator must not be zero");
        if (denominator < 0)
        {
            numerator = -numerator;
            denominator = -denominator;
        }
        var divisor = Gcd(Math.Abs(numerator), denominator);
        _numerator = numerator / divisor;
        _denominator = denominator / divisor;
    }

    public static Fraction GetFraction(int numerator, int denominator) => new(numerator, denominator);

    public static Fraction GetFraction(int value) => new(value, 1);

    public int Numerator => _numerator;

    public int Denominator => _denominator;

    //Add 通分相加 溢出按原版抛算术异常由调用方兜底
    public Fraction Add(Fraction other) => GetFraction(
        checked(_numerator * other._denominator + other._numerator * _denominator),
        checked(_denominator * other._denominator));

    public Fraction Subtract(Fraction other) => GetFraction(
        checked(_numerator * other._denominator - other._numerator * _denominator),
        checked(_denominator * other._denominator));

    public Fraction MultiplyBy(Fraction other) => GetFraction(
        checked(_numerator * other._numerator),
        checked(_denominator * other._denominator));

    public Fraction DivideBy(Fraction other) => GetFraction(
        checked(_numerator * other._denominator),
        checked(_denominator * other._numerator));

    //IntValue 截断取整 对应原版 intValue
    public int IntValue => _numerator / _denominator;

    public override bool Equals(object? obj) => obj is Fraction other && _numerator == other._numerator && _denominator == other._denominator;

    public override int GetHashCode() => HashCode.Combine(_numerator, _denominator);

    public override string ToString() => _denominator == 1 ? _numerator.ToString() : $"{_numerator}/{_denominator}";

    private static int Gcd(int left, int right)
    {
        while (right != 0) (left, right) = (right, left % right);
        return left == 0 ? 1 : left;
    }
}
