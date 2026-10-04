using Avalonia.Controls;
using Avalonia.Threading;
using NetCraft.Game.Server;
using NetCraft.Logging;

namespace NetCraft.Server.Gui;

//ServerWindow 服务端主窗口 对应原版 MinecraftServerGui 的 854x480 布局
//标签页与卡片的位置在 axaml 里 这里只管把面板装进去与 500ms 一拍的状态轮询
public sealed partial class ServerWindow : Window
{
    private const string ShutdownTitle = "Minecraft server - shutting down!";
    //JitTabIndex JIT 页在标签栏里的序号
    private const int JitTabIndex = 1;
    //原版统计与玩家列表都是 500ms 一拍
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    private readonly MinecraftServer _server;
    //_store 两个日志视图共用的一份缓冲 先建它再建面板 面板构造时要拿历史快照
    private readonly LogStore _store;
    private readonly ServerStatsPanel _stats;
    private readonly ServerPlayerListPanel _players;
    private readonly ServerLogPanel _log;
    private readonly ServerLogPage _logPage;
    private readonly ServerChunkPage _chunkPage;
    //JIT 页只在 --debug 下挂出来 常规模式没有这个面板
    private readonly ServerJitPanel? _jit;
    private readonly DispatcherTimer _poll;
    //服务端跑起来过才算数 启动阶段 Running 还是假 不能据此关窗
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
        //看编译层级属于排查手段 常规模式不挂这一页
        if (Log.DebugEnabled)
        {
            _jit = new ServerJitPanel();
            JitHost.Content = _jit;
        }
        else
        {
            Tabs.Items.Remove(JitTab);
        }

        _poll = new DispatcherTimer { Interval = PollInterval };
        _poll.Tick += (_, _) => Poll();
        _poll.Start();

        //切页时立刻刷一次 否则要等下一拍才有内容
        Tabs.SelectionChanged += (_, _) => RefreshNow();
        RefreshNow();
    }

    private void Poll()
    {
        RefreshNow();
        //服务端主循环退出(例如控制台 Ctrl+C)时窗口自行收尾 对应原版 halt 后的收尾
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
        //当前页看不见就不刷它的重活 JIT 表要遍历几千个方法
        if (_jit is not null && Tabs.SelectedIndex == JitTabIndex)
        {
            _jit.Refresh();
            return;
        }
        //区块页只遍历几百个持有器 开销小 不按当前页过滤
        //按 SelectedItem 比对一旦对不上就成了"明明在看却一直不刷"的哑火 不值得为这点开销冒险
        _chunkPage.Refresh();
        _stats.Refresh();
        _players.Refresh();
        //主循环没起来就说明 InitServer 还没跑完 这时命令框不能收输入
        _log.SetInputEnabled(_server.Running);
    }
}
