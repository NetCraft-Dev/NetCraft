namespace NetCraft.Util.Random;

//Weighted entry, maps to vanilla net.minecraft.util.random.Weighted
//A value and weight pair, zero or positive, weight must not be negative
public sealed record Weighted<T>
{
    public T Value { get; }
    public int Weight { get; }

    //Weighted constructor, maps to vanilla Weighted(T,int)
    //Negative weight throws ArgumentException; zero-weight warnings are handled by the caller
    public Weighted(T value, int weight)
    {
        if (weight < 0)
            throw new ArgumentException("Weight should be >= 0");
        Value = value;
        Weight = weight;
    }

    //map transforms the value type keeping the weight, maps to vanilla map
    public Weighted<U> Map<U>(Func<T, U> function)
        => new(function(Value), Weight);

    //TODO Codec integration: add the codec static method once RecordCodecBuilder is ready
    //TODO StreamCodec integration: add the streamCodec static method after the NetCraft.Network.StreamCodec extension
}
