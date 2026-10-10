using NetCraft.Codec;
using NetCraft.Registry;
using System.Text;

namespace NetCraft.Game.World.Items.Component;

//DataComponentPatch data component patch, maps to vanilla net.minecraft.core.component.DataComponentPatch
//Stores a map from DataComponentType to Optional; present means an added value, empty means removal
//STREAM_CODEC encodes a positiveCount+negativeCount prefix; positive entries include type+value, negative entries only type
//DELIMITED_STREAM_CODEC used for length-prefix limits on untrusted sources
public sealed class DataComponentPatch
{
    //Empty empty patch singleton
    public static readonly DataComponentPatch Empty = new(new Dictionary<object, Optional<object>>());

    //PersistentCodec persistence codec, maps to vanilla DataComponentPatch.CODEC
    public static readonly Codec<DataComponentPatch> PersistentCodec = new DataComponentPatchMapCodec();

    //StreamCodec network sync codec
    public static readonly StreamCodec<RegistryFriendlyByteBuf, DataComponentPatch> StreamCodec
        = new DataComponentPatchStreamCodec();

    //DelimitedStreamCodec codec for untrusted sources with length-prefix limits
    public static readonly StreamCodec<RegistryFriendlyByteBuf, DataComponentPatch> DelimitedStreamCodec
        = new DataComponentPatchStreamCodec();

    private readonly Dictionary<object, Optional<object>> _map;

    public DataComponentPatch(Dictionary<object, Optional<object>> map)
    {
        _map = map;
    }

    //IsEmpty indicates whether it is an empty patch
    public bool IsEmpty => _map.Count == 0;

    //Get takes Optional by type: present means an added value, empty means removal, and absence returns null
    public Optional<object>? Get<T>(DataComponentType<T> type) where T : class
        => _map.TryGetValue(type, out var value) ? value : null;

    //AsMap returns a copy of the internal map
    public IReadOnlyDictionary<object, Optional<object>> AsMap() => _map.ToDictionary(kv => kv.Key, kv => kv.Value);

    //Size is the number of patch entries
    public int Size => _map.Count;

    //EntrySet is the set of patch entries
    public IEnumerable<KeyValuePair<object, Optional<object>>> EntrySet => _map;

    //GetFrom gets the final value after patch override; when the patch has the type it wins, otherwise it falls back to prototype
    //Maps to vanilla DataComponentPatch.get
    public T? GetFrom<T>(DataComponentGetter prototype, DataComponentType<T> type) where T : class
    {
        if (_map.TryGetValue(type, out var value))
            return value.IsPresent ? (T)value.Get() : null;
        return prototype.Get(type);
    }

    //Forget discards matching patch entries, maps to vanilla forget
    public DataComponentPatch Forget(Func<object, bool> test)
    {
        if (IsEmpty) return Empty;
        var copy = new Dictionary<object, Optional<object>>();
        foreach (var kv in _map)
            if (!test(kv.Key)) copy[kv.Key] = kv.Value;
        return copy.Count == 0 ? Empty : new DataComponentPatch(copy);
    }

    //Split splits into an added map and a removed set, maps to vanilla split
    public SplitResult Split()
    {
        if (IsEmpty) return SplitResult.Empty;
        var builder = new DataComponentMapBuilder();
        var removed = new HashSet<object>();
        foreach (var kv in _map)
        {
            if (kv.Value.IsPresent) builder.SetUnchecked(kv.Key, kv.Value.Get());
            else removed.Add(kv.Key);
        }
        return new SplitResult(builder.Build(), removed);
    }

    //NewBuilder creates a patch builder, maps to vanilla DataComponentPatch.builder
    public static Builder NewBuilder() => new();

    //Equality compares patch content entry by entry, maps to vanilla equals
    public override bool Equals(object? obj)
        => ReferenceEquals(this, obj) || (obj is DataComponentPatch other && MapEquals(other));

    private bool MapEquals(DataComponentPatch other)
    {
        if (other._map.Count != _map.Count) return false;
        foreach (var kv in _map)
        {
            if (!other._map.TryGetValue(kv.Key, out var value)) return false;
            if (kv.Value.IsPresent != value.IsPresent) return false;
            if (kv.Value.IsPresent && !Equals(kv.Value.Get(), value.Get())) return false;
        }
        return true;
    }

    //The hash is consistent with equality
    public override int GetHashCode()
    {
        var hash = 0;
        foreach (var kv in _map)
            hash += kv.Key.GetHashCode() ^ kv.Value.GetHashCode();
        return hash;
    }

    //Removed entries carry a ! prefix, maps to vanilla toString
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append('{');
        var first = true;
        foreach (var kv in _map)
        {
            if (!first) sb.Append(", ");
            first = false;
            if (kv.Value.IsPresent) sb.Append(kv.Key).Append("=>").Append(kv.Value.Get());
            else sb.Append('!').Append(kv.Key);
        }
        sb.Append('}');
        return sb.ToString();
    }

    //SplitResult patch split result, maps to vanilla DataComponentPatch.SplitResult
    public sealed record SplitResult(DataComponentMap Added, IReadOnlySet<object> Removed)
    {
        public static readonly SplitResult Empty = new(DataComponentMap.Empty, new HashSet<object>());
    }

    //Builder patch builder, maps to vanilla DataComponentPatch.Builder
    public sealed class Builder
    {
        private readonly Dictionary<object, Optional<object>> _map = new();

        //Set writes or overrides the entry
        public Builder Set<T>(DataComponentType<T> type, T value) where T : class
        {
            _map[type] = Optional<object>.Of(value);
            return this;
        }

        //Remove marks the entry for removal
        public Builder Remove<T>(DataComponentType<T> type) where T : class
        {
            _map[type] = Optional<object>.Empty();
            return this;
        }

        //Set writes a typed component entry
        public Builder Set<T>(TypedDataComponent<T> component) where T : class => Set(component.Type, component.Value);

        //Build returns the singleton for an empty patch
        public DataComponentPatch Build()
            => _map.Count == 0 ? Empty : new DataComponentPatch(new Dictionary<object, Optional<object>>(_map));
    }
}

//DataComponentPatchStreamCodec DataComponentPatch network codec implementation
internal sealed class DataComponentPatchStreamCodec : StreamCodec<RegistryFriendlyByteBuf, DataComponentPatch>
{
    public DataComponentPatch Decode(RegistryFriendlyByteBuf buf)
    {
        int positiveCount = buf.ReadVarInt();
        int negativeCount = buf.ReadVarInt();
        if (positiveCount == 0 && negativeCount == 0)
            return DataComponentPatch.Empty;

        int expectedSize = positiveCount + negativeCount;
        var map = new Dictionary<object, Optional<object>>(Math.Min(expectedSize, ByteBufCodecs.MaxInitialCollectionSize));
        for (int i = 0; i < positiveCount; i++)
        {
            var type = DataComponentTypeCodecs.Decode(buf);
            var codec = (IDataComponentTypeCodec)type;
            var value = codec.DecodeValue(buf);
            map[type] = Optional<object>.Of(value);
        }
        for (int i = 0; i < negativeCount; i++)
        {
            var type = DataComponentTypeCodecs.Decode(buf);
            map[type] = Optional<object>.Empty();
        }
        return new DataComponentPatch(map);
    }

    public void Encode(RegistryFriendlyByteBuf buf, DataComponentPatch value)
    {
        int positiveCount = 0;
        int negativeCount = 0;
        foreach (var kv in value.AsMap())
        {
            if (kv.Value.IsPresent) positiveCount++;
            else negativeCount++;
        }
        buf.WriteVarInt(positiveCount);
        buf.WriteVarInt(negativeCount);
        foreach (var kv in value.AsMap())
        {
            if (kv.Value.IsPresent)
            {
                DataComponentTypeCodecs.Encode(buf, kv.Key);
                ((IDataComponentTypeCodec)kv.Key).EncodeValue(buf, kv.Value.Get());
            }
        }
        foreach (var kv in value.AsMap())
        {
            if (!kv.Value.IsPresent)
            {
                DataComponentTypeCodecs.Encode(buf, kv.Key);
            }
        }
    }
}

//DataComponentTypeCodecs DataComponentType id codec utilities
//On encode a type instance looks up the registry via GetId and writes a VarInt; on decode an id is read and the instance taken from the registry
public static class DataComponentTypeCodecs
{
    //Encode looks up the id by type instance and writes a VarInt
    public static void Encode(RegistryFriendlyByteBuf buf, object type)
    {
        var registry = buf.Lookup(Registries.DATA_COMPONENT_TYPE);
        int id = registry.GetId(type);
        if (id == IdMap<object>.Default)
            throw new InvalidOperationException($"DataComponentType not registered: {type}");
        buf.WriteVarInt(id);
    }

    //Decode reads a VarInt id and takes the DataComponentType instance from the registry
    public static object Decode(RegistryFriendlyByteBuf buf)
    {
        int id = buf.ReadVarInt();
        var registry = buf.Lookup(Registries.DATA_COMPONENT_TYPE);
        var type = registry.ById(id);
        if (type is null)
            throw new InvalidOperationException($"unknown DataComponentType id {id}");
        return type;
    }
}
