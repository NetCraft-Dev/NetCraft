namespace NetCraft.Game.Util;

//BoundedFloatFunction 有界浮点函数接口对应原版 net.minecraft.util.BoundedFloatFunction<C>
//提供 apply/minValue/maxValue 三个方法原版 CubicSpline 用此接口作为坐标输入
//阶段 1 简化CubicSpline 直接持有 DensityFunction 不强制走此接口此处作为占位
public interface BoundedFloatFunction<C>
{
    float Apply(C input);
    float MinValue { get; }
    float MaxValue { get; }

    //Constant 常量工厂对应原版 BoundedFloatFunction.constant
    static BoundedFloatFunction<C> Constant<C>(float value)
        => new ConstantFunction<C>(value);

    //Identity 恒等函数对应原版 IDENTITY minValue/maxValue 取 float 极值
    static BoundedFloatFunction<float> Identity { get; } = new IdentityFunction();
}

//ConstantFunction 常量实现所有坐标返回固定值
internal sealed class ConstantFunction<C> : BoundedFloatFunction<C>
{
    private readonly float _value;
    public ConstantFunction(float value) { _value = value; }
    public float Apply(C input) => _value;
    public float MinValue => _value;
    public float MaxValue => _value;
}

//IdentityFunction 恒等函数 apply 返回输入本身
internal sealed class IdentityFunction : BoundedFloatFunction<float>
{
    public float Apply(float input) => input;
    public float MinValue => float.NegativeInfinity;
    public float MaxValue => float.PositiveInfinity;
}
