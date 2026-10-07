using NetCraft.DataFixer.Schemas;

namespace NetCraft.Game.DFU.Fixes;

using NetCraft.DataFixer.Fixes;

using NetCraft.DataFixer;

//Attribute ID prefix fix, maps to vanilla AttributeIdPrefixFix
//1.21.2 removes the generic./horse./player./zombie. prefixes from attribute IDs and unifies them under the minecraft: namespace
public class AttributeIdPrefixFix : AttributesRenameFix
{
    private static readonly string[] PREFIXES = { "generic.", "horse.", "player.", "zombie." };

    public AttributeIdPrefixFix(Schema outputSchema)
        : base(outputSchema, "AttributeIdPrefixFix", ReplaceId, true) { }

    //replaceId tries each prefix in the list; on a hit it trims the prefix and adds the minecraft: prefix
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
