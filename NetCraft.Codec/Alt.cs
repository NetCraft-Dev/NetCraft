namespace NetCraft.Codec;

//Alt 二选一容器 对应原版 com.mojang.datafixers.util.Either
//DataFixer 已有一个同名 Either 且绑 DFU 的 HKT 无法共用 故这里另起名
public sealed class Alt<L, R>
{
    private readonly bool _isLeft;
    private readonly L? _left;
    private readonly R? _right;

    private Alt(bool isLeft, L? left, R? right)
    {
        _isLeft = isLeft;
        _left = left;
        _right = right;
    }

    //Left 构造左值 对应原版 Either.left
    public static Alt<L, R> Left(L value) => new(true, value, default);

    //Right 构造右值 对应原版 Either.right
    public static Alt<L, R> Right(R value) => new(false, default, value);

    //IsLeft 是否为左值
    public bool IsLeft => _isLeft;
    //IsRight 是否为右值
    public bool IsRight => !_isLeft;

    //GetLeft 取左值 非左值时为空
    public Optional<L> GetLeft() => _isLeft ? Optional<L>.OfNullable(_left) : Optional<L>.Empty();

    //GetRight 取右值 非右值时为空
    public Optional<R> GetRight() => _isLeft ? Optional<R>.Empty() : Optional<R>.OfNullable(_right);

    //Map 两侧分别映射后折叠到同一类型
    public T Map<T>(Func<L, T> left, Func<R, T> right) => _isLeft ? left(_left!) : right(_right!);

    //Unwrap 两侧同类型时取出值 对应原版 Either.unwrap
    public static U Unwrap<U>(Alt<U, U> alt) => alt._isLeft ? alt._left! : alt._right!;

    public override string ToString() => _isLeft ? $"Left[{_left}]" : $"Right[{_right}]";
}

//EitherCodec 先按左编解码 解析失败退回右 对应原版 Codec.either
internal sealed class EitherCodec<L, R> : ScalarCodec<Alt<L, R>>
{
    private readonly Codec<L> _left;
    private readonly Codec<R> _right;

    public EitherCodec(Codec<L> left, Codec<R> right)
    {
        _left = left;
        _right = right;
    }

    public override DataResult<Alt<L, R>> Parse<U>(DynamicOps<U> ops, U input)
    {
        var leftResult = _left.Parse(ops, input);
        if (leftResult.Result().IsPresent) return leftResult.Map(Alt<L, R>.Left);
        return _right.Parse(ops, input).Map(Alt<L, R>.Right);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Alt<L, R> value)
        => value.IsLeft
            ? _left.EncodeStart(ops, value.GetLeft().Get())
            : _right.EncodeStart(ops, value.GetRight().Get());
}
