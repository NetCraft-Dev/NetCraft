using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;

namespace NetCraft.Game.DFU.Fixes;

using NetCraft.DataFixer.Fixes;

using NetCraft.DataFixer;

//属性ID旧版重命名对应原版AttributesRenameLegacy
//1.20.5前同时处理ItemStack.AttributeModifiers与Entity.Attributes的AttributeName/Name字段
//renames函数对每个属性ID做映射失败保留原值
public class AttributesRenameLegacy : DataFix
{
    private readonly string _name;
    private readonly Func<string, string> _renames;

    public AttributesRenameLegacy(Schema outputSchema, string name, Func<string, string> renames)
        : base(outputSchema, false)
    {
        _name = name;
        _renames = renames;
    }

    protected override TypeRewriteRule MakeRule()
    {
        var itemStackType = GetInputSchema().GetType(References.ItemStack);
        var tagF = itemStackType.FindField("tag");
        return TypeRewriteRule.Seq(
            FixTypeEverywhereTyped(_name + " (ItemStack)", itemStackType, itemStack => itemStack.UpdateTyped(tagF, FixItemStackTag)),
            TypeRewriteRule.Seq(
                FixTypeEverywhereTyped(_name + " (Entity)", GetInputSchema().GetType(References.Entity), FixEntity),
                FixTypeEverywhereTyped(_name + " (Player)", GetInputSchema().GetType(References.Player), FixEntity)));
    }

    //fixName按renames函数映射字符串ID失败保留原值
    private Dynamic<object> FixName(Dynamic<object> name)
    {
        var mapped = name.AsString().Result().Map(_renames);
        return DataFixUtils.OrElse(mapped.Map(name.CreateString), name);
    }

    //fixItemStackTag处理ItemStack.tag.AttributeModifiers每项的AttributeName字段
    private Typed<object> FixItemStackTag(Typed<object> itemStack)
        => itemStack.Update(DSL.RemainderFinder(), tag =>
            tag.Update("AttributeModifiers", modifiers => FixListField(modifiers, "AttributeName")));

    //fixEntity处理Entity.Attributes每项的Name字段
    //旧存档键是大写 Attributes 新键才是小写 attributes 这里不能跟着常量改
    private Typed<object> FixEntity(Typed<object> entity)
        => entity.Update(DSL.RemainderFinder(), tag =>
            tag.Update("Attributes", attributeList => FixListField(attributeList, FixConstants.StateHolderName)));

    //fixListField按元素流应用fn映射到新列表失败保留原值
    private Dynamic<object> FixListField(Dynamic<object> listDynamic, string fieldName)
    {
        var streamOpt = listDynamic.AsStream().Result();
        if (!streamOpt.IsPresent) return listDynamic;
        var mapped = streamOpt.Get().Select(item => item.Update(fieldName, FixName));
        return listDynamic.CreateList(mapped);
    }
}
