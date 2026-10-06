using NetCraft.Registry;

namespace NetCraft.Network;

//ByteBufCodecs stream codec utilities, maps to vanilla net.minecraft.network.codec.ByteBufCodecs
//Provides base StreamCodec factories such as registry Holder codecs and collection codecs
public static class ByteBufCodecs
{
    //Cap on the initial collection capacity to avoid preallocating huge memory on malicious large length prefixes
    public const int MaxInitialCollectionSize = 65536;

    //Holder codec reads an id from RegistryFriendlyByteBuf and turns it into a Holder.Reference
    //registryKey identifies the target registry; encode looks up the id by value and decode takes the Reference by id
    public static StreamCodec<RegistryFriendlyByteBuf, Holder<T>> Holder<T>(ResourceKey<Registry<T>> registryKey) where T : class
        => new HolderStreamCodec<T>(registryKey);

    //Holder codec with directCodec supports a wrapped Direct value: id==0 goes through directCodec, otherwise id-1 goes through the registry Reference
    //Maps to vanilla ByteBufCodecs.holder(registryKey, directCodec)
    public static StreamCodec<RegistryFriendlyByteBuf, Holder<T>> Holder<T>(
        ResourceKey<Registry<T>> registryKey,
        StreamCodec<RegistryFriendlyByteBuf, T> directCodec) where T : class
        => new DirectHolderStreamCodec<T>(registryKey, directCodec);

    //Collection codec: VarInt length prefix + element list
    //elementCodec is the per-element codec; maxSize is the length cap check
    public static StreamCodec<B, List<V>> Collection<B, V>(StreamCodec<B, V> elementCodec, int maxSize = int.MaxValue) where B : class
        => new CollectionStreamCodec<B, V>(elementCodec, maxSize);

    //StringUtf8 codec for UTF-8 strings with a variable-length prefix; maxLength is the length cap check
    public static StreamCodec<RegistryFriendlyByteBuf, string> StringUtf8(int maxLength = 32767)
        => new StringStreamCodec(maxLength);

    //Identifier codec for namespace identifiers, maps to Identifier in vanilla ByteBufCodecs
    public static StreamCodec<RegistryFriendlyByteBuf, Identifier> Identifier()
        => new IdentifierStreamCodec();
}

//IdentifierStreamCodec codec for namespace identifiers, encoded as a namespace:path string
internal sealed class IdentifierStreamCodec : StreamCodec<RegistryFriendlyByteBuf, Identifier>
{
    public Identifier Decode(RegistryFriendlyByteBuf buf) => buf.ReadIdentifier();

    public void Encode(RegistryFriendlyByteBuf buf, Identifier value) => buf.WriteIdentifier(value);
}

//StringStreamCodec string codec, maps to vanilla ByteBufCodecs.stringUtf8
internal sealed class StringStreamCodec : StreamCodec<RegistryFriendlyByteBuf, string>
{
    private readonly int _maxLength;

    public StringStreamCodec(int maxLength) => _maxLength = maxLength;

    public string Decode(RegistryFriendlyByteBuf buf) => buf.ReadString(_maxLength);

    public void Encode(RegistryFriendlyByteBuf buf, string value) => buf.WriteString(value, _maxLength);
}

//HolderStreamCodec bidirectional codec between registry id and Holder.Reference
internal sealed class HolderStreamCodec<T> : StreamCodec<RegistryFriendlyByteBuf, Holder<T>> where T : class
{
    private readonly ResourceKey<Registry<T>> _registryKey;

    public HolderStreamCodec(ResourceKey<Registry<T>> registryKey)
    {
        _registryKey = registryKey;
    }

    public Holder<T> Decode(RegistryFriendlyByteBuf buf)
    {
        int id = buf.ReadVarInt();
        var registry = buf.Lookup(_registryKey);
        var holder = registry.Get(id);
        if (holder is null)
            throw new InvalidOperationException($"unknown holder id {id} in {_registryKey}");
        return holder;
    }

    public void Encode(RegistryFriendlyByteBuf buf, Holder<T> value)
    {
        if (value is not Reference<T> reference)
            throw new InvalidOperationException($"cannot encode non-Reference holder: {value}");
        var registry = buf.Lookup(_registryKey);
        int id = registry.GetId(reference.Value);
        if (id == IdMap<T>.Default)
            throw new InvalidOperationException($"holder value not registered: {reference.Value}");
        buf.WriteVarInt(id);
    }
}

//DirectHolderStreamCodec mixed codec for registry id + wrapped Direct value, maps to vanilla ByteBufCodecs.holder(registryKey, directCodec)
//DIRECT_HOLDER_ID = 0 is reserved for Direct; Reference writes id+1, and on decode id==0 goes through directCodec, otherwise id-1 looks up the registry
internal sealed class DirectHolderStreamCodec<T> : StreamCodec<RegistryFriendlyByteBuf, Holder<T>> where T : class
{
    private const int DirectHolderId = 0;

    private readonly ResourceKey<Registry<T>> _registryKey;
    private readonly StreamCodec<RegistryFriendlyByteBuf, T> _directCodec;

    public DirectHolderStreamCodec(ResourceKey<Registry<T>> registryKey, StreamCodec<RegistryFriendlyByteBuf, T> directCodec)
    {
        _registryKey = registryKey;
        _directCodec = directCodec;
    }

    public Holder<T> Decode(RegistryFriendlyByteBuf buf)
    {
        int id = buf.ReadVarInt();
        if (id == DirectHolderId)
            return Holder<T>.Direct(_directCodec.Decode(buf));
        var registry = buf.Lookup(_registryKey);
        var holder = registry.Get(id - 1);
        if (holder is null)
            throw new InvalidOperationException($"unknown holder id {id} in {_registryKey}");
        return holder;
    }

    public void Encode(RegistryFriendlyByteBuf buf, Holder<T> value)
    {
        if (value.HolderKind == Holder<T>.Kind.Reference)
        {
            var registry = buf.Lookup(_registryKey);
            int id = registry.GetId(value.Value);
            if (id == IdMap<T>.Default)
                throw new InvalidOperationException($"holder value not registered: {value.Value}");
            buf.WriteVarInt(id + 1);
        }
        else
        {
            buf.WriteVarInt(DirectHolderId);
            _directCodec.Encode(buf, value.Value);
        }
    }
}

//CollectionStreamCodec collection codec with a VarInt length prefix, encoding and decoding element by element
internal sealed class CollectionStreamCodec<B, V> : StreamCodec<B, List<V>> where B : class
{
    private readonly StreamCodec<B, V> _elementCodec;
    private readonly int _maxSize;

    public CollectionStreamCodec(StreamCodec<B, V> elementCodec, int maxSize)
    {
        _elementCodec = elementCodec;
        _maxSize = maxSize;
    }

    public List<V> Decode(B buf)
    {
        int size = ReadVarInt(buf);
        if (size > _maxSize)
            throw new InvalidOperationException($"collection length out of range {size} > {_maxSize}");
        var list = new List<V>(Math.Min(size, ByteBufCodecs.MaxInitialCollectionSize));
        for (int i = 0; i < size; i++)
            list.Add(_elementCodec.Decode(buf));
        return list;
    }

    public void Encode(B buf, List<V> value)
    {
        if (value.Count > _maxSize)
            throw new InvalidOperationException($"collection length out of range {value.Count} > {_maxSize}");
        WriteVarInt(buf, value.Count);
        foreach (var item in value)
            _elementCodec.Encode(buf, item);
    }

    //ReadVarInt reads a VarInt from FriendlyByteBuf via reflection to avoid binding type B to FriendlyByteBuf
    //In practice B is always a FriendlyByteBuf subclass, so its ReadVarInt method is called
    private static int ReadVarInt(B buf)
    {
        if (buf is FriendlyByteBuf fbb) return fbb.ReadVarInt();
        throw new InvalidOperationException($"unsupported buffer type {buf?.GetType()}");
    }

    private static void WriteVarInt(B buf, int value)
    {
        if (buf is FriendlyByteBuf fbb) { fbb.WriteVarInt(value); return; }
        throw new InvalidOperationException($"unsupported buffer type {buf?.GetType()}");
    }
}
