using NetCraft.Logging;

namespace NetCraft;

//GameConfig 客户端 options.txt 配置
//对应原版 net.minecraft.client.Options 简化版
//仅保留核心渲染与游戏体验字段渲染距离 FOV gamma 难度等
//原版含 100+ Option 字段 NC 简化版按需扩展
public sealed class GameConfig
{
    //渲染距离单位 chunk 默认 12
    public int RenderDistance { get; set; } = 12;

    //FOV 默认 70 范围 30-110
    public int Fov { get; set; } = 70;

    //Gamma 亮度 0-1 默认 0.5
    public float Gamma { get; set; } = 0.5f;

    //是否全屏
    public bool Fullscreen { get; set; }

    //是否启用 VSync
    public bool EnableVsync { get; set; } = true;

    //是否 demo 模式
    public bool Demo { get; set; }

    //语言代码默认 en_us
    public string Language { get; set; } = "en_us";

    //聊天可见度 0=隐藏 1=系统 2=全部
    public int ChatVisibility { get; set; } = 2;

    //鼠标灵敏度 0-2 默认 1
    public float MouseSensitivity { get; set; } = 1.0f;

    //主手 left/right
    public string MainHand { get; set; } = "right";

    //加载路径下的 options.txt 配置文件不存在返回默认配置
    public static GameConfig Load(string path)
    {
        var config = new GameConfig();
        if (!File.Exists(path)) return config;

        var props = new PropertiesConfig();
        props.Load(path);

        config.RenderDistance = props.GetInt("renderDistance", 12);
        config.Fov = props.GetInt("fov", 70);
        config.Gamma = props.GetFloat("gamma", 0.5f);
        config.Fullscreen = props.GetBool("fullscreen", false);
        config.EnableVsync = props.GetBool("enableVsync", true);
        config.Demo = props.GetBool("demo", false);
        config.Language = props.GetOrDefault("lang", "en_us");
        config.ChatVisibility = props.GetInt("chatVisibility", 2);
        config.MouseSensitivity = props.GetFloat("mouseSensitivity", 1.0f);
        config.MainHand = props.GetOrDefault("mainHand", "right");

        if (config.RenderDistance is < 2 or > 32) config.RenderDistance = 12;
        if (config.Fov is < 30 or > 110) config.Fov = 70;
        if (config.Gamma is < 0 or > 1) config.Gamma = 0.5f;

        return config;
    }

    //保存当前配置到 options.txt 覆盖已有文件
    public void Save(string path)
    {
        var props = new PropertiesConfig();
        props.SetInt("renderDistance", RenderDistance);
        props.SetInt("fov", Fov);
        props.Set("gamma", Gamma.ToString(System.Globalization.CultureInfo.InvariantCulture));
        props.SetBool("fullscreen", Fullscreen);
        props.SetBool("enableVsync", EnableVsync);
        props.SetBool("demo", Demo);
        props.Set("lang", Language);
        props.SetInt("chatVisibility", ChatVisibility);
        props.Set("mouseSensitivity", MouseSensitivity.ToString(System.Globalization.CultureInfo.InvariantCulture));
        props.Set("mainHand", MainHand);
        props.Save(path);
        Log.Info($"Saved client config to {path}");
    }
}
