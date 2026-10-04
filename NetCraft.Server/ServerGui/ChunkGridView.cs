using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using NetCraft.Primitives;

namespace NetCraft.Server.Gui;

//ChunkGridView 区块方格图 以某个区块为中心把周围已建持有器的区块按票等级涂色
//只画表里有的区块 不去遍历可见范围的每个坐标: 视距外本来就没有持有器 留背景色正好当"卸载"
//颜色只分两档 实体可 tick 一档 其余在加载范围内的一档 都不在就什么都不画
//中心是小数 拖动与跟随玩家都靠它连续移动 另有每格自己的过渡状态做加载与卸载的渐变
public sealed class ChunkGridView : Control
{
    //单格边长的取值范围 缩放就是在这个区间里改它
    public const double MinCellSize = 2;
    public const double MaxCellSize = 28;
    //格子小于这个尺寸时线比格还密 那时不画网格
    private const double GridLineThreshold = 4;
    //每帧朝目标推进的比例 0.25 时约 0.3 秒收敛 看着像匀速滑过去又不拖沓
    private const double EaseFactor = 0.25;
    //小于这个差距就当到位 免得无限逼近停不下来
    private const double Epsilon = 0.002;
    //每格每秒的淡入淡出推进量
    private const double FadeSpeed = 7;
    //强弱两档之间的插值级数 颜色按这个数分段 免得每格都新建一支画刷
    private const int ShadeCount = 8;

    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(16);

    public static readonly StyledProperty<double> CellSizeProperty =
        AvaloniaProperty.Register<ChunkGridView, double>(nameof(CellSize), 10);

    public static readonly StyledProperty<IBrush?> GridBackgroundProperty =
        AvaloniaProperty.Register<ChunkGridView, IBrush?>(nameof(GridBackground));

    public static readonly StyledProperty<IBrush?> GridLineProperty =
        AvaloniaProperty.Register<ChunkGridView, IBrush?>(nameof(GridLine));

    public static readonly StyledProperty<IBrush?> LoadedBrushProperty =
        AvaloniaProperty.Register<ChunkGridView, IBrush?>(nameof(LoadedBrush));

    public static readonly StyledProperty<IBrush?> WeakBrushProperty =
        AvaloniaProperty.Register<ChunkGridView, IBrush?>(nameof(WeakBrush));

    public static readonly StyledProperty<IBrush?> MarkerBrushProperty =
        AvaloniaProperty.Register<ChunkGridView, IBrush?>(nameof(MarkerBrush));

    //原点色 世界中心那一格上的固定标记
    public static readonly StyledProperty<IBrush?> OriginBrushProperty =
        AvaloniaProperty.Register<ChunkGridView, IBrush?>(nameof(OriginBrush));

    //_targets 目标快照 区块 -> 是否为强加载 过渡状态朝它收敛
    private readonly Dictionary<ChunkPos, bool> _targets = new();
    //_states 每格的过渡状态 进度管它从无到有 强度管它在黄绿之间挪
    private readonly Dictionary<ChunkPos, CellState> _states = new();
    //_players 玩家所在区块与名字 图上按名牌画在对应格里
    private readonly List<(ChunkPos Pos, string Name)> _players = new();
    //_hover 鼠标悬停的区块 为空表示指针不在图上 描边框跟着它走
    private ChunkPos? _hover;
    //_fading 本轮要丢掉的格 遍历字典时不能顺手删
    private readonly List<ChunkPos> _fading = new();

    private double _centerX;
    private double _centerZ;
    private double _targetCenterX;
    private double _targetCenterZ;

    //_timer 帧循环 有过渡要跑才开 收敛就停 静止时不占 CPU
    private DispatcherTimer? _timer;
    private bool _dragging;
    private Point _dragOrigin;
    private double _dragCenterX;
    private double _dragCenterZ;

    private sealed class CellState
    {
        //Progress 出场进度 0 是还没露面 1 是完全显示
        public double Progress;
        //Strength 强加载程度 0 是弱加载色 1 是加载色
        public double Strength;
    }

    static ChunkGridView()
    {
        //尺寸或任一画笔画笔变了都要重画 否则缩放时画面停在旧比例上
        AffectsRender<ChunkGridView>(CellSizeProperty, GridBackgroundProperty, GridLineProperty,
            LoadedBrushProperty, WeakBrushProperty, MarkerBrushProperty, OriginBrushProperty);
    }

    //CenterPanned 用户拖动地图后回调当前中心 页面据此退出跟随并同步手动中心
    public event Action<double, double>? CenterPanned;

    //HoverChanged 鼠标悬停的区块变化 带该格上的玩家名 指针离开时区块传空
    public event Action<ChunkPos?, IReadOnlyList<string>>? HoverChanged;

    //CurrentCenter 当前渲染中心 取消跟随或玩家离线时用它把画面钉在原地
    public (double X, double Z) CurrentCenter => (_centerX, _centerZ);

    //CellSize 单格边长像素
    public double CellSize
    {
        get => GetValue(CellSizeProperty);
        set => SetValue(CellSizeProperty, value);
    }

    public IBrush? GridBackground
    {
        get => GetValue(GridBackgroundProperty);
        set => SetValue(GridBackgroundProperty, value);
    }

    public IBrush? GridLine
    {
        get => GetValue(GridLineProperty);
        set => SetValue(GridLineProperty, value);
    }

    public IBrush? LoadedBrush
    {
        get => GetValue(LoadedBrushProperty);
        set => SetValue(LoadedBrushProperty, value);
    }

    public IBrush? WeakBrush
    {
        get => GetValue(WeakBrushProperty);
        set => SetValue(WeakBrushProperty, value);
    }

    public IBrush? MarkerBrush
    {
        get => GetValue(MarkerBrushProperty);
        set => SetValue(MarkerBrushProperty, value);
    }

    //OriginBrush 世界中心那一格上的固定标记色
    public IBrush? OriginBrush
    {
        get => GetValue(OriginBrushProperty);
        set => SetValue(OriginBrushProperty, value);
    }

    //SetSnapshot 换入一帧的目标快照 中心与格集都朝新值平滑过去
    //strong 为真表示该区块的票等级已到实体可 tick
    public void SetSnapshot(double centerX, double centerZ,
        IReadOnlyList<(ChunkPos Pos, bool Strong)> cells,
        IReadOnlyList<(ChunkPos Pos, string Name)> players)
    {
        //目标与上次一模一样就别开帧循环 静止时每半秒空转一帧也是白烧
        var changed = _targets.Count != cells.Count
            || Math.Abs(_targetCenterX - centerX) > 0.0001
            || Math.Abs(_targetCenterZ - centerZ) > 0.0001;
        if (!changed)
        {
            for (var i = 0; i < cells.Count; i++)
            {
                var (pos, strong) = cells[i];
                if (_targets.TryGetValue(pos, out var previous) && previous == strong) continue;
                changed = true;
                break;
            }
        }

        _targetCenterX = centerX;
        _targetCenterZ = centerZ;
        _targets.Clear();
        for (var i = 0; i < cells.Count; i++)
        {
            var (pos, strong) = cells[i];
            _targets[pos] = strong;
            //新格从全透明起步 强度也从弱加载那档起 于是先亮黄再转绿 正好是加载推进的顺序
            if (!_states.ContainsKey(pos)) _states[pos] = new CellState();
        }

        _players.Clear();
        for (var i = 0; i < players.Count; i++) _players.Add(players[i]);

        if (changed) EnsureAnimating();
        InvalidateVisual();
    }

    //EnsureAnimating 有过渡要跑才开帧循环 跑完自己停
    private void EnsureAnimating()
    {
        if (_timer is not null) return;
        _timer = new DispatcherTimer(FrameInterval, DispatcherPriority.Render, OnFrame);
        _timer.Start();
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var busy = Step();
        InvalidateVisual();
        if (busy) return;
        _timer?.Stop();
        _timer = null;
    }

    //Step 推进一步过渡 返回是否还需要继续
    private bool Step()
    {
        var busy = false;

        var dx = _targetCenterX - _centerX;
        var dz = _targetCenterZ - _centerZ;
        if (Math.Abs(dx) > Epsilon || Math.Abs(dz) > Epsilon)
        {
            _centerX += dx * EaseFactor;
            _centerZ += dz * EaseFactor;
            busy = true;
        }
        else if (_centerX != _targetCenterX || _centerZ != _targetCenterZ)
        {
            //收尾时直接对齐 免得停在差一点点的地方
            _centerX = _targetCenterX;
            _centerZ = _targetCenterZ;
        }

        var step = FadeSpeed * FrameInterval.TotalSeconds;
        _fading.Clear();
        foreach (var (pos, state) in _states)
        {
            if (_targets.TryGetValue(pos, out var strong))
            {
                if (state.Progress < 1)
                {
                    state.Progress = Math.Min(1, state.Progress + step);
                    busy = true;
                }
                var wanted = strong ? 1.0 : 0.0;
                if (Math.Abs(state.Strength - wanted) > Epsilon)
                {
                    state.Strength += (wanted - state.Strength) * EaseFactor;
                    busy = true;
                }
                else if (state.Strength != wanted) state.Strength = wanted;
            }
            else
            {
                state.Progress -= step;
                if (state.Progress <= 0) _fading.Add(pos);
                else busy = true;
            }
        }
        foreach (var pos in _fading) _states.Remove(pos);

        return busy;
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0) return;

        var background = GridBackground;
        if (background is not null) context.FillRectangle(background, new Rect(0, 0, width, height));

        var cell = Math.Max(CellSize, MinCellSize);
        var centerX = width / 2;
        var centerY = height / 2;

        DrawCells(context, cell, centerX, centerY, width, height);
        DrawGridLines(context, cell, centerX, centerY, width, height);
        DrawMarkers(context, cell, centerX, centerY, width, height);
    }

    //DrawCells 逐块填色 颜色按强度在两档之间插值 透明度按出场进度给 于是加载和卸载都是渐变的
    private void DrawCells(DrawingContext context, double cell, double centerX, double centerY,
        double width, double height)
    {
        if (WeakBrush is not ISolidColorBrush weak || LoadedBrush is not ISolidColorBrush loaded) return;

        //两档之间的颜色先分好级 一帧只建这几支画刷 不按格数建
        var shades = new SolidColorBrush[ShadeCount + 1];
        for (var i = 0; i <= ShadeCount; i++)
            shades[i] = new SolidColorBrush(Lerp(weak.Color, loaded.Color, (double)i / ShadeCount));

        foreach (var (pos, state) in _states)
        {
            var x = centerX + (pos.X - _centerX - 0.5) * cell;
            var y = centerY + (pos.Z - _centerZ - 0.5) * cell;
            if (x + cell < 0 || x > width || y + cell < 0 || y > height) continue;

            //出场从格心往外涨 消失再缩回去 比硬切自然
            var size = cell * (0.6 + 0.4 * state.Progress);
            var rect = new Rect(x + (cell - size) / 2, y + (cell - size) / 2, size, size);
            var brush = shades[(int)Math.Round(state.Strength * ShadeCount)];
            if (state.Progress >= 1)
            {
                context.FillRectangle(brush, rect);
                continue;
            }
            using (context.PushOpacity(state.Progress))
                context.FillRectangle(brush, rect);
        }
    }

    private void DrawGridLines(DrawingContext context, double cell, double centerX, double centerY,
        double width, double height)
    {
        var line = GridLine;
        if (line is null || cell < GridLineThreshold) return;
        var pen = new Pen(line, 1);
        //格边界落在 控件中心 + (格号 + 0.5 - 中心) * 格宽 上 随中心连续滑动 拖拽才跟手
        var firstColumn = (int)Math.Ceiling(-centerX / cell + _centerX - 0.5);
        var lastColumn = (int)Math.Floor((width - centerX) / cell + _centerX - 0.5);
        for (var k = firstColumn; k <= lastColumn; k++)
        {
            var x = Math.Round(centerX + (k + 0.5 - _centerX) * cell) + 0.5;
            context.DrawLine(pen, new Point(x, 0), new Point(x, height));
        }
        var firstRow = (int)Math.Ceiling(-centerY / cell + _centerZ - 0.5);
        var lastRow = (int)Math.Floor((height - centerY) / cell + _centerZ - 0.5);
        for (var k = firstRow; k <= lastRow; k++)
        {
            var y = Math.Round(centerY + (k + 0.5 - _centerZ) * cell) + 0.5;
            context.DrawLine(pen, new Point(0, y), new Point(width, y));
        }
    }

    //DrawMarkers 鼠标悬停的那一格描边 玩家所在格摆名牌 世界原点格点一个红点
    //描边框早先钉在渲染中心上 现在跟着指针走 指哪一格就框哪一格
    private void DrawMarkers(DrawingContext context, double cell, double centerX, double centerY,
        double width, double height)
    {
        var marker = MarkerBrush;
        if (marker is not null)
        {
            if (_hover is { } hover)
            {
                var pen = new Pen(marker, 2);
                var markX = centerX + (hover.X - _centerX - 0.5) * cell;
                var markY = centerY + (hover.Z - _centerZ - 0.5) * cell;
                context.DrawRectangle(null, pen, new Rect(markX, markY, cell, cell));
            }

            //玩家不画方块点了 直接在所在格摆名牌 格子小到放不下字时退回小方块
            var fontSize = Math.Clamp(cell * 0.42, 8, 14);
            foreach (var (pos, name) in _players)
            {
                var x = centerX + (pos.X - _centerX) * cell;
                var y = centerY + (pos.Z - _centerZ) * cell;
                if (x < 0 || x > width || y < 0 || y > height) continue;
                if (cell < 16)
                {
                    var size = Math.Clamp(cell * 0.5, 2, 8);
                    context.FillRectangle(marker, new Rect(x - size / 2, y - size / 2, size, size));
                    continue;
                }
                //两个 FormattedText 同名 用全限定名 这里要的是 Avalonia 画字那个
                var label = new Avalonia.Media.FormattedText(name, CultureInfo.CurrentCulture,
                    Avalonia.Media.FlowDirection.LeftToRight, Typeface.Default, fontSize, marker);
                var box = new Rect(x - label.Width / 2 - 3, y - label.Height / 2 - 1,
                    label.Width + 6, label.Height + 2);
                if (GridBackground is { } background) context.FillRectangle(background, box);
                context.DrawText(label, new Point(box.X + 3, box.Y + 1));
            }
        }

        //世界中心那一格 位置不随渲染中心走 缩放到底时也得看得见所以半径带下限
        var origin = OriginBrush;
        if (origin is null) return;
        var originX = centerX - _centerX * cell;
        var originY = centerY - _centerZ * cell;
        if (originX < -cell || originX > width + cell || originY < -cell || originY > height + cell) return;
        var radius = Math.Clamp(cell * 0.22, 1.5, 3.5);
        context.DrawEllipse(origin, null, new Point(originX, originY), radius, radius);
    }

    //UpdateHover 按指针位置换算所在区块 变了才描边重画并通知页面
    private void UpdateHover(Point position, double cell)
    {
        var x = (int)Math.Floor((position.X - Bounds.Width / 2) / cell + _centerX + 0.5);
        var z = (int)Math.Floor((position.Y - Bounds.Height / 2) / cell + _centerZ + 0.5);
        var pos = new ChunkPos(x, z);
        if (_hover == pos) return;
        _hover = pos;
        InvalidateVisual();
        HoverChanged?.Invoke(pos, NamesAt(pos));
    }

    //ClearHover 指针离开后撤掉描边
    private void ClearHover()
    {
        if (_hover is null) return;
        _hover = null;
        InvalidateVisual();
        HoverChanged?.Invoke(null, Array.Empty<string>());
    }

    //NamesAt 该区块上的玩家名 供悬停提示用
    private IReadOnlyList<string> NamesAt(ChunkPos pos)
    {
        List<string>? names = null;
        foreach (var (playerPos, name) in _players)
            if (playerPos == pos) (names ??= new List<string>()).Add(name);
        return names ?? (IReadOnlyList<string>)Array.Empty<string>();
    }

    //OnPointerPressed 按下左键开始拖动地图
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _dragging = true;
        _dragOrigin = e.GetPosition(this);
        _dragCenterX = _centerX;
        _dragCenterZ = _centerZ;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    //OnPointerMoved 拖动时地图跟着手走 中心往反方向挪 这里不插值 要的就是跟手
    //指针停在图上就更新悬停格 描边框与页面提示都跟着它
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var cell = Math.Max(CellSize, MinCellSize);
        var now = e.GetPosition(this);
        if (_dragging)
        {
            var x = _dragCenterX - (now.X - _dragOrigin.X) / cell;
            var z = _dragCenterZ - (now.Y - _dragOrigin.Y) / cell;
            _centerX = x;
            _centerZ = z;
            _targetCenterX = x;
            _targetCenterZ = z;
            InvalidateVisual();
            CenterPanned?.Invoke(x, z);
            e.Handled = true;
        }
        //拖动会改中心 悬停格要在新中心上算 否则会慢一拍
        UpdateHover(now, cell);
    }

    //OnPointerExited 指针离开控件撤掉悬停描边 拖动中被捕获到外面时先留着
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_dragging) return;
        ClearHover();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging) return;
        _dragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = false;
    }

    //离开可视树就停表 免得页面切走之后还有个计时器在空转
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Stop();
        _timer = null;
    }

    //Lerp 两个颜色之间按比例取中点
    private static Color Lerp(Color from, Color to, double t)
        => Color.FromArgb(
            (byte)(from.A + (to.A - from.A) * t),
            (byte)(from.R + (to.R - from.R) * t),
            (byte)(from.G + (to.G - from.G) * t),
            (byte)(from.B + (to.B - from.B) * t));
}
