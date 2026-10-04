using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;

namespace NetCraft.DataFixer.Fixes;

//属性重命名修复类对应原版net.minecraft.util.datafix.fixes.AttributesRenameFix
//同时处理DataComponents/Entity/Player中属性ID的重命名oldDataComponentFormat决定旧版还是新版格式
public class AttributesRenameFix : DataFix
{
    private readonly string _name;
    private readonly Func<string, string> _renames;
    private readonly bool _oldDataComponentFormat;

    public AttributesRenameFix(Schema outputSchema, string name, Func<string, string> renames)
        : this(outputSchema, name, renames, false) { }

    public AttributesRenameFix(Schema outputSchema, string name, Func<string, string> renames, bool oldDataComponentFormat)
        : base(outputSchema, false)
    {
        _name = name;
        _renames = renames;
        _oldDataComponentFormat = oldDataComponentFormat;
    }

    protected override TypeRewriteRule MakeRule()
        => TypeRewriteRule.Seq(
            FixTypeEverywhereTyped(_name + " (Components)", GetInputSchema().GetType(References.DataComponents),
                _oldDataComponentFormat ? FixDataComponentsOld : (Func<Typed<object>, Typed<object>>)FixDataComponents),
            TypeRewriteRule.Seq(
                FixTypeEverywhereTyped(_name + " (Entity)", GetInputSchema().GetType(References.Entity),
                    (Func<Typed<object>, Typed<object>>)FixEntity),
                FixTypeEverywhereTyped(_name + " (Player)", GetInputSchema().GetType(References.Player),
                    (Func<Typed<object>, Typed<object>>)FixEntity)));

    private Typed<object> FixDataComponents(Typed<object> components)
        => components.Update(DSL.RemainderFinder(), componentData =>
            componentData.Update("minecraft:attribute_modifiers", attributeModifiers =>
                FixListField(attributeModifiers, FixTypeField)));

    private Typed<object> FixDataComponentsOld(Typed<object> components)
        => components.Update(DSL.RemainderFinder(), componentData =>
            componentData.Update("minecraft:attribute_modifiers", attributeModifiers =>
                attributeModifiers.Update("modifiers", modifiers =>
                    FixListField(modifiers, FixTypeField))));

    private Typed<object> FixEntity(Typed<object> entity)
        => entity.Update(DSL.RemainderFinder(), tag =>
            tag.Update(FixConstants.LivingEntityAttributes, attributeList =>
                FixListField(attributeList, FixIdField)));

    private Dynamic<object> FixIdField(Dynamic<object> dynamic)
        => ExtraDataFixUtils.FixStringField(dynamic, "id", _renames);

    private Dynamic<object> FixTypeField(Dynamic<object> dynamic)
        => ExtraDataFixUtils.FixStringField(dynamic, FixConstants.ChunkRegionIoEventType, _renames);

    //列表字段尝试按元素映射失败时保留原值对应原版map.map(createList).orElse(original)
    private Dynamic<object> FixListField(Dynamic<object> listDynamic, Func<Dynamic<object>, Dynamic<object>> fn)
    {
        var streamOpt = listDynamic.AsStream().Result();
        var mappedOpt = streamOpt.Map(s => s.Select(fn));
        var mappedToDynamic = mappedOpt.Map(listDynamic.CreateList);
        return DataFixUtils.OrElse(mappedToDynamic, listDynamic);
    }
}
