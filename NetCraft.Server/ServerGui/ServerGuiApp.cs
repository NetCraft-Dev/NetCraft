using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using NetCraft.Game.Server;

namespace NetCraft.Server.Gui;

//ServerGuiApp, the Avalonia application, maps to the window creation and shutdown callback part of vanilla showFrameFor
//When potato is true the potato easter egg turns on, only effective on Windows, see PotatoIcon
public sealed class ServerGuiApp(MinecraftServer server, bool potato = false) : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = ResolveThemeVariant();
    }

    //ResolveThemeVariant, the theme variant follows the system dark/light setting by default
    //If NETCRAFT_THEME is set to light/dark it is forced, handy for a fixed look or temporary debugging
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
            //Closing the window shuts down the server, maps to server.halt(true) in vanilla windowClosing
            window.Closed += (_, _) => server.Stop();
            //Potato easter egg, the platform handle is only valid after the window appears, the icon is fetched asynchronously and the original is kept if it fails
            if (potato) window.Opened += (_, _) => PotatoIcon.Apply(window);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
