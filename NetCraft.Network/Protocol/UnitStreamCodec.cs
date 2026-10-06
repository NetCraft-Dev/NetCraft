namespace NetCraft.Network.Protocol;

//UnitStreamCodec constant-value codec, maps to vanilla StreamCodec.unit
//Decode returns the fixed instance and encode verifies the value matches
//internal, visible across files in the Protocol sub-namespace
internal sealed class UnitStreamCodec<B, V> : StreamCodec<B, V> where B : class
{
    private readonly V _instance;

    public UnitStreamCodec(V instance) => _instance = instance;

    public V Decode(B buf) => _instance;

    public void Encode(B buf, V value)
    {
        if (!EqualityComparer<V>.Default.Equals(value, _instance))
            throw new InvalidOperationException($"expected value {_instance} actual {value}");
    }
}
