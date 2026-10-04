namespace NetCraft.Network.Chat;

//MessageSignature 消息签名对应原版 net.minecraft.network.chat.MessageSignature
//固定 256 字节数组用 Read/write 静态方法编解码
public sealed class MessageSignature
{
    public const int Size = 256;

    public byte[] Bytes { get; }

    public MessageSignature(byte[] bytes)
    {
        if (bytes.Length != Size)
            throw new ArgumentException($"Invalid message signature size: {bytes.Length}");
        Bytes = bytes;
    }

    //Read 从 buf 读 256 字节构造 MessageSignature 对齐原版 read
    public static MessageSignature Read(FriendlyByteBuf input)
    {
        var bytes = input.ReadBytes(Size);
        return new MessageSignature(bytes);
    }

    //Write 把 256 字节写入 buf 对齐原版 write
    public static void Write(FriendlyByteBuf output, MessageSignature signature)
        => output.WriteBytes(signature.Bytes);

    //Describe 返回签名描述 null 返回 no signature 对齐原版 describe
    public static string Describe(MessageSignature? signature)
        => signature is null ? "<no signature>" : Convert.ToBase64String(signature.Bytes);

    public override bool Equals(object? obj)
        => obj is MessageSignature that && Bytes.SequenceEqual(that.Bytes);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.AddBytes(Bytes);
        return hash.ToHashCode();
    }

    public override string ToString() => Convert.ToBase64String(Bytes);

    //Packed 紧凑形式 id==-1 表示 full signature 否则用 cache id 对齐原版 MessageSignature.Packed
    //FULL_SIGNATURE = -1 编解码 VarInt id + 1 若 id==-1 后跟完整 256 字节签名
    public sealed class Packed
    {
        public const int FullSignatureId = -1;

        public int Id { get; }
        public MessageSignature? FullSignature { get; }

        public Packed(int id, MessageSignature? fullSignature)
        {
            Id = id;
            FullSignature = fullSignature;
        }

        public Packed(MessageSignature signature) : this(FullSignatureId, signature) { }

        public Packed(int id) : this(id, null) { }

        //Read 从 buf 读 Packed VarInt id+1 若 id==-1 后跟完整签名
        public static Packed Read(FriendlyByteBuf input)
        {
            int id = input.ReadVarInt() - 1;
            if (id == FullSignatureId)
                return new Packed(MessageSignature.Read(input));
            return new Packed(id);
        }

        //Write 把 Packed 写入 buf VarInt id+1 若有完整签名后跟 256 字节
        public static void Write(FriendlyByteBuf output, Packed packed)
        {
            output.WriteVarInt(packed.Id + 1);
            if (packed.FullSignature is not null)
                MessageSignature.Write(output, packed.FullSignature);
        }
    }
}
