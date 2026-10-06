using NetCraft.Network.Chat;

namespace NetCraft;

//NcLanguage loads tables only from the language directory, without touching resource packs
//Used when resource packs are not attached yet but logs already need text; installs NC's own text at the earliest startup stage
//Falls back to en_us when the language code has no matching file in the language directory
public static class NcLanguage
{
    //Load installs the table for the language code and replaces the current instance
    public static void Load(string code) => Language.Inject(Build(code));

    //Build installs the table without replacing the current instance, for testing
    //When languageRoot is empty, uses the program root's lang/; tests can point to a temp directory to avoid pollution
    public static Language Build(string code, string? languageRoot = null)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        NcLanguageFiles.Load(Language.Default, entries, languageRoot);
        var resolved = Resolve(code, languageRoot);
        if (!string.Equals(resolved, Language.Default, StringComparison.Ordinal))
            NcLanguageFiles.Load(resolved, entries, languageRoot);
        return Language.FromEntries(entries);
    }

    //Resolve falls back to en_us when the language code is unsupported
    public static string Resolve(string code, string? languageRoot = null)
        => string.Equals(code, Language.Default, StringComparison.Ordinal) || NcLanguageFiles.Exists(code, languageRoot)
            ? code
            : Language.Default;
}
