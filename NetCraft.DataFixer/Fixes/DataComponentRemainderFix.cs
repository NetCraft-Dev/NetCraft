using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;

namespace NetCraft.DataFixer.Fixes;

//数据组件残留修复抽象父类对应原版net.minecraft.util.datafix.fixes.DataComponentRemainderFix
//子类实现fixComponent处理指定组件的修改/移位/重命名
public abstract class DataComponentRemainderFix : DataFix
{
    private readonly string _name;
    private readonly string _componentId;
    private readonly string _newComponentId;

    protected abstract Dynamic<object> FixComponent(Dynamic<object> input);

    public DataComponentRemainderFix(Schema outputSchema, string name, string componentId)
        : this(outputSchema, name, componentId, componentId) { }

    public DataComponentRemainderFix(Schema outputSchema, string name, string componentId, string newComponentId)
        : base(outputSchema, false)
    {
        _name = name;
        _componentId = componentId;
        _newComponentId = newComponentId;
    }

    protected sealed override TypeRewriteRule MakeRule()
    {
        var dataComponentsType = GetInputSchema().GetType(References.DataComponents);
        return FixTypeEverywhereTyped(_name, dataComponentsType, components =>
        {
            return components.Update(DSL.RemainderFinder(), remainder =>
            {
                var componentOpt = remainder.Get(_componentId).Result();
                if (!componentOpt.IsPresent) return remainder;
                var newComponent = FixComponent(componentOpt.Get());
                return remainder.Remove(_componentId).SetFieldIfPresent(_newComponentId,
                    newComponent is null ? Optional<Dynamic<object>>.Empty() : Optional<Dynamic<object>>.Of(newComponent));
            });
        });
    }
}
