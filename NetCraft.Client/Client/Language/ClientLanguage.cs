using NetCraft.Game;
using NetCraft.Resources;

namespace NetCraft.Client.Language;

//ClientLanguage client language table, maps to vanilla net.minecraft.client.resources.language.ClientLanguage
//Attached to the reload chain as a resource reload listener; each reload reassembles the language table for the current language code and replaces the global instance
//Vanilla refreshes the available-languages list on reload; here the language section of pack.mcmeta is read as well
public sealed class ClientLanguage : PreparableReloadListener
{
    //LanguageCode current language code, maps to vanilla Minecraft.options.languageCode; to switch languages change this and trigger a reload
    public string LanguageCode { get; set; }

    //AvailableLanguages languages declared by resource packs; empty before the first reload
    public IReadOnlyDictionary<string, LanguageInfo> AvailableLanguages => _available.Languages;

    private LanguageMetadataSection _available = LanguageMetadataSection.Empty;

    public ClientLanguage(string languageCode) => LanguageCode = languageCode;

    //Reload assembles and replaces the global instance for the current language code; when the code has no matching file only en_us remains as fallback
    public void Reload(ResourceManager rm, ReloadContext ctx)
    {
        LanguageTable.Load(rm, LanguageCode);
        _available = LanguageMetadataSection.ReadAll(rm);
    }
}
