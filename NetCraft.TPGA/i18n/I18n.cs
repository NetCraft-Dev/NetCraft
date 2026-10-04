using NetCraft.TPGA.Logging;

namespace NetCraft.TPGA.Localization;

//I18n 国际化系统 静态访问
//语言包从 locales/*.json 加载 key 用点分 Get 找不到返回 key 本身
//必须在 ConfigManager.Load 之前用 PreLoadLanguage 的语言 Init
public static class I18n
{
    private static Dictionary<string, string> _messages = new();
    private static string _currentLanguage = "en_US";

    //CurrentLanguage 当前语言代码
    public static string CurrentLanguage => _currentLanguage;

    //Init 加载指定语言包 失败回退 en_US
    public static void Init(string language, string localesDir)
    {
        _currentLanguage = NormalizeLanguage(language);
        _messages = LanguageLoader.Load(localesDir, _currentLanguage);
        if (_messages.Count == 0 && _currentLanguage != "en_US")
        {
            Log.Warning("I18n", $"语言包 {_currentLanguage} 缺失回退 en_US");
            _currentLanguage = "en_US";
            _messages = LanguageLoader.Load(localesDir, _currentLanguage);
        }
    }

    //Get 按 key 取文案 找不到返回 key 本身
    public static string Get(string key)
        => _messages.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) ? value : key;

    //NormalizeLanguage 归一化语言代码 zh-CN/zhCN → zh_CN
    private static string NormalizeLanguage(string language)
    {
        if (string.IsNullOrWhiteSpace(language)) return "en_US";
        return language.Replace("-", "_").Trim();
    }
}
