using NetCraft.Nbt;

namespace NetCraft.Registry.EntityAttribute;

//AttributeMap entity attribute map, maps to vanilla AttributeMap
//Instances are created lazily; attributes the entity never touched read directly from the type's default table
//The server also maintains "attributes changed this tick" for incremental sync, see AttributesToSync
public sealed class AttributeMap
{
    //AttributesTag attribute save field name, maps to vanilla LivingEntity.TAG_ATTRIBUTES
    private const string AttributesTag = "attributes";

    //_attributes the entity's own instances; only attributes that were read or changed land here, maps to vanilla attributes
    private readonly Dictionary<Attribute, AttributeInstance> _attributes = new();
    //_attributesToSync attributes changed this tick that need to sync to the client, maps to vanilla attributesToSync
    private readonly HashSet<AttributeInstance> _attributesToSync = new();
    //_supplier this entity type's default attribute table; attributes not registered for the type can never get an instance
    private readonly AttributeSupplier _supplier;

    public AttributeMap(AttributeSupplier supplier) => _supplier = supplier;

    //GetInstance gets the entity's own instance, copying one from the template if not built yet; returns null if the type lacks the attribute, maps to vanilla getInstance
    public AttributeInstance? GetInstance(Attribute attribute)
    {
        if (_attributes.TryGetValue(attribute, out var existing)) return existing;
        var created = _supplier.CreateInstance(attribute, OnAttributeModified);
        if (created is not null) _attributes[attribute] = created;
        return created;
    }

    //GetValue gets the attribute's final value; uses the entity's own instance if any, otherwise falls back to the type's default table, maps to vanilla getValue
    //If the default table lacks the attribute it falls back to the attribute's own default, so callers need not check hasAttribute first
    public double GetValue(Attribute attribute)
        => _attributes.TryGetValue(attribute, out var instance)
            ? instance.Value
            : SupplierHas(attribute) ? _supplier.GetValue(attribute) : attribute.DefaultValue;

    //GetBaseValue gets the attribute's base value, maps to vanilla getBaseValue
    public double GetBaseValue(Attribute attribute)
        => _attributes.TryGetValue(attribute, out var instance)
            ? instance.BaseValue
            : SupplierHas(attribute) ? _supplier.GetBaseValue(attribute) : attribute.DefaultValue;

    //ResetBaseValue restores the attribute base value to the type's default table, maps to vanilla resetBaseValue
    //Returns false if the type does not register the attribute; if no instance exists yet there is nothing to restore, so it succeeds, matching the supplier behavior
    public bool ResetBaseValue(Attribute attribute)
    {
        if (!SupplierHas(attribute)) return false;
        if (_attributes.TryGetValue(attribute, out var instance))
            instance.SetBaseValue(_supplier.GetBaseValue(attribute));
        return true;
    }

    //HasAttribute whether the entity has the attribute, maps to vanilla hasAttribute
    public bool HasAttribute(Attribute attribute)
        => _attributes.ContainsKey(attribute) || _supplier.HasAttribute(attribute);

    //SyncableAttributes all attributes that need to sync to the client, maps to vanilla getSyncableAttributes
    //Sent in full when the entity is paired; includes only instantiated attributes and untouched ones use the client's local default table
    public IReadOnlyList<AttributeInstance> SyncableAttributes
    {
        get
        {
            var result = new List<AttributeInstance>();
            foreach (var instance in _attributes.Values)
                if (instance.Attribute.ClientSyncable) result.Add(instance);
            return result;
        }
    }

    //AttributesToSync attributes changed this tick that need syncing, maps to vanilla getAttributesToSync
    public IReadOnlyCollection<AttributeInstance> AttributesToSync => _attributesToSync;

    //Pack packs all attribute instances for saving; includes only changed attributes, maps to vanilla pack
    //Untouched attributes take no save space and are read back from the type's default table
    public IReadOnlyList<AttributeInstance.Packed> Pack()
    {
        var result = new List<AttributeInstance.Packed>(_attributes.Count);
        foreach (var instance in _attributes.Values) result.Add(instance.Pack());
        return result;
    }

    //Apply applies attributes read back from a save, dropping entries for attributes the type lacks, maps to vanilla apply
    public void Apply(IReadOnlyList<AttributeInstance.Packed> packedAttributes)
    {
        foreach (var packed in packedAttributes)
        {
            var instance = GetInstance(packed.Attribute);
            instance?.Apply(packed);
        }
    }

    //WriteTo writes attributes into the entity save, the attributes field in vanilla LivingEntity saves
    //Attributes are written by registry name so registry order changes do not affect reading back; attributes not registered in the registry are skipped
    public void WriteTo(CompoundTag tag)
    {
        var list = new ListTag();
        foreach (var packed in Pack())
        {
            if (BuiltInRegistries.ATTRIBUTE.GetKey(packed.Attribute) is not { } id) continue;
            var entry = new CompoundTag();
            entry.PutString("id", id.ToString());
            //base is always written by vanilla; a missing value on read is treated as 0
            entry.PutDouble("base", packed.BaseValue);
            //modifiers is omitted when empty, as in vanilla
            if (packed.Modifiers.Count > 0)
            {
                var modifiers = new ListTag();
                foreach (var modifier in packed.Modifiers)
                    modifiers.Add(WriteModifier(modifier));
                entry.Put("modifiers", modifiers);
            }
            list.Add(entry);
        }
        tag.Put(AttributesTag, list);
    }

    //ReadFrom reads attributes back from the entity save, leaving the current map untouched if the field is absent
    //A single entry that fails to decode is dropped without discarding the rest, matching vanilla LivingEntity calling AttributeMap.apply after reading
    public void ReadFrom(CompoundTag tag)
    {
        if (tag.GetList(AttributesTag) is not { } list) return;
        var packed = new List<AttributeInstance.Packed>(list.Count);
        for (var i = 0; i < list.Count; i++)
            if (list.GetCompound(i) is { } entry && ReadAttribute(entry) is { } one) packed.Add(one);
        Apply(packed);
    }

    //WriteModifier writes a single modifier, maps to vanilla AttributeModifier.CODEC
    //operation uses the serialized name rather than the enum ordinal so saves do not depend on enum order
    private static CompoundTag WriteModifier(AttributeModifier modifier)
    {
        var tag = new CompoundTag();
        tag.PutString("id", modifier.Id.ToString());
        tag.PutDouble("amount", modifier.Amount);
        tag.PutString("operation", AttributeModifier.GetSerializedName(modifier.Operation));
        return tag;
    }

    //ReadAttribute reads a single attribute entry, dropping unregistered attributes
    private static AttributeInstance.Packed? ReadAttribute(CompoundTag tag)
    {
        if (Identifier.TryParse(tag.GetStringValue("id")) is not { } id) return null;
        if (BuiltInRegistries.ATTRIBUTE.GetValue(id) is not { } attribute) return null;
        var baseValue = tag.GetDouble("base")?.Value ?? 0.0;
        var modifiers = new List<AttributeModifier>();
        if (tag.GetList("modifiers") is { } list)
        {
            for (var i = 0; i < list.Count; i++)
                if (list.GetCompound(i) is { } entry && ReadModifier(entry) is { } modifier)
                    modifiers.Add(modifier);
        }
        return new AttributeInstance.Packed(attribute, baseValue, modifiers);
    }

    //ReadModifier reads a single modifier, dropping the entry if the operation name is unknown
    private static AttributeModifier? ReadModifier(CompoundTag tag)
    {
        if (Identifier.TryParse(tag.GetStringValue("id")) is not { } id) return null;
        if (AttributeModifier.TryFromName(tag.GetStringValue("operation")) is not { } operation) return null;
        return new AttributeModifier(id, tag.GetDouble("amount")?.Value ?? 0.0, operation);
    }

    //ClearAttributesToSync clears the pending sync set after syncing, matching the vanilla attributes.clear() at the send point
    public void ClearAttributesToSync() => _attributesToSync.Clear();

    //OnAttributeModified callback when an instance is marked dirty, maps to vanilla onAttributeModified
    //Only sync-marked attributes enter the set; a non-sync attribute should not be sent even if changed
    private void OnAttributeModified(AttributeInstance instance)
    {
        if (instance.Attribute.ClientSyncable) _attributesToSync.Add(instance);
    }

    //SupplierHas whether the type's default table has the attribute
    private bool SupplierHas(Attribute attribute) => _supplier.HasAttribute(attribute);
}
