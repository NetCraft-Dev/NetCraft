using NetCraft.Logging;

namespace NetCraft;

//GameConfig client options.txt config
//Simplified version of vanilla net.minecraft.client.Options
//Keeps only core rendering and gameplay fields: render distance, FOV, gamma, difficulty, etc.
//Vanilla has 100+ Option fields; NC's simplified version expands on demand
public sealed class GameConfig
{
    //Render distance in chunks, default 12
    public int RenderDistance { get; set; } = 12;

    //FOV default 70, range 30-110
    public int Fov { get; set; } = 70;

    //Gamma brightness 0-1, default 0.5
    public float Gamma { get; set; } = 0.5f;

    //Whether fullscreen
    public bool Fullscreen { get; set; }

    //Whether VSync is enabled
    public bool EnableVsync { get; set; } = true;

    //Whether demo mode
    public bool Demo { get; set; }

    //Language code, default en_us
    public string Language { get; set; } = "en_us";

    //Chat visibility 0=hidden 1=system 2=all
    public int ChatVisibility { get; set; } = 2;

    //Mouse sensitivity 0-2, default 1
    public float MouseSensitivity { get; set; } = 1.0f;

    //Main hand left/right
    public string MainHand { get; set; } = "right";

    //Load the options.txt at the given path; if it does not exist, return the default config
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

    //Save the current config to options.txt, overwriting the existing file
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
