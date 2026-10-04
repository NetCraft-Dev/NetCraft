namespace NetCraft.Network.Protocol;

//UnitStreamCodec 恒定值编解码器对应原版 StreamCodec.unit
//解码返回固定实例编码校验值一致
//internal 跨 Protocol 子命名空间文件可见
internal sealed class UnitStreamCodec<B, V> : StreamCodec<B, V> where B : class
{
    private readonly V _instance;

    public UnitStreamCodec(V instance) => _instance = instance;

    public V Decode(B buf) => _instance;

    public void Encode(B buf, V value)
    {
        if (!EqualityComparer<V>.Default.Equals(value, _instance))
            throw new InvalidOperationException($"期望值 {_instance} 实际 {value}");
    }
}
