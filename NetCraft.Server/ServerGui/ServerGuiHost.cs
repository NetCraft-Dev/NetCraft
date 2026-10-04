using Avalonia;
using Avalonia.Fonts.Inter;
using NetCraft.Game.Server;

namespace NetCraft.Server.Gui;

//ServerGuiHost 服务端图形界面宿主
//对应原版 net.minecraft.server.gui.MinecraftServerGui.showFrameFor 的装配部分
//主线程跑 Avalonia 消息循环直到窗口关闭才返回 服务端主循环由调用方放在后台线程
public static class ServerGuiHost
{
    //Run 启动 GUI 并阻塞到窗口关闭
    //server 已构造好的服务端实例 窗口只读它的状态 不接管其生命周期
    public static void Run(MinecraftServer server, string[] args)
    {
        //--potato 彩蛋开关 非 Windows 上 PotatoIcon 内部会直接跳过
        var potato = Array.IndexOf(args, "--potato") >= 0;
        AppBuilder.Configure(() => new ServerGuiApp(server, potato))
            .UsePlatformDetect()
            .WithInterFont()
            .StartWithClassicDesktopLifetime(args);
    }
}
