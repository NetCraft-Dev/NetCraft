namespace NetCraft.Network;

//StreamCodec streaming codec interface, maps to vanilla net.minecraft.network.codec.StreamCodec
//B is the buffer type and V is the value type; encode/decode implement the actual coding
//B is marked in (contravariant) so StreamCodec<FriendlyByteBuf, V> implicitly converts to StreamCodec<RegistryFriendlyByteBuf, V>
public interface StreamCodec<in B, V>
{
    //decode deserializes from buffer to value
    V Decode(B buf);

    //encode serializes value into buffer
    void Encode(B buf, V value);
}

//IObjectEncodable non-generic codec interface supporting encoding as object
//common packets have the static type Packet<parent listener> and cannot be cast to Packet<child listener>, so the encode chain must pass them as object
internal interface IObjectEncodable
{
    //EncodeObject encodes a packet as object, extracting by its actual type inside the implementation
    void EncodeObject(RegistryFriendlyByteBuf buf, object packet);
}

//StreamCodecs static factory class providing basic codecs
public static class StreamCodecs
{
    //Bool bool codec reading a 1-byte boolean
    public static StreamCodec<T, bool> Bool<T>(Func<T, bool> reader, Action<T, bool> writer)
        where T : class
        => new FuncCodec<T, bool>(reader, writer);

    //Byte byte codec reading 1 byte
    public static StreamCodec<T, byte> Byte<T>(Func<T, byte> reader, Action<T, byte> writer)
        where T : class
        => new FuncCodec<T, byte>(reader, writer);

    //Int int codec, big-endian 4 bytes
    public static StreamCodec<T, int> Int<T>(Func<T, int> reader, Action<T, int> writer)
        where T : class
        => new FuncCodec<T, int>(reader, writer);

    //VarInt variable-length int codec
    public static StreamCodec<T, int> VarInt<T>(Func<T, int> reader, Action<T, int> writer)
        where T : class
        => new FuncCodec<T, int>(reader, writer);

    //Long long codec, big-endian 8 bytes
    public static StreamCodec<T, long> Long<T>(Func<T, long> reader, Action<T, long> writer)
        where T : class
        => new FuncCodec<T, long>(reader, writer);

    //String UTF-8 string codec with a leading length prefix
    public static StreamCodec<T, string> String<T>(Func<T, string> reader, Action<T, string> writer)
        where T : class
        => new FuncCodec<T, string>(reader, writer);
}

//FuncCodec functional codec implementation wrapping reader/writer delegates as a StreamCodec
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
