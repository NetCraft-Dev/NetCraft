using NetCraft.DataFixer.Schemas;

namespace NetCraft.DataFixer.Fixes;

//属性ID前缀修复对应原版AttributeIdPrefixFix
//1.21.2移除属性ID的generic./horse./player./zombie.前缀统一minecraft:命名空间
public class AttributeIdPrefixFix : AttributesRenameFix
{
    private static readonly string[] PREFIXES = { "generic.", "horse.", "player.", "zombie." };

    public AttributeIdPrefixFix(Schema outputSchema)
        : base(outputSchema, "AttributeIdPrefixFix", ReplaceId, true) { }

    //replaceId按前缀列表逐个尝试匹配命中则截掉前缀加minecraft:前缀
    private static string ReplaceId(string id)
    {
        var namespacedId = NamespacedSchema.EnsureNamespaced(id);
        foreach (var prefix in PREFIXES)
        {
            var namespacedPrefix = NamespacedSchema.EnsureNamespaced(prefix);
            if (namespacedId.StartsWith(namespacedPrefix))
            {
                return "minecraft:" + namespacedId.Substring(namespacedPrefix.Length);
            }
        }
        return id;
    }
}
