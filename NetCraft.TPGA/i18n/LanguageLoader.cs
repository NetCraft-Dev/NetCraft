using System.Text.Json;

namespace NetCraft.TPGA.Localization;

//LanguageLoader 从 locales 目录加载 JSON 语言包
//文件名 {language}.json 如 zh_CN.json en_US.json
public static class LanguageLoader
{
    private static readonly JsonSerializerOptions _options = new() { PropertyNameCaseInsensitive = true };

    //Load 加载指定语言 JSON 文件 不存在或解析失败返回空字典
    public static Dictionary<string, string> Load(string localesDir, string language)
    {
        var path = Path.Combine(localesDir, $"{language}.json");
        if (!File.Exists(path)) return new Dictionary<string, string>();
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json, _options) ?? new();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }
}
