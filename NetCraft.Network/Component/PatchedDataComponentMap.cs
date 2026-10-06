using NetCraft.Codec;
using NetCraft.Registry;
using System.Text;

namespace NetCraft.Network.Component;

//PatchedDataComponentMap readable component map that applies a patch, maps to vanilla net.minecraft.core.component.PatchedDataComponentMap
//prototype provides the base values, the patch overrides or removes them, and get checks the patch first before falling back to prototype
//keySet is the prototype set minus patch removals plus patch additions
//Mutations go through an immutable patch, rebuilt each time, equivalent to vanilla's copyOnWrite mutable patch
public sealed class PatchedDataComponentMap : DataComponentMap
{
    private readonly DataComponentMap _prototype;
    private DataComponentPatch _patch;

    public PatchedDataComponentMap(DataComponentMap prototype)
        : this(prototype, DataComponentPatch.Empty)
    {
    }

    public PatchedDataComponentMap(DataComponentMap prototype, DataComponentPatch patch)
    {
        _prototype = prototype;
        _patch = patch;
    }

    //FromPatch constructs from a prototype and patch, clearing patch entries equal to the prototype, maps to vanilla fromPatch
    public static PatchedDataComponentMap FromPatch(DataComponentMap prototype, DataComponentPatch patch)
    {
        var map = new PatchedDataComponentMap(prototype);
        map.ApplyPatch(patch);
        return map;
    }

    //Prototype base map
    public DataComponentMap Prototype => _prototype;

    //Patch the currently applied patch
    public DataComponentPatch Patch => _patch;

    //Get checks the patch first: present returns the value, empty returns null for removal, and absence falls back to prototype
    public T? Get<T>(DataComponentType<T> type) where T : class => _patch.GetFrom(_prototype, type);

    //HasNonDefault indicates the entry was explicitly changed in the patch, maps to vanilla hasNonDefault
    public bool HasNonDefault<T>(DataComponentType<T> type) where T : class => _patch.Get(type) is not null;

    //KeySet prototype set minus patch removals plus patch additions
    public IEnumerable<object> KeySet
    {
        get
        {
            var removed = new HashSet<object>();
            var added = new HashSet<object>();
            foreach (var kv in _patch.AsMap())
            {
                if (kv.Value.IsPresent)
                    added.Add(kv.Key);
                else
                    removed.Add(kv.Key);
            }
            foreach (var key in _prototype.KeySet)
            {
                if (!removed.Contains(key))
                    yield return key;
            }
            foreach (var key in added)
            {
                yield return key;
            }
        }
    }

    //Set overrides a component value; writing back the prototype's same value is equivalent to undoing it and returns the previous value, maps to vanilla set
    public T? Set<T>(DataComponentType<T> type, T value) where T : class
    {
        var previous = Get(type);
        var map = new Dictionary<object, Optional<object>>(_patch.AsMap());
        if (Equals(value, _prototype.Get(type))) map.Remove(type);
        else map[type] = Optional<object>.Of(value);
        _patch = map.Count == 0 ? DataComponentPatch.Empty : new DataComponentPatch(map);
        return previous;
    }

    //Set writes a typed component entry
    public T? Set<T>(TypedDataComponent<T> component) where T : class => Set(component.Type, component.Value);

    //Remove removes a component; when the prototype lacks it no trace is left in the patch, and the previous value is returned, maps to vanilla remove
    public T? Remove<T>(DataComponentType<T> type) where T : class
    {
        var previous = Get(type);
        var map = new Dictionary<object, Optional<object>>(_patch.AsMap());
        if (_prototype.Get(type) is not null) map[type] = Optional<object>.Empty();
        else map.Remove(type);
        _patch = map.Count == 0 ? DataComponentPatch.Empty : new DataComponentPatch(map);
        return previous;
    }

    //ApplyPatch applies the patch entry by entry; entries equal to the prototype are not kept in the patch, maps to vanilla applyPatch
    public void ApplyPatch(DataComponentPatch patch)
    {
        if (patch.IsEmpty) return;
        var map = new Dictionary<object, Optional<object>>(_patch.AsMap());
        foreach (var kv in patch.AsMap())
        {
            var prototypeValue = kv.Key is DataComponentType<object> type ? _prototype.Get(type) : null;
            if (kv.Value.IsPresent)
            {
                if (Equals(kv.Value.Get(), prototypeValue)) map.Remove(kv.Key);
                else map[kv.Key] = kv.Value;
            }
            else
            {
                if (prototypeValue is not null) map[kv.Key] = Optional<object>.Empty();
                else map.Remove(kv.Key);
            }
        }
        _patch = map.Count == 0 ? DataComponentPatch.Empty : new DataComponentPatch(map);
    }

    //RestorePatch discards the current patch and replaces it wholesale with the given patch, maps to vanilla restorePatch
    public void RestorePatch(DataComponentPatch patch) => _patch = patch;

    //ClearPatch clears the patch and returns to a pure prototype, maps to vanilla clearPatch
    public void ClearPatch() => _patch = DataComponentPatch.Empty;

    //SetAll overlays every component of another map one by one, maps to vanilla setAll
    public void SetAll(DataComponentMap components)
    {
        foreach (var key in components.KeySet)
            if (key is DataComponentType<object> type && components.Get(type) is { } value)
                Set(type, value);
    }

    //AsPatch returns the patch for ItemStack.STREAM_CODEC to encode
    public DataComponentPatch AsPatch() => _patch;

    //Copy duplicates this map; the patch is immutable and shared directly
    public PatchedDataComponentMap Copy() => new(_prototype, _patch);

    //ToImmutableMap without a patch the prototype is the result, maps to vanilla toImmutableMap
    public DataComponentMap ToImmutableMap() => _patch.IsEmpty ? _prototype : Copy();

    //Equality requires both prototype and patch to match, maps to vanilla equals
    public override bool Equals(object? obj)
        => ReferenceEquals(this, obj)
            || (obj is PatchedDataComponentMap other
                && Equals(_prototype, other._prototype)
                && _patch.Equals(other._patch));

    //The hash is consistent with equality
    public override int GetHashCode() => _prototype.GetHashCode() + (_patch.GetHashCode() * 31);

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append('{');
        var first = true;
        foreach (var key in KeySet)
        {
            if (!first) sb.Append(", ");
            first = false;
            if (key is DataComponentType<object> type && Get(type) is { } value)
                sb.Append(key).Append("=>").Append(value);
        }
        sb.Append('}');
        return sb.ToString();
    }
}
