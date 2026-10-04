namespace NetCraft.Network;

//StreamCodec 流式编解码器接口对应原版 net.minecraft.network.codec.StreamCodec
//B 是 buffer 类型V 是 value 类型encode/decode 实现具体编解码
//B 标记 in 逆变让 StreamCodec<FriendlyByteBuf, V> 可隐式转 StreamCodec<RegistryFriendlyByteBuf, V>
public interface StreamCodec<in B, V>
{
    //decode 从 buffer 反序列化为 value
    V Decode(B buf);

    //encode 把 value 序列化到 buffer
    void Encode(B buf, V value);
}

//IObjectEncodable 支持以 object 编码的非泛型编解码器接口
//common 包静态类型是 Packet<父监听器> 无法强转 Packet<子监听器> 编码链需按 object 传递
internal interface IObjectEncodable
{
    //EncodeObject 以 object 形式编码包实现内部按实际类型提取
    void EncodeObject(RegistryFriendlyByteBuf buf, object packet);
}

//StreamCodecs 静态工厂类提供基础编解码器
public static class StreamCodecs
{
    //Bool bool 编解码器读 1 字节布尔
    public static StreamCodec<T, bool> Bool<T>(Func<T, bool> reader, Action<T, bool> writer)
        where T : class
        => new FuncCodec<T, bool>(reader, writer);

    //Byte byte 编解码器读 1 字节
    public static StreamCodec<T, byte> Byte<T>(Func<T, byte> reader, Action<T, byte> writer)
        where T : class
        => new FuncCodec<T, byte>(reader, writer);

    //Int int 编解码器大端 4 字节
    public static StreamCodec<T, int> Int<T>(Func<T, int> reader, Action<T, int> writer)
        where T : class
        => new FuncCodec<T, int>(reader, writer);

    //VarInt 可变长度 int 编解码器
    public static StreamCodec<T, int> VarInt<T>(Func<T, int> reader, Action<T, int> writer)
        where T : class
        => new FuncCodec<T, int>(reader, writer);

    //Long long 编解码器大端 8 字节
    public static StreamCodec<T, long> Long<T>(Func<T, long> reader, Action<T, long> writer)
        where T : class
        => new FuncCodec<T, long>(reader, writer);

    //String UTF-8 字符串编解码器前置长度前缀
    public static StreamCodec<T, string> String<T>(Func<T, string> reader, Action<T, string> writer)
        where T : class
        => new FuncCodec<T, string>(reader, writer);
}

//FuncCodec 函数式编解码器实现把 reader/writer 委托包装为 StreamCodec
internal sealed class FuncCodec<B, V> : StreamCodec<B, V> where B : class
{
    private readonly Func<B, V> _reader;
    private readonly Action<B, V> _writer;

    public FuncCodec(Func<B, V> reader, Action<B, V> writer)
    {
        _reader = reader;
        _writer = writer;
    }

    public V Decode(B buf) => _reader(buf);
    public void Encode(B buf, V value) => _writer(buf, value);
}
