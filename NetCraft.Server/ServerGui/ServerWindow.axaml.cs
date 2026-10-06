using Avalonia.Controls;
using Avalonia.Threading;
using NetCraft.Game.Server;

namespace NetCraft.Server.Gui;

//ServerWindow, the server main window, maps to the 854x480 layout of vanilla MinecraftServerGui
//Tab and card positions are in the axaml, this only hosts the panels and polls state every 500ms
public sealed partial class ServerWindow : Window
{
    private const string ShutdownTitle = "Minecraft server - shutting down!";
    //Vanilla stats and player list both tick every 500ms
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    private readonly MinecraftServer _server;
    //_store a buffer shared by the two log views, built before the panels since they take a history snapshot at construction
    private readonly LogStore _store;
    private readonly ServerStatsPanel _stats;
    private readonly ServerPlayerListPanel _players;
    private readonly ServerLogPanel _log;
    private readonly ServerLogPage _logPage;
    private readonly ServerChunkPage _chunkPage;
    private readonly DispatcherTimer _poll;
    //Only counts once the server has actually run, Running is still false during startup and the window must not close on that
    private bool _sawRunning;

    public ServerWindow(MinecraftServer server)
    {
        _server = server;
        InitializeComponent();

        _store = new LogStore();
        _store.Attach();
        _stats = new ServerStatsPanel(server);
        _players = new ServerPlayerListPanel(server);
        _log = new ServerLogPanel(server, _store);
        _logPage = new ServerLogPage(_store);
        _chunkPage = new ServerChunkPage(server);
        StatsCard.Child = _stats;
        PlayersCard.Child = _players;
        LogCard.Child = _log;
        LogPageHost.Content = _logPage;
        ChunkPageHost.Content = _chunkPage;

        _poll = new DispatcherTimer { Interval = PollInterval };
        _poll.Tick += (_, _) => Poll();
        _poll.Start();

        //Refresh immediately on tab switch, otherwise content only appears on the next tick
        Tabs.SelectionChanged += (_, _) => RefreshNow();
        RefreshNow();
    }

    private void Poll()
    {
        RefreshNow();
        //When the server main loop exits (for example console Ctrl+C) the window wraps up by itself, maps to the cleanup after vanilla halt
        if (_server.Running)
        {
            _sawRunning = true;
            return;
        }
        if (!_sawRunning) return;
        _poll.Stop();
        Title = ShutdownTitle;
        _log.Detach();
        _logPage.Detach();
        _store.Detach();
        Close();
    }

    private void RefreshNow()
    {
        //The chunk page only walks a few hundred holders, cheap enough to not filter by current page
        //Comparing SelectedItem can misfire and become "watching but never refreshing", not worth the risk for this little cost
        _chunkPage.Refresh();
        _stats.Refresh();
        _players.Refresh();
        //If the main loop is not up, InitServer has not finished, and the command box must not accept input
        _log.SetInputEnabled(_server.Running);
    }
}
