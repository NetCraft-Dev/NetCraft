using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;

namespace NetCraft.DataFixer.Fixes;

//legacy attribute ID rename, maps to vanilla AttributesRenameLegacy
//before 1.20.5, handles the AttributeName/Name fields of ItemStack.AttributeModifiers and Entity.Attributes
//the renames function maps each attribute ID, keeping the original on failure
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

    //fixName maps a string ID via the renames function, keeping the original on failure
    private Dynamic<object> FixName(Dynamic<object> name)
    {
        var mapped = name.AsString().Result().Map(_renames);
        return DataFixUtils.OrElse(mapped.Map(name.CreateString), name);
    }

    //fixItemStackTag handles the AttributeName field of each ItemStack.tag.AttributeModifiers entry
    private Typed<object> FixItemStackTag(Typed<object> itemStack)
        => itemStack.Update(DSL.RemainderFinder(), tag =>
            tag.Update("AttributeModifiers", modifiers => FixListField(modifiers, "AttributeName")));

    //fixEntity handles the Name field of each Entity.Attributes entry
    //old save key is uppercase Attributes, only the new key is lowercase attributes, so this cannot follow the constant
    private Typed<object> FixEntity(Typed<object> entity)
        => entity.Update(DSL.RemainderFinder(), tag =>
            tag.Update("Attributes", attributeList => FixListField(attributeList, FixConstants.StateHolderName)));

    //fixListField maps the element stream with fn into a new list, keeping the original on failure
    private Dynamic<object> FixListField(Dynamic<object> listDynamic, string fieldName)
    {
        var streamOpt = listDynamic.AsStream().Result();
        if (!streamOpt.IsPresent) return listDynamic;
        var mapped = streamOpt.Get().Select(item => item.Update(fieldName, FixName));
        return listDynamic.CreateList(mapped);
    }
}
