namespace NetCraft.Registry.EntityAttribute;

//AttributeInstance attribute instance, maps to vanilla AttributeInstance
//Holds the base value and all modifiers on the attribute; value reads go through a dirty-flag cache and modifier changes notify the owner
public sealed class AttributeInstance
{
    //_modifiersByOperation modifiers grouped by operation, iterated per group when computing, maps to vanilla modifiersByOperation
    private readonly Dictionary<AttributeOperation, Dictionary<Identifier, AttributeModifier>> _modifiersByOperation = new();
    //_modifierById all modifiers indexed by id, used for deduplication and lookup, maps to vanilla modifierById
    private readonly Dictionary<Identifier, AttributeModifier> _modifierById = new();
    private readonly Action<AttributeInstance>? _onDirty;
    private double _baseValue;
    private double _cachedValue;
    private bool _dirty = true;

    public AttributeInstance(Attribute attribute, Action<AttributeInstance>? onDirty)
    {
        Attribute = attribute;
        _onDirty = onDirty;
        _baseValue = attribute.DefaultValue;
    }

    //Attribute owning attribute, maps to vanilla getAttribute
    public Attribute Attribute { get; }

    //BaseValue base value, maps to vanilla getBaseValue
    public double BaseValue => _baseValue;

    //SetBaseValue changes the base value; no dirty flag when unchanged, maps to vanilla setBaseValue
    public void SetBaseValue(double value)
    {
        if (value == _baseValue) return;
        _baseValue = value;
        SetDirty();
    }

    //Modifiers snapshot of all modifiers, maps to vanilla getModifiers
    public IReadOnlyCollection<AttributeModifier> Modifiers => new List<AttributeModifier>(_modifierById.Values);

    //GetModifier gets a modifier by id, returning null if absent, maps to vanilla getModifier
    public AttributeModifier? GetModifier(Identifier id) => _modifierById.GetValueOrDefault(id);

    //HasModifier whether the attribute already has a modifier with the given id, maps to vanilla hasModifier
    public bool HasModifier(Identifier id) => _modifierById.ContainsKey(id);

    //AddTransientModifier adds a transient modifier; an existing same id throws immediately, maps to vanilla addTransientModifier
    public void AddTransientModifier(AttributeModifier modifier)
    {
        if (_modifierById.ContainsKey(modifier.Id))
            throw new ArgumentException($"The attribute already has a modifier with id={modifier.Id}");
        _modifierById[modifier.Id] = modifier;
        GetOrCreateModifiersIn(modifier.Operation)[modifier.Id] = modifier;
        SetDirty();
    }

    //RemoveModifier removes a modifier by id, returning false if absent, maps to vanilla removeModifier
    public bool RemoveModifier(Identifier id)
    {
        if (!_modifierById.Remove(id, out var modifier)) return false;
        if (_modifiersByOperation.TryGetValue(modifier.Operation, out var byId)) byId.Remove(id);
        SetDirty();
        return true;
    }

    //Value final value, recomputed only when dirty, maps to vanilla getValue
    public double Value
    {
        get
        {
            if (_dirty)
            {
                _cachedValue = CalculateValue();
                _dirty = false;
            }
            return _cachedValue;
        }
    }

    //ReplaceFrom copies another instance's base value and modifiers wholesale, maps to vanilla replaceFrom
    //The attribute type's default table copies new instances out of a template instance like this
    public void ReplaceFrom(AttributeInstance other)
    {
        _baseValue = other._baseValue;
        _modifierById.Clear();
        foreach (var (id, modifier) in other._modifierById) _modifierById[id] = modifier;
        _modifiersByOperation.Clear();
        foreach (var (operation, byId) in other._modifiersByOperation)
        {
            var target = GetOrCreateModifiersIn(operation);
            foreach (var (id, modifier) in byId) target[id] = modifier;
        }
        SetDirty();
    }

    //Pack packs into the save form with the base value and all modifiers, maps to vanilla pack
    public Packed Pack()
        => new(Attribute, BaseValue, new List<AttributeModifier>(_modifierById.Values));

    //Apply restores from the save form, overriding the base value and installing modifiers by id, maps to vanilla apply
    //Vanilla stores only permanent modifiers; this port has no transient modifier source yet, so all modifiers are treated as permanent
    public void Apply(Packed packed)
    {
        SetBaseValue(packed.BaseValue);
        foreach (var modifier in packed.Modifiers)
        {
            RemoveModifier(modifier.Id);
            AddTransientModifier(modifier);
        }
    }

    //Packed save form of an attribute instance, maps to vanilla AttributeInstance.Packed
    //modifiers are permanent modifiers under vanilla semantics, matching all modifiers here
    public sealed record Packed(Attribute Attribute, double BaseValue, IReadOnlyList<AttributeModifier> Modifiers);

    //CalculateValue computes the value in vanilla's three-stage order, which cannot be reordered
    //First add all add_value to the base value, then compute add_multiplied_base on the updated base value, and finally multiply add_multiplied_total one by one
    private double CalculateValue()
    {
        var baseValue = BaseValue;
        foreach (var modifier in ModifiersIn(AttributeOperation.AddValue))
            baseValue += modifier.Amount;
        var result = baseValue;
        foreach (var modifier in ModifiersIn(AttributeOperation.AddMultipliedBase))
            result += baseValue * modifier.Amount;
        foreach (var modifier in ModifiersIn(AttributeOperation.AddMultipliedTotal))
            result *= 1.0 + modifier.Amount;
        return Attribute.SanitizeValue(result);
    }

    //SetDirty marks dirty and notifies the owner, maps to vanilla setDirty
    private void SetDirty()
    {
        _dirty = true;
        _onDirty?.Invoke(this);
    }

    //ModifiersIn gets the modifiers under an operation, returning empty if never registered
    private IReadOnlyCollection<AttributeModifier> ModifiersIn(AttributeOperation operation)
        => _modifiersByOperation.TryGetValue(operation, out var byId)
            ? byId.Values
            : Array.Empty<AttributeModifier>();

    //GetOrCreateModifiersIn gets the modifier table under an operation, creating one if absent
    private Dictionary<Identifier, AttributeModifier> GetOrCreateModifiersIn(AttributeOperation operation)
    {
        if (_modifiersByOperation.TryGetValue(operation, out var byId)) return byId;
        byId = new Dictionary<Identifier, AttributeModifier>();
        _modifiersByOperation[operation] = byId;
        return byId;
    }
}
