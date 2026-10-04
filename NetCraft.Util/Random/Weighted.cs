namespace NetCraft.Util.Random;

//带权重项对应原版net.minecraft.util.random.Weighted
//值与权重一对零或正权重不能为负
public sealed record Weighted<T>
{
    public T Value { get; }
    public int Weight { get; }

    //Weighted构造对应原版Weighted(T,int)
    //权重为负抛ArgumentException零权重警告由调用方处理
    public Weighted(T value, int weight)
    {
        if (weight < 0)
            throw new ArgumentException("Weight should be >= 0");
        Value = value;
        Weight = weight;
    }

    //map转换值类型保持权重对应原版map
    public Weighted<U> Map<U>(Func<T, U> function)
        => new(function(Value), Weight);

    //TODO Codec集成RecordCodecBuilder未就绪后补codec静态方法
    //TODO StreamCodec集成NetCraft.Network.StreamCodec扩展后补streamCodec静态方法
}
