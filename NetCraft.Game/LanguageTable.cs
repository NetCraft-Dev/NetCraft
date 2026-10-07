using NetCraft.Network.Chat;
using NetCraft.Registry;
using NetCraft.Resources;

namespace NetCraft.Game;

//LanguageTable language table assembly; builds a language instance from resource packs and the language directory by language code
//Aligned with the languageStack semantics of vanilla ClientLanguage.loadFrom: load en_us as a fallback first, then the target code to override
//Vanilla translations go through resource pack assets/<namespace>/lang/<language code>.json
//NC's own text goes through the program root lang/<language code>.json; both are key-value pairs merged into one table
public static class LanguageTable
{
    //Load assembles the table for the given language code and replaces the current instance
    public static void Load(ResourceManager manager, string code) => Language.Inject(Build(manager, code));

    //Build assembles the language table without replacing the current instance, for tests and manifest validation
    public static Language Build(ResourceManager manager, string code)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        //The fallback base table is loaded first and the target language overrides, consistent with the client languageStack order
        Apply(entries, manager, Language.Default);
        var resolved = Resolve(manager, code);
        if (!string.Equals(resolved, Language.Default, StringComparison.Ordinal))
            Apply(entries, manager, resolved);
        return Language.FromEntries(entries);
    }

    //Apply merges the entries for the language code into the table, reading both resource packs and the language directory; later writes override earlier ones
    private static void Apply(Dictionary<string, string> entries, ResourceManager manager, string code)
    {
        foreach (var space in manager.GetNamespaces(PackType.ClientResources))
        {
            var location = Identifier.FromNamespaceAndPath(space, "lang/" + code + ".json");
            var resource = manager.GetResource(PackType.ClientResources, location);
            if (resource is null) continue;
            using var stream = resource.Open();
            Language.LoadFromJson(stream, (key, value) => entries[key] = value);
        }
        NcLanguageFiles.Load(code, entries);
    }

    //Resolve falls back to en_us when the language code is unsupported
    //Supported = the language directory has this language file, or some resource pack has a language file for this code
    private static string Resolve(ResourceManager manager, string code)
    {
        if (string.Equals(code, Language.Default, StringComparison.Ordinal)) return Language.Default;
        if (NcLanguageFiles.Exists(code)) return code;
        foreach (var space in manager.GetNamespaces(PackType.ClientResources))
        {
            var location = Identifier.FromNamespaceAndPath(space, "lang/" + code + ".json");
            if (manager.GetResource(PackType.ClientResources, location) is not null) return code;
        }
        return Language.Default;
    }
}
