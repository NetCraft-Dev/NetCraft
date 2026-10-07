using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;

namespace NetCraft.Game.DFU.Fixes;

using NetCraft.DataFixer.Fixes;

using NetCraft.DataFixer;

//Attribute ID legacy rename, maps to vanilla AttributesRenameLegacy
//Before 1.20.5 it handles the AttributeName/Name fields of both ItemStack.AttributeModifiers and Entity.Attributes
//The renames function maps each attribute ID and keeps the original value on failure
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

    //fixName maps a string ID through the renames function and keeps the original value on failure
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
    //The old save key is the capitalized Attributes; only the new key is lowercase attributes, so this must not follow the constant
    private Typed<object> FixEntity(Typed<object> entity)
        => entity.Update(DSL.RemainderFinder(), tag =>
            tag.Update("Attributes", attributeList => FixListField(attributeList, FixConstants.StateHolderName)));

    //fixListField applies the fn mapping per element into a new list, keeping the original value on failure
    private Dynamic<object> FixListField(Dynamic<object> listDynamic, string fieldName)
    {
        var streamOpt = listDynamic.AsStream().Result();
        if (!streamOpt.IsPresent) return listDynamic;
        var mapped = streamOpt.Get().Select(item => item.Update(fieldName, FixName));
        return listDynamic.CreateList(mapped);
    }
}
