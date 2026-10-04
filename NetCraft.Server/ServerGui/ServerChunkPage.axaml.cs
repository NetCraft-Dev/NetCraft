using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using NetCraft.Game;
using NetCraft.Game.Server;
using NetCraft.Primitives;
using NetCraft.Storage;

namespace NetCraft.Server.Gui;

//ServerChunkPage 区块页 左边是生成任务与玩家名单 右边是以某个区块为中心的方格图
//图只覆盖已建持有器的区块 视距外没有持有器 不画就是卸载态
//中心来源有三处: 默认出生点、跟随某个玩家、手动跳转或拖动 同一时刻只有跟随会自己动
public sealed partial class ServerChunkPage : UserControl
{
    //每按一档缩放改变的格边长像素
    private const double ZoomStep = 2;
    //列表项淡入淡出的时长
    private const int FadeMilliseconds = 200;

    private readonly MinecraftServer _server;
    //_followName 正在跟随的玩家 为空表示中心由 _manualX/_manualZ 决定
    //点玩家开始跟随 再点一次、拖动地图或跳到指定区块都会退出跟随
    private string? _followName;
    //_manualX/_manualZ 手动中心 跟随期间会顺手记下目标玩家所在区块 于是他一掉线中心正好停在原地
    private double _manualX;
    private double _manualZ;
    private bool _centerReady;

    //_shownPlayers/_shownSelected 上一次画出来的玩家名单与跟随对象
    //两者都没变就不重建按钮 否则每 500ms 重建一次会把悬停状态抖掉
    private List<string> _shownPlayers = new();
    private string? _shownSelected;
    //_taskItems 生成任务项 按坐标字符串复用 只动真正增删的那几项
    private readonly Dictionary<string, TextBlock> _taskItems = new();

    //_cells/_players 复用同一份缓冲 每帧只清空重填 不重新分配
    private readonly List<(ChunkPos Pos, bool Strong)> _cells = new();
    private readonly List<(ChunkPos Pos, string Name)> _players = new();

    public ServerChunkPage(MinecraftServer server)
    {
        _server = server;
        InitializeComponent();

        Focusable = true;
        //滚轮管缩放 但只有按下 Ctrl 才算
        ChunkGrid.PointerWheelChanged += OnWheel;
        //键盘缩放要先有点击把焦点拿过来
        ChunkGrid.PointerPressed += (_, _) => Focus();
        //拖动地图要退出跟随 顺手把中心同步回页面
        ChunkGrid.CenterPanned += OnCenterPanned;
        ChunkGrid.HoverChanged += OnHoverChanged;
        KeyDown += OnKeyDown;

        GotoButton.Click += OnGoto;
        GotoX.KeyDown += OnGotoKey;
        GotoZ.KeyDown += OnGotoKey;

        UpdateScaleLabel();
    }

    //Refresh 由窗口的 500ms 轮询驱动 与原版统计面板同频
    public void Refresh()
    {
        //主循环没起来时关卡还不存在 这页先留空
        if (!_server.Running) return;

        //第一次拿到出生点才定初始中心 之后中心只由跟随与手动操作改
        if (!_centerReady)
        {
            var spawn = _server.SpawnPos;
            _manualX = ToChunk(spawn.X);
            _manualZ = ToChunk(spawn.Z);
            _centerReady = true;
        }

        DropOfflineFollow();
        RebuildPlayerList();
        RebuildGenerationList();

        var source = _server.Overworld.ChunkSource;
        var (centerX, centerZ) = ResolveCenter();
        FillCells(source);
        ChunkGrid.SetSnapshot(centerX, centerZ, _cells, _players);

        //把持有与绘制数摆在明面上 图上空白时一眼能分出是没数据还是没画出来
        CenterLabel.Text = Loc.Format("netcraft.gui.chunk.center",
            Math.Round(centerX), Math.Round(centerZ), source.HoldersCount, _cells.Count);
    }

    //OnWheel Ctrl 加滚轮缩放
    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.Delta.Y == 0) return;
        Zoom(e.Delta.Y > 0 ? ZoomStep : -ZoomStep);
        e.Handled = true;
    }

    //OnKeyDown Ctrl 加方向键或加减号缩放
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        switch (e.Key)
        {
            case Key.Up:
            case Key.OemPlus:
            case Key.Add:
                Zoom(ZoomStep);
                e.Handled = true;
                break;
            case Key.Down:
            case Key.OemMinus:
            case Key.Subtract:
                Zoom(-ZoomStep);
                e.Handled = true;
                break;
        }
    }

    private void Zoom(double delta)
    {
        var next = Math.Clamp(ChunkGrid.CellSize + delta, ChunkGridView.MinCellSize, ChunkGridView.MaxCellSize);
        if (Math.Abs(next - ChunkGrid.CellSize) < 0.001) return;
        ChunkGrid.CellSize = next;
        UpdateScaleLabel();
    }

    private void UpdateScaleLabel()
        => ScaleLabel.Text = Loc.Format("netcraft.gui.chunk.scale",
            ChunkGrid.CellSize.ToString("0.#", CultureInfo.CurrentCulture));

    //OnGoto 跳到指定区块 输入的是区块坐标 和左边任务列表方括号里的数字一致
    private void OnGoto(object? sender, RoutedEventArgs e)
    {
        //两个框都填对了才动 否则光标还停在半截数字上 直接跳走会莫名其妙
        if (!int.TryParse(GotoX.Text?.Trim(), out var x)) return;
        if (!int.TryParse(GotoZ.Text?.Trim(), out var z)) return;
        _followName = null;
        _manualX = x;
        _manualZ = z;
        Refresh();
    }

    //OnGotoKey 坐标框里回车等同于点前往
    private void OnGotoKey(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        OnGoto(sender, e);
        e.Handled = true;
    }

    //OnHoverChanged 悬停格变化时刷新信息带 格里站着玩家就一并报名字
    private void OnHoverChanged(ChunkPos? pos, IReadOnlyList<string> names)
    {
        if (pos is not { } chunk)
        {
            HoverLabel.Text = string.Empty;
            return;
        }
        HoverLabel.Text = names.Count == 0
            ? Loc.Format("netcraft.gui.chunk.hover", chunk.X, chunk.Z)
            : Loc.Format("netcraft.gui.chunk.hover_players", chunk.X, chunk.Z, string.Join(", ", names));
    }

    //OnCenterPanned 拖动地图改中心 既然要自己看就说明不想再跟着谁 顺手退出跟随
    private void OnCenterPanned(double x, double z)
    {
        _manualX = x;
        _manualZ = z;
        if (_followName is null) return;
        _followName = null;
        //按钮上的选中标记要跟着撤掉 名字没变时这行会自己短路
        RebuildPlayerList();
    }

    //ResolveCenter 跟随中的玩家在线就取它所在区块 顺手记下来 万一它掉线中心正好停在这一格
    private (double X, double Z) ResolveCenter()
    {
        if (_followName is not null)
        {
            var player = _server.PlayerList.Players.FirstOrDefault(p => p.Profile.Name == _followName);
            if (player is not null)
            {
                _manualX = ToChunk(player.Position.X);
                _manualZ = ToChunk(player.Position.Z);
            }
        }
        return (_manualX, _manualZ);
    }

    //DropOfflineFollow 跟随的玩家不在了就撤掉跟随 中心留在 _manualX/_manualZ 上不再动
    private void DropOfflineFollow()
    {
        if (_followName is null) return;
        if (_server.PlayerList.Players.Any(p => p.Profile.Name == _followName)) return;
        _followName = null;
    }

    private void RebuildPlayerList()
    {
        var players = _server.PlayerList.Players;
        var names = players.Select(p => p.Profile.Name).ToList();
        if (names.SequenceEqual(_shownPlayers) && _followName == _shownSelected) return;
        _shownPlayers = names;
        _shownSelected = _followName;

        PlayerList.Children.Clear();
        foreach (var player in players)
        {
            var name = player.Profile.Name;
            var toggle = new ToggleButton
            {
                Content = name,
                IsChecked = name == _followName,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Classes = { "playerToggle" },
            };
            var captured = player;
            toggle.Click += (_, _) => SelectPlayer(captured);
            PlayerList.Children.Add(toggle);
        }
    }

    //SelectPlayer 点玩家开始跟随 点同一个就是取消 取消后中心停在当前画面上不再追
    private void SelectPlayer(ServerPlayer player)
    {
        var name = player.Profile.Name;
        if (_followName == name)
        {
            //定在原地取的是当前渲染中心而不是玩家位置 否则取消的瞬间画面还要再往前滑一下
            var (x, z) = ChunkGrid.CurrentCenter;
            _manualX = x;
            _manualZ = z;
            _followName = null;
        }
        else _followName = name;

        //重建放到这次点击派发完之后 否则正在处理点击的那个按钮会被自己清掉
        Dispatcher.UIThread.Post(() =>
        {
            RebuildPlayerList();
            Refresh();
        }, DispatcherPriority.Background);
    }

    //RebuildGenerationList 列出已提交加载但还没完成的区块 这些就是正在跑的地形生成
    private void RebuildGenerationList()
    {
        var source = _server.Overworld.ChunkSource;
        var tasks = new List<string>();
        foreach (var holder in source.Holders)
        {
            if (!holder.WasScheduled || holder.IsDone) continue;
            tasks.Add($"[{holder.Pos.X}, {holder.Pos.Z}]");
        }
        tasks.Sort(StringComparer.Ordinal);

        GenerationSummary.Text = Loc.Format("netcraft.gui.chunk.generating",
            tasks.Count, source.LoadedCount, source.HoldersCount, source.ViewDistance);

        SyncTaskItems(tasks);
    }

    //SyncTaskItems 按新列表增量维护任务项 只动真正增删的那几个
    //整列清空重建会让没变的项跟着闪一下 动画反而更乱
    private void SyncTaskItems(List<string> tasks)
    {
        var wanted = new HashSet<string>(tasks);
        //消失的项先淡出 等过渡跑完再摘掉 直接移除是瞬间消失
        var stale = _taskItems.Keys.Where(key => !wanted.Contains(key)).ToList();
        foreach (var key in stale)
        {
            var item = _taskItems[key];
            _taskItems.Remove(key);
            FadeOut(item);
        }

        for (var i = 0; i < tasks.Count; i++)
        {
            var key = tasks[i];
            if (_taskItems.TryGetValue(key, out var existing))
            {
                //顺序变了就挪位置 挪过的项本身不动 只有旁边的项跟着让位
                var at = GenerationList.Children.IndexOf(existing);
                if (at != i) GenerationList.Children.Move(at, i);
                continue;
            }

            var item = new TextBlock { Text = key, Classes = { "monoValue" }, Opacity = 0 };
            ApplyFade(item);
            _taskItems[key] = item;
            GenerationList.Children.Insert(Math.Min(i, GenerationList.Children.Count), item);
            //先以全透明进树 下一拍再提上来 过渡才会真的跑
            Dispatcher.UIThread.Post(() => item.Opacity = 1, DispatcherPriority.Background);
        }
    }

    //ApplyFade 让透明度变化走过渡而不是瞬切
    private static void ApplyFade(Control item)
        => item.Transitions = new Transitions
        {
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = TimeSpan.FromMilliseconds(FadeMilliseconds),
            },
        };

    //FadeOut 淡出后再摘掉 等待时间比过渡略长 免得动画没跑完就被摘走
    private static void FadeOut(Control item)
    {
        item.Opacity = 0;
        DispatcherTimer.RunOnce(() =>
        {
            if (item.Parent is Panel panel) panel.Children.Remove(item);
        }, TimeSpan.FromMilliseconds(FadeMilliseconds + 60));
    }

    //FillCells 把持有器表转成图上的一格格 票等级落在可 tick 档之外的不画
    //只画方块可 tick 及以内: 更外层的持有器是加载范围的外圈 图上按未加载显示
    private void FillCells(ServerChunkCache source)
    {
        _cells.Clear();
        foreach (var holder in source.Holders)
        {
            if (!ChunkLevel.IsBlockTicking(holder.TicketLevel)) continue;
            //强弱按模拟等级分: 模拟距离之外只加载不推进 就是那一圈弱加载
            _cells.Add((holder.Pos, source.InEntityTickingRange(holder.Pos.Pack())));
        }

        _players.Clear();
        foreach (var player in _server.PlayerList.Players)
            _players.Add((new ChunkPos((int)ToChunk(player.Position.X), (int)ToChunk(player.Position.Z)),
                player.Profile.Name));
    }

    //ToChunk 世界坐标到区块坐标 先取整再右移 负数才会落进正确的那一格
    private static double ToChunk(double value) => (int)Math.Floor(value) >> 4;
}
