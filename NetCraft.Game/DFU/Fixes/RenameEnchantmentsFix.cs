using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;

namespace NetCraft.Game.DFU.Fixes;

using NetCraft.DataFixer.Fixes;

using NetCraft.DataFixer;

//Enchantment rename fix, maps to vanilla RenameEnchantmentsFix
//Before 1.20.5 it renames the id field in the Enchantments and StoredEnchantments lists under ItemStack.tag
//renames is the old ID to new ID mapping table
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

    //fixTag applies the fix to the Enchantments and StoredEnchantments fields
    private Dynamic<object> FixTag(Dynamic<object> tag)
        => FixEnchantmentList(FixEnchantmentList(tag, "Enchantments"), "StoredEnchantments");

    //fixEnchantmentList fixes the id field of each entry in itemStack's field list
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
