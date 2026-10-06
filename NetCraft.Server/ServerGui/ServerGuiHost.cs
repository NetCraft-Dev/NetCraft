using Avalonia;
using Avalonia.Fonts.Inter;
using NetCraft.Game.Server;

namespace NetCraft.Server.Gui;

//ServerGuiHost, the server GUI host
//Maps to the assembly part of vanilla net.minecraft.server.gui.MinecraftServerGui.showFrameFor
//The main thread runs the Avalonia message loop and returns only when the window closes, the caller puts the server main loop on a background thread
public static class ServerGuiHost
{
    //Run starts the GUI and blocks until the window closes
    //server is a fully constructed server instance, the window only reads its state and does not take over its lifecycle
    public static void Run(MinecraftServer server, string[] args)
    {
        //--potato easter egg toggle, PotatoIcon skips internally on non-Windows
        var potato = Array.IndexOf(args, "--potato") >= 0;
        AppBuilder.Configure(() => new ServerGuiApp(server, potato))
            .UsePlatformDetect()
            .WithInterFont()
            .StartWithClassicDesktopLifetime(args);
    }
}
