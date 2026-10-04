using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using NetCraft.Game.Server;

namespace NetCraft.Server.Gui;

//ServerGuiApp Avalonia 应用 对应原版 showFrameFor 里建窗与挂收尾回调的那段
//potato 为真时开土豆彩蛋 只在 Windows 上生效 见 PotatoIcon
public sealed class ServerGuiApp(MinecraftServer server, bool potato = false) : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = ResolveThemeVariant();
    }

    //ResolveThemeVariant 主题变体默认跟随系统暗亮色
    //NETCRAFT_THEME 给了 light/dark 就按它强制 方便固定观感或临时排查
    private static ThemeVariant ResolveThemeVariant()
        => Environment.GetEnvironmentVariable("NETCRAFT_THEME")?.Trim().ToLowerInvariant() switch
        {
            "light" => ThemeVariant.Light,
            "dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new ServerWindow(server);
            desktop.MainWindow = window;
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            //关窗即关服 对应原版 windowClosing 里的 server.halt(true)
            window.Closed += (_, _) => server.Stop();
            //土豆彩蛋 窗口露出来之后平台句柄才有效 图标是异步拉的 拉不到就还用它原来的
            if (potato) window.Opened += (_, _) => PotatoIcon.Apply(window);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
