namespace NetCraft.Game.Util;

//BoundedFloatFunction bounded float function interface, maps to vanilla net.minecraft.util.BoundedFloatFunction<C>
//Provides apply/minValue/maxValue; vanilla CubicSpline uses this interface as coordinate input
//Simplified in phase 1: CubicSpline holds DensityFunction directly and does not go through this interface; kept here as a placeholder
public interface BoundedFloatFunction<C>
{
    float Apply(C input);
    float MinValue { get; }
    float MaxValue { get; }

    //Constant constant factory, maps to vanilla BoundedFloatFunction.constant
    static BoundedFloatFunction<C> Constant<C>(float value)
        => new ConstantFunction<C>(value);

    //Identity identity function, maps to vanilla IDENTITY; minValue/maxValue are the float extremes
    static BoundedFloatFunction<float> Identity { get; } = new IdentityFunction();
}

//ConstantFunction constant implementation; every coordinate returns a fixed value
internal sealed class ConstantFunction<C> : BoundedFloatFunction<C>
{
    private readonly float _value;
    public ConstantFunction(float value) { _value = value; }
    public float Apply(C input) => _value;
    public float MinValue => _value;
    public float MaxValue => _value;
}

//IdentityFunction identity function; apply returns the input itself
internal sealed class IdentityFunction : BoundedFloatFunction<float>
{
    public float Apply(float input) => input;
    public float MinValue => float.NegativeInfinity;
    public float MaxValue => float.PositiveInfinity;
}
