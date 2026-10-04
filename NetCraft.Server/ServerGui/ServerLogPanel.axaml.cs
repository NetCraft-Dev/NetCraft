using System;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NetCraft.Game;
using NetCraft.Game.Commands;
using NetCraft.Game.Server;
using NetCraft.Logging;

namespace NetCraft.Server.Gui;

//ServerLogPanel 日志与命令面板 对应原版 MinecraftServerGui 的 "Log and chat"
//上方只读日志区 下方命令输入框 回车按控制台源执行
public sealed partial class ServerLogPanel : UserControl
{
    //日志区最多保留的行数 与历史缓存对齐 开服那一段才留得住
    //超出从头部丢 放任增长会把内存与文本布局拖垮
    private const int MaxLines = 20000;
    //每帧放行一小批日志 逐条往外淌看着是连续的 而不是几百行一起砸下来
    private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(16);
    //单次放行的行数上限 积压再多也留到下一帧 免得一帧插入上千个控件把界面顶住
    private const int MaxPerFlush = 120;
    //放行行数不超过这个值才做入场动画 积压时动画只会帮倒忙
    private const int AnimatedBatchLimit = 6;
    //待放行日志的积压上限 界面只留一千行 攒得再多也只是白占内存
    private const int MaxPending = 4000;
    //底部的容差 小于它就算贴着
    private const double BottomTolerance = 4;

    private readonly MinecraftServer _server;
    private readonly LogStore _store;
    //_pending 日志由输出线程写 UI 线程读 出队用 Queue 摊平成 O(1)
    private readonly Queue<LogStore.Entry> _pending = new();
    private readonly Lock _pendingLock = new();
    private readonly DispatcherTimer _flush;
    //_view 列表绑定源 只在构造时挂一次 整体换 ItemsSource 会把容器全部重建
    private readonly RangeObservableCollection<LogRow> _view = new();
    //_follow 是否粘着最新一行 只在用户真的挪了视口时改
    //每帧现算 Extent/Offset 是不行的: 新日志一插进来 Extent 就变大而 Offset 不动
    //只要有一帧判成"不在底部"就再也回不来 用户不动 Offset 而 Extent 一直涨 于是一路被甩开
    private bool _follow = true;
    //_scroll 内部滚动条 由 ScrollChanged 惰性拿到 不在每帧去遍历可视树
    private ScrollViewer? _scroll;
    private double _lastOffsetY;

    //Scroll 取内部滚动条 拿不到时才去可视树里翻一次并缓存
    //挂进可视树之前翻不到 那时保持 null 下一次用到再翻
    private ScrollViewer? Scroll
        => _scroll ??= LogLines.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();

    public ServerLogPanel(MinecraftServer server, LogStore store)
    {
        _server = server;
        _store = store;
        InitializeComponent();

        CommandInput.KeyDown += OnInputKeyDown;
        LogLines.ItemsSource = _view;
        //挂在 ListBox 上接内部滚动条冒泡上来的事件 免得到 Loaded 里再去树里翻
        //handledEventsToo: 万一滚动条那边把事件标了 handled 也要收到 收不到就退化成永远跟随
        LogLines.AddHandler(ScrollViewer.ScrollChangedEvent, OnScrollChanged,
            RoutingStrategies.Bubble, handledEventsToo: true);

        //日志由输出线程发出 回调里只入队 落到控件上的动作统一交给下面这个定时器
        //先补看订阅之前发生的日志再订阅 内核初始化与资源加载阶段的日志只有缓冲里有
        LoadHistory();
        _store.LineAdded += OnLogLine;
        //历史在构造期填入 那会儿还没测量 直接滚到底是空操作
        //等布局跑完再排一次 否则开窗停在最老的一行
        LogLines.Loaded += (_, _) =>
            Dispatcher.UIThread.Post(ScrollToEnd, DispatcherPriority.Loaded);
        _flush = new DispatcherTimer { Interval = FlushInterval };
        _flush.Tick += (_, _) => FlushPending();
        _flush.Start();
    }

    //SetInputEnabled 服务端就绪前禁掉命令框
    //GUI 比 Done 先显示 这时敲命令会撞上还在初始化的世界 半截状态下执行结果不可预期
    public void SetInputEnabled(bool enabled) => CommandInput.IsEnabled = enabled;

    //LoadHistory 把订阅之前写出的日志补进日志区 之后走订阅实时路径
    private void LoadHistory()
    {
        var rows = new List<LogRow>();
        foreach (var entry in _store.Snapshot()) rows.Add(MakeRow(entry, animate: false));
        //历史条数可能超过面板上限 先按同一规则截掉头部 再一次性铺进去
        //这一次是整体换内容 发一条 Reset 让列表重建是合适的 逐条加反而白跑几千次容器逻辑
        if (rows.Count > MaxLines) rows.RemoveRange(0, rows.Count - MaxLines);
        _view.ReplaceAll(rows);
    }

    //Detach 解订阅日志 窗口关闭时调 否则这个面板会被日志系统一直引用着
    public void Detach()
    {
        _store.LineAdded -= OnLogLine;
        _flush.Stop();
    }

    private void OnLogLine(LogStore.Entry entry)
    {
        lock (_pendingLock)
        {
            //积压到顶就丢最老的 日志暴涨时保最新的一批才有意义
            if (_pending.Count >= MaxPending) _pending.Dequeue();
            _pending.Enqueue(entry);
        }
    }

    //FlushPending 每帧把积压的日志放行一小批 逐条建控件而不是整块重设文本
    //放行量随积压伸缩 追得上暴涨的日志 又能让常规速率下的输出看起来是连续的
    private void FlushPending()
    {
        List<LogStore.Entry> batch;
        lock (_pendingLock)
        {
            if (_pending.Count == 0) return;
            var take = Math.Clamp(_pending.Count / 8, 1, MaxPerFlush);
            batch = new List<LogStore.Entry>(take);
            for (var i = 0; i < take; i++) batch.Add(_pending.Dequeue());
        }
        //跟不跟随由 _follow 说了算 它只被用户的实际滚动动作改过 见 OnScrollChanged
        var animate = batch.Count <= AnimatedBatchLimit;
        //同批一次通知带完 逐条加会让列表为每一行各跑一遍容器逻辑
        var rows = new List<LogRow>(batch.Count);
        foreach (var entry in batch) rows.Add(MakeRow(entry, animate));
        _view.AddRange(rows);
        TrimToLimit();
        //粘着底部才继续跟随 与原版那条 shouldScroll 判定等价
        if (_follow) ScrollToEnd();
    }

    //TrimToLimit 超出行数上限的从头部整批裁掉
    //逐条 RemoveAt(0) 每条都要搬一次数组并各发一次通知 整批裁只发一次
    private void TrimToLimit()
    {
        var over = _view.Count - MaxLines;
        if (over > 0) _view.RemoveRange(0, over);
    }

    //ScrollToEnd 滚到最新一行
    //已经贴着底部就不再动: 每帧强滚一次本身就是抖动源 虚拟化下滚动还会顺带带出一轮布局
    //拿不到滚动条时退回 ScrollIntoView 列表还没挂进可视树时走这一支
    private void ScrollToEnd()
    {
        if (Scroll is not { } scroll)
        {
            if (_view.Count > 0) LogLines.ScrollIntoView(_view[^1]);
            return;
        }
        var target = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
        if (target - scroll.Offset.Y < 0.5) return;
        scroll.Offset = new Vector(scroll.Offset.X, target);
    }

    //OnScrollChanged 视口真的被挪动时更新跟随状态
    //先按 Extent/Viewport 的增量把被动挪动滤掉: 插入新行或裁掉旧行都会让 Extent 变
    //那种变化会把 Offset 一起带偏 跟用户拖视口是两回事 混在一起判等于自己把自己的跟随关掉
    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (e.Source is ScrollViewer scroll) _scroll = scroll;
        if (_scroll is null) return;
        if (Math.Abs(e.ExtentDelta.Y) > 0.5 || Math.Abs(e.ViewportDelta.Y) > 0.5) return;
        var y = _scroll.Offset.Y;
        if (Math.Abs(y - _lastOffsetY) < 0.5) return;
        //到底部(或接近底部)一律恢复跟随 用户往上滚就停止 其余情况(程序往下滚)保持原状
        //不能写成"没到底就置假": 滚动定位落到的位置未必正好是最后一像素
        //只要有一帧判假就再也回不来 用户不动 Offset 而 Extent 一直涨 于是一路被甩开
        var up = y < _lastOffsetY;
        _lastOffsetY = y;
        if (y + _scroll.Viewport.Height >= _scroll.Extent.Height - BottomTolerance) _follow = true;
        else if (up) _follow = false;
    }

    //MakeRow 解析一行日志 真正的文本控件由虚拟化按需生成
    //配色片段留在行对象上 容器滚出屏幕被回收再复用时直接照搬 不用重解析
    //animate 为真时这行挂进可见区会补一段入场动画 历史补看一次几百行就不做 那会把开窗拖慢
    private static LogRow MakeRow(LogStore.Entry entry, bool animate)
        => new(entry.Text, entry.Level, animate);

    //OnRowPrepared 新行第一次挂进可见区时播一次入场动画
    //虚拟化会把滚出屏幕的容器回收 滚动时重挂是常事 用 IsNew 挡一道只放行真正的新行
    private void OnRowPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container is not ListBoxItem item || item.DataContext is not LogRow row || !row.IsNew) return;
        row.IsNew = false;
        //先把起始值写到本地 动画起来之前那一帧才不会闪出完整的一行
        //跑完由 PlayAppear 把终值落回本地 否则属性回落到 0 整行会消失
        item.Opacity = 0;
        item.RenderTransform = new TranslateTransform(0, 6);
        LogRowAnimation.Play(item);
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        var command = CommandInput.Text?.Trim();
        CommandInput.Text = string.Empty;
        if (string.IsNullOrEmpty(command)) return;
        Execute(command);
    }

    //Execute 以控制台源投递命令 由主循环 tick 取出执行
    //不在 UI 线程也不在连接线程跑 与主循环同线程才有一致的世界状态 回执按命令源约定落回日志
    private void Execute(string command)
        => _server.EnqueueConsoleCommand(ServerCommandSource.Console(_server), command);
}

//LogRow 日志面板的一行 原始文本与配色片段一起存着
//虚拟化容器滚出屏幕会被回收再复用 片段留在行上 重新挂进来时直接照搬不用重解析
public sealed class LogRow
{
    private readonly string _line;
    private IReadOnlyList<AnsiLogParser.LogSpan>? _spans;
    private string? _plainText;

    public LogRow(string line, LogLevel level, bool animate)
    {
        _line = line;
        Level = level;
        IsNew = animate;
    }

    //Line 原始文本 排查时想按内容找行用得着 里面还带着 ANSI 着色码
    public string Line => _line;

    //Level 这行的级别 日志页按它做过滤 级别由 LogStore 解析一次 行对象直接带着走
    public LogLevel Level { get; }

    //Spans 配色片段 解析一次就留着
    //缓存提到两万行后构造期就解析会把开窗拖慢 而列表是虚拟化的 只有看得见的几十行会真去取它
    //所以拖到第一次要用时再解析
    public IReadOnlyList<AnsiLogParser.LogSpan> Spans => _spans ??= AnsiLogParser.Parse(_line);

    //PlainText 去掉 ANSI 后的可见文本 搜索在它上面跑
    public string PlainText
    {
        get
        {
            if (_plainText is not null) return _plainText;
            var text = new StringBuilder();
            foreach (var span in Spans) text.Append(span.Text);
            return _plainText = text.ToString();
        }
    }

    //IsNew 还没播过入场动画 容器回收复用后不该再播一次
    public bool IsNew { get; set; }

    //Matches 命中搜索的词在 PlainText 上的区间 渲染时按它把命中的那几个字挑出来加底
    public IReadOnlyList<(int Start, int Length)> Matches { get; set; } = Array.Empty<(int, int)>();

    //InView 这行当前在日志页的视图里 裁掉旧行时要据此同步删列表 免得视图与全量缓冲对不上
    public bool InView { get; set; }
}

//LogRowSpans 把一行的配色片段与命中区间灌进 TextBlock 的 Inlines
//TextBlock.Inlines 本身挂不上绑定 而 ItemTemplate 生成的控件又不在 ListBoxItem.Content 上
//挂成附加属性后变更回调正好落在模板实例绑好数据那一刻 容器改挂到另一行时也会重走进来
public static class LogRowSpans
{
    public static readonly AttachedProperty<IReadOnlyList<AnsiLogParser.LogSpan>?> SpansProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, IReadOnlyList<AnsiLogParser.LogSpan>?>(
            "Spans", typeof(TextBlock));

    public static readonly AttachedProperty<IReadOnlyList<(int Start, int Length)>?> MatchesProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, IReadOnlyList<(int Start, int Length)>?>(
            "Matches", typeof(TextBlock));

    //命中词的底色与字色 深底上拿亮黄配深字最跳 又不会把整行盖住
    private static readonly IBrush MatchBackground = new SolidColorBrush(Color.Parse("#FFD54A"));
    private static readonly IBrush MatchForeground = new SolidColorBrush(Color.Parse("#1E1F22"));

    static LogRowSpans()
    {
        //两个属性谁先到都会整行重画一遍 绑定更新顺序不保证 这样结果始终一致
        SpansProperty.Changed.AddClassHandler<TextBlock>((block, _) => Render(block));
        MatchesProperty.Changed.AddClassHandler<TextBlock>((block, _) => Render(block));
    }

    public static void SetSpans(TextBlock target, IReadOnlyList<AnsiLogParser.LogSpan>? value)
        => target.SetValue(SpansProperty, value);

    public static IReadOnlyList<AnsiLogParser.LogSpan>? GetSpans(TextBlock target)
        => target.GetValue(SpansProperty);

    public static void SetMatches(TextBlock target, IReadOnlyList<(int Start, int Length)>? value)
        => target.SetValue(MatchesProperty, value);

    public static IReadOnlyList<(int Start, int Length)>? GetMatches(TextBlock target)
        => target.GetValue(MatchesProperty);

    //Render 按片段写 Inlines 落在命中区间里的那几段换成高亮样式
    //片段是有色的 命中区间是按可见文本给的 所以边走边记已写出的字符数 把两者对齐
    private static void Render(TextBlock block)
    {
        block.Inlines!.Clear();
        var spans = block.GetValue(SpansProperty);
        if (spans is null) return;
        var matches = block.GetValue(MatchesProperty);
        var offset = 0;
        foreach (var span in spans)
        {
            var text = span.Text;
            var length = text.Length;
            if (matches is null || matches.Count == 0 || length == 0)
            {
                if (length > 0) block.Inlines.Add(new Run(text) { Foreground = span.Brush });
                offset += length;
                continue;
            }
            //这段文本覆盖 [offset, offset+length) 命中区间落进来的部分单独切一段出来
            var cursor = 0;
            foreach (var (start, matchLength) in matches)
            {
                var from = Math.Max(start, offset) - offset;
                var to = Math.Min(start + matchLength, offset + length) - offset;
                if (to <= from) continue;
                if (from > cursor) block.Inlines.Add(new Run(text[cursor..from]) { Foreground = span.Brush });
                block.Inlines.Add(new Run(text[from..to])
                {
                    Foreground = MatchForeground,
                    Background = MatchBackground,
                });
                cursor = to;
            }
            if (cursor < length) block.Inlines.Add(new Run(text[cursor..]) { Foreground = span.Brush });
            offset += length;
        }
    }
}
