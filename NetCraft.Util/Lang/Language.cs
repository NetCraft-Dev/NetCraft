namespace NetCraft.Network.Chat;

using System.Text.Json;
using System.Text.RegularExpressions;

//语言表对应原版net.minecraft.locale.Language
//翻译缺失回退成 key 本身对齐原版 getOrDefault(elementId) 的语义
//服务端固定用 en_us 对齐原版 DedicatedServer 不做本地化的行为
//放在 Util 项目是因为它是纯文本基础设施 日志层也要用 命名空间保持 Network.Chat 不动已有引用
public abstract class Language
{
    //Default 默认语言代号对应原版 DEFAULT
    public const string Default = "en_us";

    //UnsupportedFormatPattern 把 %d %f 这类无法直接拼接的占位统一改写成 %s 对应原版 UNSUPPORTED_FORMAT_PATTERN
    //必须声明在 DefaultInstance 之前 否则加载默认语言时它还空着
    private static readonly Regex UnsupportedFormatPattern =
        new(@"%(\d+\$)?[\d.]*[df]", RegexOptions.Compiled);

    //DefaultInstance 默认语言实例对应原版 DEFAULT_INSTANCE 静态初始化即加载
    public static readonly Language DefaultInstance = LoadDefault();

    //Instance 当前语言实例对应原版 getInstance
    public static Language Instance => _instance;

    private static Language _instance = DefaultInstance;

    //Inject 替换当前语言实例对应原版 inject 客户端切换语言时用
    public static void Inject(Language language) => _instance = language;

    //FromEntries 用现成键值表构造语言实例 表构造后不再改动 多线程读取安全
    public static Language FromEntries(Dictionary<string, string> entries) => new MapLanguage(entries);

    //GetOrDefault 缺翻译回退 key 对应原版 getOrDefault(String)
    public string GetOrDefault(string elementId) => GetOrDefault(elementId, elementId);

    //GetOrDefault 带默认值对应原版 getOrDefault(String,String)
    public abstract string GetOrDefault(string elementId, string defaultValue);

    //Has 判断是否存在翻译对应原版 has
    public abstract bool Has(string elementId);

    //LoadFromJson 解析语言 json 逐条写入 output 对应原版 loadFromJson
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

    //LoadDefault 从程序目录 assets/minecraft/lang/en_us.json 读取默认语言
    //文件缺失时留空表 翻译组件退回 key 不至于崩
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

    //MapLanguage 只读字典实现 表加载完不再改动 多线程读取安全
    private sealed class MapLanguage(Dictionary<string, string> storage) : Language
    {
        public override string GetOrDefault(string elementId, string defaultValue)
            => storage.TryGetValue(elementId, out var value) ? value : defaultValue;

        public override bool Has(string elementId) => storage.ContainsKey(elementId);
    }
}
