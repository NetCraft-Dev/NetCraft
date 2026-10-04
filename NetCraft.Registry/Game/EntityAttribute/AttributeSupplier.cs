namespace NetCraft.Registry.EntityAttribute;

//AttributeSupplier 实体类型的属性默认表 对应原版 AttributeSupplier
//每个属性一条模板实例 实体建实例时按模板复制 没有登记过的属性一律取不到值
public sealed class AttributeSupplier
{
    //Empty 空表 没有登记任何属性 基类实体与不吃属性的实体用它
    public static readonly AttributeSupplier Empty = Builder.Create().Build();

    private readonly Dictionary<Attribute, AttributeInstance> _instances;

    private AttributeSupplier(Dictionary<Attribute, AttributeInstance> instances) => _instances = instances;

    //CreateInstance 按模板复制出实体自己的实例 该类型没登记这个属性返回 null 对应原版 createInstance
    public AttributeInstance? CreateInstance(Attribute attribute, Action<AttributeInstance>? onDirty)
    {
        if (!_instances.TryGetValue(attribute, out var template)) return null;
        var instance = new AttributeInstance(attribute, onDirty);
        instance.ReplaceFrom(template);
        return instance;
    }

    //HasAttribute 该类型是否登记了某属性 对应原版 hasAttribute
    public bool HasAttribute(Attribute attribute) => _instances.ContainsKey(attribute);

    //GetValue 取模板上的取值 实体没有自己的实例时回落到它 对应原版 getValue
    public double GetValue(Attribute attribute) => GetTemplate(attribute).Value;

    //GetBaseValue 取模板上的基值 对应原版 getBaseValue
    public double GetBaseValue(Attribute attribute) => GetTemplate(attribute).BaseValue;

    //GetTemplate 取模板实例 没登记该属性直接抛 与原版一致
    private AttributeInstance GetTemplate(Attribute attribute)
        => _instances.TryGetValue(attribute, out var template)
            ? template
            : throw new ArgumentException($"该实体类型没有属性 {attribute.DescriptionId}");

    //Builder 属性默认表构建器 对应原版 AttributeSupplier.Builder
    public sealed class Builder
    {
        private readonly Dictionary<Attribute, AttributeInstance> _instances = new();
        private bool _frozen;

        private Builder() { }

        public static Builder Create() => new();

        //Add 登记属性 基值取属性自身的默认值
        public Builder Add(Attribute attribute)
        {
            Create(attribute);
            return this;
        }

        //Add 登记属性并覆盖基值
        public Builder Add(Attribute attribute, double baseValue)
        {
            Create(attribute).SetBaseValue(baseValue);
            return this;
        }

        //Build 冻结成表 冻结后再改模板就抛 对应原版 Builder 的 instanceFrozen
        public AttributeSupplier Build()
        {
            _frozen = true;
            return new AttributeSupplier(new Dictionary<Attribute, AttributeInstance>(_instances));
        }

        //Create 建一条模板实例 冻结后任何改动都会在回调里抛
        private AttributeInstance Create(Attribute attribute)
        {
            var instance = new AttributeInstance(attribute, changed =>
            {
                if (_frozen)
                    throw new InvalidOperationException($"属性默认表已冻结 不能再改 {changed.Attribute.DescriptionId}");
            });
            _instances[attribute] = instance;
            return instance;
        }
    }
}
