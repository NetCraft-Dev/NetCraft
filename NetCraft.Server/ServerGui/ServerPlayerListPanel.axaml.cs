using Avalonia.Controls;
using NetCraft.Game.Server;

namespace NetCraft.Server.Gui;

//ServerPlayerListPanel, player list panel, maps to vanilla net.minecraft.server.gui.PlayerListComponent
//Lists online player names only, vanilla is also a plain name list
public sealed partial class ServerPlayerListPanel : UserControl
{
    private readonly MinecraftServer _server;
    private List<string> _shown = new();

    public ServerPlayerListPanel(MinecraftServer server)
    {
        _server = server;
        InitializeComponent();
    }

    //Refresh does nothing when the content is unchanged, avoids resetting ItemsSource each time and shaking the scroll position
    public void Refresh()
    {
        var names = _server.PlayerList.Players.Select(player => player.Profile.Name).ToList();
        if (names.Count == _shown.Count && names.SequenceEqual(_shown)) return;
        _shown = names;
        Players.ItemsSource = names;
    }
}
