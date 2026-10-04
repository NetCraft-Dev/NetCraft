using Avalonia.Controls;
using NetCraft.Game.Server;

namespace NetCraft.Server.Gui;

//ServerPlayerListPanel 玩家列表面板 对应原版 net.minecraft.server.gui.PlayerListComponent
//只列在线玩家名 原版也是一张纯名单表
public sealed partial class ServerPlayerListPanel : UserControl
{
    private readonly MinecraftServer _server;
    private List<string> _shown = new();

    public ServerPlayerListPanel(MinecraftServer server)
    {
        _server = server;
        InitializeComponent();
    }

    //Refresh 与上一次内容相同就不动 免得每次重设 ItemsSource 把滚动位置抖掉
    public void Refresh()
    {
        var names = _server.PlayerList.Players.Select(player => player.Profile.Name).ToList();
        if (names.Count == _shown.Count && names.SequenceEqual(_shown)) return;
        _shown = names;
        Players.ItemsSource = names;
    }
}
