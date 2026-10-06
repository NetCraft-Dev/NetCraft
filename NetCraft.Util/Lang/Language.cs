namespace NetCraft.Network.Chat;

using System.Text.Json;
using System.Text.RegularExpressions;

//Language table, maps to vanilla net.minecraft.locale.Language
//Missing translations fall back to the key itself, aligning with vanilla getOrDefault(elementId)
//The server always uses en_us, aligning with vanilla DedicatedServer's no-localization behavior
//Placed in the Util project because it is plain-text infrastructure the log layer also needs; the namespace stays Network.Chat to avoid touching existing references
public abstract class Language
{
    //Default default language code, maps to vanilla DEFAULT
    public const string Default = "en_us";

    //UnsupportedFormatPattern rewrites placeholders like %d %f that cannot be concatenated directly into %s, maps to vanilla UNSUPPORTED_FORMAT_PATTERN
    //Must be declared before DefaultInstance, otherwise it is still empty when the default language loads
    private static readonly Regex UnsupportedFormatPattern =
        new(@"%(\d+\$)?[\d.]*[df]", RegexOptions.Compiled);

    //DefaultInstance default language instance, maps to vanilla DEFAULT_INSTANCE, loaded on static init
    public static readonly Language DefaultInstance = LoadDefault();

    //Instance current language instance, maps to vanilla getInstance
    public static Language Instance => _instance;

    private static Language _instance = DefaultInstance;

    //Inject replaces the current language instance, maps to vanilla inject, used when the client switches language
    public static void Inject(Language language) => _instance = language;

    //FromEntries builds a language instance from an existing key-value table; the table is immutable after construction, safe for multi-threaded reads
    public static Language FromEntries(Dictionary<string, string> entries) => new MapLanguage(entries);

    //GetOrDefault falls back to the key on a missing translation, maps to vanilla getOrDefault(String)
    public string GetOrDefault(string elementId) => GetOrDefault(elementId, elementId);

    //GetOrDefault with a default value, maps to vanilla getOrDefault(String,String)
    public abstract string GetOrDefault(string elementId, string defaultValue);

    //Has tests whether a translation exists, maps to vanilla has
    public abstract bool Has(string elementId);

    //LoadFromJson parses the language json writing each entry into output, maps to vanilla loadFromJson
    public static void LoadFromJson(Stream stream, Action<string, string> output)
    {
        using var document = JsonDocument.Parse(stream);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            var text = UnsupportedFormatPattern.Replace(
                property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString()! : property.Name,
                match => "%" + (match.Groups[1].Success ? match.Groups[1].Value : string.Empty) + "s");
            output(property.Name, text);
        }
    }

    //LoadDefault reads the default language from assets/minecraft/lang/en_us.json under the program directory
    //Leaves an empty table when the file is missing; translation components fall back to the key instead of crashing
    private static Language LoadDefault()
    {
        var storage = new Dictionary<string, string>();
        var path = Path.Combine(AppContext.BaseDirectory, "assets", "minecraft", "lang", Default + ".json");
        if (File.Exists(path))
        {
            using var stream = File.OpenRead(path);
            LoadFromJson(stream, (key, value) => storage[key] = value);
        }
        return new MapLanguage(storage);
    }

    //MapLanguage readonly dictionary implementation, immutable after loading, safe for multi-threaded reads
    private sealed class MapLanguage(Dictionary<string, string> storage) : Language
    {
        public override string GetOrDefault(string elementId, string defaultValue)
            => storage.TryGetValue(elementId, out var value) ? value : defaultValue;

        public override bool Has(string elementId) => storage.ContainsKey(elementId);
    }
}
