using System.IO;
using NetCraft.Network.Chat;

namespace NetCraft;

//NcLanguageFiles writes and reads NC's own language files on disk
//Language files are embedded into NetCraft.Game at compile time and extracted to the program root's lang/ at startup
//Afterwards they are always read from disk; users can edit the files or add new languages without recompiling
public static class NcLanguageFiles
{
    //_embeddedPrefix embedded resource name prefix, matching the csproj's LogicalName
    private const string EmbeddedPrefix = "assets/netcraft/lang/";

    //Extract unpacks embedded language files into the language directory and returns the number of newly released files
    //Existing files are not overwritten; users' edited translations are preserved
    //When targetRoot is empty, lands in the program root; tests can point to a temp directory to avoid pollution
    public static int Extract(string? targetRoot = null)
    {
        var dir = Dir(targetRoot);
        var assembly = typeof(NcLanguageFiles).Assembly;
        var written = 0;
        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(EmbeddedPrefix, StringComparison.Ordinal)) continue;
            var target = Path.Combine(dir, name[EmbeddedPrefix.Length..]);
            if (File.Exists(target)) continue;

            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is null) continue;
            Directory.CreateDirectory(dir);
            using var file = File.Create(target);
            stream.CopyTo(file);
            written++;
        }
        return written;
    }

    //Exists checks whether the language directory has a file for the given language code
    public static bool Exists(string code, string? targetRoot = null)
        => File.Exists(Path.Combine(Dir(targetRoot), code + ".json"));

    //Load reads the file for the language code into the table; returns false when the file does not exist
    public static bool Load(string code, Dictionary<string, string> entries, string? targetRoot = null)
    {
        var path = Path.Combine(Dir(targetRoot), code + ".json");
        if (!File.Exists(path)) return false;
        using var stream = File.OpenRead(path);
        Language.LoadFromJson(stream, (key, value) => entries[key] = value);
        return true;
    }

    //Dir the language directory, lang/ under the program root
    public static string Dir(string? targetRoot = null)
        => Path.Combine(targetRoot ?? AppPaths.BaseDirectory, "lang");
}
