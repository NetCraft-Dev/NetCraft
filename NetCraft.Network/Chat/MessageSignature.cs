namespace NetCraft.Network.Chat;

//MessageSignature message signature, maps to vanilla net.minecraft.network.chat.MessageSignature
//A fixed 256-byte array, coded by the Read/write static methods
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

    //Read reads 256 bytes from buf to build a MessageSignature, aligns with vanilla read
    public static MessageSignature Read(FriendlyByteBuf input)
    {
        var bytes = input.ReadBytes(Size);
        return new MessageSignature(bytes);
    }

    //Write writes the 256 bytes into buf, aligns with vanilla write
    public static void Write(FriendlyByteBuf output, MessageSignature signature)
        => output.WriteBytes(signature.Bytes);

    //Describe returns the signature description, or "no signature" when null, aligns with vanilla describe
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

    //Packed compact form: id==-1 means full signature, otherwise a cache id is used, aligns with vanilla MessageSignature.Packed
    //FULL_SIGNATURE = -1; codes a VarInt id + 1, and when id==-1 a full 256-byte signature follows
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

        //Read reads Packed from buf: VarInt id+1, and when id==-1 a full signature follows
        public static Packed Read(FriendlyByteBuf input)
        {
            int id = input.ReadVarInt() - 1;
            if (id == FullSignatureId)
                return new Packed(MessageSignature.Read(input));
            return new Packed(id);
        }

        //Write writes Packed into buf: VarInt id+1, and when a full signature exists 256 bytes follow
        public static void Write(FriendlyByteBuf output, Packed packed)
        {
            output.WriteVarInt(packed.Id + 1);
            if (packed.FullSignature is not null)
                MessageSignature.Write(output, packed.FullSignature);
        }
    }
}
