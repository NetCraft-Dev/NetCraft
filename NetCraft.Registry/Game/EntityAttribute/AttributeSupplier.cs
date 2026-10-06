namespace NetCraft.Registry.EntityAttribute;

//AttributeSupplier per-entity-type default attribute table, maps to vanilla AttributeSupplier
//One template instance per attribute; entities copy from the template when built, and unregistered attributes can never be read
public sealed class AttributeSupplier
{
    //Empty empty table with no attributes registered, used by base entities and entities that take no attributes
    public static readonly AttributeSupplier Empty = Builder.Create().Build();

    private readonly Dictionary<Attribute, AttributeInstance> _instances;

    private AttributeSupplier(Dictionary<Attribute, AttributeInstance> instances) => _instances = instances;

    //CreateInstance copies the template into the entity's own instance; returns null if this type has not registered the attribute, maps to vanilla createInstance
    public AttributeInstance? CreateInstance(Attribute attribute, Action<AttributeInstance>? onDirty)
    {
        if (!_instances.TryGetValue(attribute, out var template)) return null;
        var instance = new AttributeInstance(attribute, onDirty);
        instance.ReplaceFrom(template);
        return instance;
    }

    //HasAttribute whether this type has registered an attribute, maps to vanilla hasAttribute
    public bool HasAttribute(Attribute attribute) => _instances.ContainsKey(attribute);

    //GetValue gets the value on the template, the fallback when the entity has no instance of its own, maps to vanilla getValue
    public double GetValue(Attribute attribute) => GetTemplate(attribute).Value;

    //GetBaseValue gets the base value on the template, maps to vanilla getBaseValue
    public double GetBaseValue(Attribute attribute) => GetTemplate(attribute).BaseValue;

    //GetTemplate gets the template instance; throws if the attribute is not registered, matching vanilla
    private AttributeInstance GetTemplate(Attribute attribute)
        => _instances.TryGetValue(attribute, out var template)
            ? template
            : throw new ArgumentException($"This entity type does not have attribute {attribute.DescriptionId}");

    //Builder attribute default table builder, maps to vanilla AttributeSupplier.Builder
    public sealed class Builder
    {
        private readonly Dictionary<Attribute, AttributeInstance> _instances = new();
        private bool _frozen;

        private Builder() { }

        public static Builder Create() => new();

        //Add registers an attribute with the base value taken from the attribute's own default
        public Builder Add(Attribute attribute)
        {
            Create(attribute);
            return this;
        }

        //Add registers an attribute and overrides the base value
        public Builder Add(Attribute attribute, double baseValue)
        {
            Create(attribute).SetBaseValue(baseValue);
            return this;
        }

        //Build freezes into a table; changing a template after freezing throws, matching vanilla Builder's instanceFrozen
        public AttributeSupplier Build()
        {
            _frozen = true;
            return new AttributeSupplier(new Dictionary<Attribute, AttributeInstance>(_instances));
        }

        //Create builds a template instance; any change after freezing throws in the callback
        private AttributeInstance Create(Attribute attribute)
        {
            var instance = new AttributeInstance(attribute, changed =>
            {
                if (_frozen)
                    throw new InvalidOperationException($"Attribute default table is frozen, cannot change {changed.Attribute.DescriptionId}");
            });
            _instances[attribute] = instance;
            return instance;
        }
    }
}
