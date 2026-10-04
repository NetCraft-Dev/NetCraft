using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;

namespace NetCraft.DataFixer.Fixes;

//附魔重命名修复对应原版RenameEnchantmentsFix
//1.20.5前重命名ItemStack.tag下Enchantments与StoredEnchantments列表中id字段
//renames为旧ID到新ID映射表
public class RenameEnchantmentsFix : DataFix
{
    private readonly string _name;
    private readonly Dictionary<string, string> _renames;

    public RenameEnchantmentsFix(Schema outputSchema, string name, Dictionary<string, string> renames)
        : base(outputSchema, false)
    {
        _name = name;
        _renames = renames;
    }

    protected override TypeRewriteRule MakeRule()
    {
        var item = GetInputSchema().GetType(References.ItemStack);
        var tagFinder = item.FindField("tag");
        return FixTypeEverywhereTyped(_name, item, input =>
            input.UpdateTyped(tagFinder, tag => tag.Update(DSL.RemainderFinder(), FixTag)));
    }

    //fixTag对Enchantments与StoredEnchantments两个字段应用修复
    private Dynamic<object> FixTag(Dynamic<object> tag)
        => FixEnchantmentList(FixEnchantmentList(tag, "Enchantments"), "StoredEnchantments");

    //fixEnchantmentList对itemStack的field字段列表逐项修复id字段
    private Dynamic<object> FixEnchantmentList(Dynamic<object> itemStack, string field)
        => itemStack.Update(field, tag =>
        {
            var mapped = tag.AsStream().Map(s => s.Select(element =>
                element.Update("id", id =>
                {
                    var renamed = id.AsString().Map(stringId =>
                        element.CreateString(_renames.TryGetValue(NamespacedSchema.EnsureNamespaced(stringId), out var v) ? v : stringId));
                    return renamed.MapOrElse(d => d, _ => id);
                })));
            return mapped.Map(list => tag.CreateList(list)).MapOrElse(d => d, _ => tag);
        });
}
