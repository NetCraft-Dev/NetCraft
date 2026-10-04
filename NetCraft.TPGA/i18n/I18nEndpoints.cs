using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NetCraft.TPGA.Config;
using NetCraft.TPGA.Logging;

namespace NetCraft.TPGA.Localization;

//I18nEndpoints 前端 webui 翻译端点
//translations 按 lang 参数返回对应语言 lang 缺失或无对应文件回退 config.Language 再回退 en_US
//languages 列出 i18n/webui 下可用语言 前端用于展示可选语言
//端点不挂 /api 前缀 不经 AdminAuth 未登录也能加载翻译 否则登录页无文案
public static class I18nEndpoints
{
    private static readonly JsonSerializerOptions _options = new() { PropertyNameCaseInsensitive = true };
    //WebuiDir 运行目录下 i18n/webui 存放前端翻译 json
    private static string WebuiDir => Path.Combine(AppContext.BaseDirectory, "i18n", "webui");

    //MapI18nEndpoints 注册 translations 与 languages 路由 挂 api 分支
    public static IEndpointRouteBuilder MapI18nEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/i18n/translations", (string? lang, TpgaConfig config) => ServeTranslations(lang, config));
        app.MapGet("/i18n/languages", ServeLanguages);
        return app;
    }

    //ServeTranslations 解析语言并返回翻译字典 lang 回退 config.Language 再回退 en_US
    private static IResult ServeTranslations(string? lang, TpgaConfig config)
    {
        var dir = WebuiDir;
        var resolved = ResolveLanguage(dir, lang, config.Language);
        var translations = LoadTranslations(dir, resolved);
        return Results.Json(new { ok = true, lang = resolved, translations });
    }

    //ServeLanguages 扫描 webui 目录返回可用语言代码列表
    private static IResult ServeLanguages()
    {
        var dir = WebuiDir;
        var list = new List<string>();
        if (Directory.Exists(dir))
        {
            list = Directory.GetFiles(dir, "*.json")
                .Select(p => Path.GetFileNameWithoutExtension(p))
                .OrderBy(x => x)
                .ToList();
        }
        return Results.Json(new { ok = true, languages = list });
    }

    //ResolveLanguage 依次尝试 lang→config.Language→en_US 取首个存在文件的
    private static string ResolveLanguage(string dir, string? lang, string configLang)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(lang)) candidates.Add(Normalize(lang));
        candidates.Add(Normalize(configLang));
        candidates.Add("en_US");
        foreach (var c in candidates.Distinct())
        {
            if (File.Exists(Path.Combine(dir, c + ".json"))) return c;
        }
        return "en_US";
    }

    //LoadTranslations 读 json 反序列化为字典 失败返回空字典并告警
    private static Dictionary<string, string> LoadTranslations(string dir, string lang)
    {
        var path = Path.Combine(dir, lang + ".json");
        if (!File.Exists(path)) return new Dictionary<string, string>();
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json, _options) ?? new();
        }
        catch (Exception e)
        {
            Log.Warning("I18n", $"load webui translations failed lang={lang} err={e.Message}");
            return new Dictionary<string, string>();
        }
    }

    //Normalize 归一化语言代码 zh-CN/zhCN → zh_CN 小写后首尾修整
    private static string Normalize(string lang)
    {
        if (string.IsNullOrWhiteSpace(lang)) return "en_US";
        var v = lang.Replace("-", "_").Trim();
        //zh_CN/en_US 保留原样 其他无下划线的补 _XX 保证与文件名匹配
        if (!v.Contains('_') && v.Length >= 2)
        {
            var region = v.Equals("zh", StringComparison.OrdinalIgnoreCase) ? "CN" : "US";
            v = v + "_" + region;
        }
        return v;
    }
}
