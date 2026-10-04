using NetCraft.Network.Chat;
using NetCraft.Registry;
using NetCraft.Resources;

namespace NetCraft.Game;

//LanguageTable 语言表装配 从资源包与语言目录按语言码拼出一份语言实例
//对齐原版 ClientLanguage.loadFrom 的 languageStack 语义 先装 en_us 兜底再装目标码覆盖
//原版译名走资源包的 assets/<命名空间>/lang/<语言码>.json
//NC 自有文案走程序根目录 lang/<语言码>.json 两边都是键值对 合进同一张表
public static class LanguageTable
{
    //Load 装配指定语言码的表并替换当前实例
    public static void Load(ResourceManager manager, string code) => Language.Inject(Build(manager, code));

    //Build 装配语言表但不替换当前实例 供测试与清单校验
    public static Language Build(ResourceManager manager, string code)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        //兜底底表先装 目标语言后装覆盖 与客户端 languageStack 的顺序一致
        Apply(entries, manager, Language.Default);
        var resolved = Resolve(manager, code);
        if (!string.Equals(resolved, Language.Default, StringComparison.Ordinal))
            Apply(entries, manager, resolved);
        return Language.FromEntries(entries);
    }

    //Apply 把该语言码的条目并入表 资源包与语言目录都读 后写覆盖先写
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

    //Resolve 语言码不被支持时退回 en_us
    //支持 = 语言目录里有这个语言文件 或某个资源包里有这个语言码的语言文件
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
