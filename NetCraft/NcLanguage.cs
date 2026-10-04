using NetCraft.Network.Chat;

namespace NetCraft;

//NcLanguage 只按语言目录装表 不碰资源包
//资源包还没挂上但日志已经要出文案时用它 启动最早期就能把 NC 自有文案装上
//语言码在语言目录里没有对应文件时退回 en_us
public static class NcLanguage
{
    //Load 按语言码装表并替换当前实例
    public static void Load(string code) => Language.Inject(Build(code));

    //Build 装表但不替换当前实例 供测试用
    //languageRoot 为空时用程序根目录的 lang/ 测试可指定临时目录避免污染
    public static Language Build(string code, string? languageRoot = null)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        NcLanguageFiles.Load(Language.Default, entries, languageRoot);
        var resolved = Resolve(code, languageRoot);
        if (!string.Equals(resolved, Language.Default, StringComparison.Ordinal))
            NcLanguageFiles.Load(resolved, entries, languageRoot);
        return Language.FromEntries(entries);
    }

    //Resolve 语言码不被支持时退回 en_us
    public static string Resolve(string code, string? languageRoot = null)
        => string.Equals(code, Language.Default, StringComparison.Ordinal) || NcLanguageFiles.Exists(code, languageRoot)
            ? code
            : Language.Default;
}
