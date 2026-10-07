using NetCraft.DataFixer.Schemas;

namespace NetCraft.DataFixer.Fixes;

//attribute ID prefix fix, maps to vanilla AttributeIdPrefixFix
//1.21.2 removes the generic./horse./player./zombie. prefixes from attribute IDs, unifying under the minecraft: namespace
public class AttributeIdPrefixFix : AttributesRenameFix
{
    private static readonly string[] PREFIXES = { "generic.", "horse.", "player.", "zombie." };

    public AttributeIdPrefixFix(Schema outputSchema)
        : base(outputSchema, "AttributeIdPrefixFix", ReplaceId, true) { }

    //replaceId tries each prefix in order; on a hit it strips the prefix and adds the minecraft: prefix
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
