using System.IO;
using NetCraft.Network.Chat;

namespace NetCraft;

//NcLanguageFiles NC 自有语言文件在磁盘上的落地与读取
//语言文件编译期内嵌进 NetCraft.Game 启动时解压到程序根目录的 lang/
//之后一律从磁盘读 用户可以直接改文件或者往里补新语言 不必重新编译
public static class NcLanguageFiles
{
    //_embeddedPrefix 内嵌资源名前缀 与 csproj 的 LogicalName 对应
    private const string EmbeddedPrefix = "assets/netcraft/lang/";

    //Extract 把内嵌语言文件解压到语言目录 返回新释放的文件数
    //已存在的文件不覆盖 用户改过的翻译要留住
    //targetRoot 为空时落程序根目录 测试可指定临时目录避免污染
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

    //Exists 语言目录里有没有该语言码的文件
    public static bool Exists(string code, string? targetRoot = null)
        => File.Exists(Path.Combine(Dir(targetRoot), code + ".json"));

    //Load 读该语言码的文件并入表 文件不存在返回 false
    public static bool Load(string code, Dictionary<string, string> entries, string? targetRoot = null)
    {
        var path = Path.Combine(Dir(targetRoot), code + ".json");
        if (!File.Exists(path)) return false;
        using var stream = File.OpenRead(path);
        Language.LoadFromJson(stream, (key, value) => entries[key] = value);
        return true;
    }

    //Dir 语言目录 程序根目录下的 lang/
    public static string Dir(string? targetRoot = null)
        => Path.Combine(targetRoot ?? AppPaths.BaseDirectory, "lang");
}
