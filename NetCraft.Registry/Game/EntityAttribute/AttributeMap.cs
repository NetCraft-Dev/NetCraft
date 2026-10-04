using NetCraft.Nbt;

namespace NetCraft.Registry.EntityAttribute;

//AttributeMap 实体的属性表 对应原版 AttributeMap
//实例惰性创建 实体没碰过的属性直接取类型默认表的值
//服务端还要维护"本刻被改脏的属性"供每刻增量同步 见 AttributesToSync
public sealed class AttributeMap
{
    //AttributesTag 属性存档字段名 对应原版 LivingEntity.TAG_ATTRIBUTES
    private const string AttributesTag = "attributes";

    //_attributes 实体自己的实例 只有被取过或被改过的属性才落在这里 对应原版 attributes
    private readonly Dictionary<Attribute, AttributeInstance> _attributes = new();
    //_attributesToSync 本刻被改脏且需要同步给客户端的属性 对应原版 attributesToSync
    private readonly HashSet<AttributeInstance> _attributesToSync = new();
    //_supplier 该实体类型的属性默认表 实体类型没登记过的属性一律取不到实例
    private readonly AttributeSupplier _supplier;

    public AttributeMap(AttributeSupplier supplier) => _supplier = supplier;

    //GetInstance 取实体自己的实例 没建过就按模板复制一个 该类型没这个属性返回 null 对应原版 getInstance
    public AttributeInstance? GetInstance(Attribute attribute)
    {
        if (_attributes.TryGetValue(attribute, out var existing)) return existing;
        var created = _supplier.CreateInstance(attribute, OnAttributeModified);
        if (created is not null) _attributes[attribute] = created;
        return created;
    }

    //GetValue 取属性最终值 实体有自己的实例就用它 否则回落类型默认表 对应原版 getValue
    //默认表没登记该属性时再回落属性自身的默认值 调用方不必先 hasAttribute
    public double GetValue(Attribute attribute)
        => _attributes.TryGetValue(attribute, out var instance)
            ? instance.Value
            : SupplierHas(attribute) ? _supplier.GetValue(attribute) : attribute.DefaultValue;

    //GetBaseValue 取属性基值 对应原版 getBaseValue
    public double GetBaseValue(Attribute attribute)
        => _attributes.TryGetValue(attribute, out var instance)
            ? instance.BaseValue
            : SupplierHas(attribute) ? _supplier.GetBaseValue(attribute) : attribute.DefaultValue;

    //ResetBaseValue 把属性基值恢复为该类型默认表的基值 对应原版 resetBaseValue
    //类型没登记该属性返回 false 实例还没建过就不必恢复直接算成功 与供给方行为一致
    public bool ResetBaseValue(Attribute attribute)
    {
        if (!SupplierHas(attribute)) return false;
        if (_attributes.TryGetValue(attribute, out var instance))
            instance.SetBaseValue(_supplier.GetBaseValue(attribute));
        return true;
    }

    //HasAttribute 实体是否有该属性 对应原版 hasAttribute
    public bool HasAttribute(Attribute attribute)
        => _attributes.ContainsKey(attribute) || _supplier.HasAttribute(attribute);

    //SyncableAttributes 全部需要同步给客户端的属性 对应原版 getSyncableAttributes
    //实体配对时按它下发全量 只含已实例化的属性 没被碰过的走客户端本地默认表
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

    //AttributesToSync 本刻被改脏且需要同步的属性 对应原版 getAttributesToSync
    public IReadOnlyCollection<AttributeInstance> AttributesToSync => _attributesToSync;

    //Pack 打包全部属性实例供存档 只含被改动过的属性 对应原版 pack
    //没碰过的属性不占存档 读回时按类型默认表取值
    public IReadOnlyList<AttributeInstance.Packed> Pack()
    {
        var result = new List<AttributeInstance.Packed>(_attributes.Count);
        foreach (var instance in _attributes.Values) result.Add(instance.Pack());
        return result;
    }

    //Apply 应用存档读回的属性 该类型没这个属性就丢弃该项 对应原版 apply
    public void Apply(IReadOnlyList<AttributeInstance.Packed> packedAttributes)
    {
        foreach (var packed in packedAttributes)
        {
            var instance = GetInstance(packed.Attribute);
            instance?.Apply(packed);
        }
    }

    //WriteTo 把属性写进实体存档 对应原版 LivingEntity 存档里的 attributes 字段
    //属性按注册名写 注册表顺序变化不影响读回 没登记进注册表的属性跳过
    public void WriteTo(CompoundTag tag)
    {
        var list = new ListTag();
        foreach (var packed in Pack())
        {
            if (BuiltInRegistries.ATTRIBUTE.GetKey(packed.Attribute) is not { } id) continue;
            var entry = new CompoundTag();
            entry.PutString("id", id.ToString());
            //base 原版总是写出 读回缺失时按 0 处理
            entry.PutDouble("base", packed.BaseValue);
            //modifiers 为空按原版省略该字段
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

    //ReadFrom 从实体存档读回属性 没有该字段就保持当前表不动
    //单条解不出来只丢那一条 不整份放弃 对应原版 LivingEntity 读回后调 AttributeMap.apply
    public void ReadFrom(CompoundTag tag)
    {
        if (tag.GetList(AttributesTag) is not { } list) return;
        var packed = new List<AttributeInstance.Packed>(list.Count);
        for (var i = 0; i < list.Count; i++)
            if (list.GetCompound(i) is { } entry && ReadAttribute(entry) is { } one) packed.Add(one);
        Apply(packed);
    }

    //WriteModifier 写出单个修饰符 对应原版 AttributeModifier.CODEC
    //operation 用序列化名不用枚举序号 存档不依赖枚举顺序
    private static CompoundTag WriteModifier(AttributeModifier modifier)
    {
        var tag = new CompoundTag();
        tag.PutString("id", modifier.Id.ToString());
        tag.PutDouble("amount", modifier.Amount);
        tag.PutString("operation", AttributeModifier.GetSerializedName(modifier.Operation));
        return tag;
    }

    //ReadAttribute 读单个属性条目 未注册的属性直接丢掉
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

    //ReadModifier 读单个修饰符 运算名不认识就丢掉该条
    private static AttributeModifier? ReadModifier(CompoundTag tag)
    {
        if (Identifier.TryParse(tag.GetStringValue("id")) is not { } id) return null;
        if (AttributeModifier.TryFromName(tag.GetStringValue("operation")) is not { } operation) return null;
        return new AttributeModifier(id, tag.GetDouble("amount")?.Value ?? 0.0, operation);
    }

    //ClearAttributesToSync 同步之后清空待同步集合 对应原版发送点的 attributes.clear()
    public void ClearAttributesToSync() => _attributesToSync.Clear();

    //OnAttributeModified 实例被改脏时回调 对应原版 onAttributeModified
    //只有标记同步的属性才进集合 不同步的属性改了也不该发出去
    private void OnAttributeModified(AttributeInstance instance)
    {
        if (instance.Attribute.ClientSyncable) _attributesToSync.Add(instance);
    }

    //SupplierHas 类型默认表里是否有该属性
    private bool SupplierHas(Attribute attribute) => _supplier.HasAttribute(attribute);
}
