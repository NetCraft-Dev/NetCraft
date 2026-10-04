namespace NetCraft.Registry.EntityAttribute;

//AttributeInstance 属性实例 对应原版 AttributeInstance
//持基值与该属性上的全部修饰符 取值走脏标志缓存 修饰符变动时回调持有者
public sealed class AttributeInstance
{
    //_modifiersByOperation 修饰符按运算分组 算值时按组遍历 对应原版 modifiersByOperation
    private readonly Dictionary<AttributeOperation, Dictionary<Identifier, AttributeModifier>> _modifiersByOperation = new();
    //_modifierById 全部修饰符按 id 索引 判重与查找走它 对应原版 modifierById
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

    //Attribute 所属属性 对应原版 getAttribute
    public Attribute Attribute { get; }

    //BaseValue 基值 对应原版 getBaseValue
    public double BaseValue => _baseValue;

    //SetBaseValue 改基值 值相同就不动脏标记 对应原版 setBaseValue
    public void SetBaseValue(double value)
    {
        if (value == _baseValue) return;
        _baseValue = value;
        SetDirty();
    }

    //Modifiers 全部修饰符快照 对应原版 getModifiers
    public IReadOnlyCollection<AttributeModifier> Modifiers => new List<AttributeModifier>(_modifierById.Values);

    //GetModifier 按 id 取修饰符 没有该修饰符返回 null 对应原版 getModifier
    public AttributeModifier? GetModifier(Identifier id) => _modifierById.GetValueOrDefault(id);

    //HasModifier 该属性上是否已有指定 id 的修饰符 对应原版 hasModifier
    public bool HasModifier(Identifier id) => _modifierById.ContainsKey(id);

    //AddTransientModifier 加临时修饰符 同 id 已存在直接抛 对应原版 addTransientModifier
    public void AddTransientModifier(AttributeModifier modifier)
    {
        if (_modifierById.ContainsKey(modifier.Id))
            throw new ArgumentException($"该属性上已有 id={modifier.Id} 的修饰符");
        _modifierById[modifier.Id] = modifier;
        GetOrCreateModifiersIn(modifier.Operation)[modifier.Id] = modifier;
        SetDirty();
    }

    //RemoveModifier 按 id 移除修饰符 没有该修饰符返回 false 对应原版 removeModifier
    public bool RemoveModifier(Identifier id)
    {
        if (!_modifierById.Remove(id, out var modifier)) return false;
        if (_modifiersByOperation.TryGetValue(modifier.Operation, out var byId)) byId.Remove(id);
        SetDirty();
        return true;
    }

    //Value 最终取值 只有脏了才重算 对应原版 getValue
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

    //ReplaceFrom 整份复制另一个实例的基值与修饰符 对应原版 replaceFrom
    //属性类型的默认表就是以模板实例复制出新实例的
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

    //Pack 打包成存档形态 带上基值与全部修饰符 对应原版 pack
    public Packed Pack()
        => new(Attribute, BaseValue, new List<AttributeModifier>(_modifierById.Values));

    //Apply 从存档形态还原 基值覆盖 修饰符按 id 覆盖式装上 对应原版 apply
    //原版只存永久修饰符 本作暂时没有临时修饰符来源 全部修饰符都当永久处理
    public void Apply(Packed packed)
    {
        SetBaseValue(packed.BaseValue);
        foreach (var modifier in packed.Modifiers)
        {
            RemoveModifier(modifier.Id);
            AddTransientModifier(modifier);
        }
    }

    //Packed 属性实例的存档形态 对应原版 AttributeInstance.Packed
    //modifiers 按原版语义是永久修饰符 本作与全部修饰符一致
    public sealed record Packed(Attribute Attribute, double BaseValue, IReadOnlyList<AttributeModifier> Modifiers);

    //CalculateValue 按原版三段式算值 顺序不能换
    //先把全部 add_value 加在基值上 再按加完的基值算 add_multiplied_base 最后逐个连乘 add_multiplied_total
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

    //SetDirty 标脏并回调持有者 对应原版 setDirty
    private void SetDirty()
    {
        _dirty = true;
        _onDirty?.Invoke(this);
    }

    //ModifiersIn 取某运算下的修饰符 没登记过返回空
    private IReadOnlyCollection<AttributeModifier> ModifiersIn(AttributeOperation operation)
        => _modifiersByOperation.TryGetValue(operation, out var byId)
            ? byId.Values
            : Array.Empty<AttributeModifier>();

    //GetOrCreateModifiersIn 取某运算下的修饰符表 没有就建一个
    private Dictionary<Identifier, AttributeModifier> GetOrCreateModifiersIn(AttributeOperation operation)
    {
        if (_modifiersByOperation.TryGetValue(operation, out var byId)) return byId;
        byId = new Dictionary<Identifier, AttributeModifier>();
        _modifiersByOperation[operation] = byId;
        return byId;
    }
}
