using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NetCraft.Game;
using NetCraft.Logging;

namespace NetCraft.Server.Gui;

//ServerLogPage 日志页 左边按级别过滤 上边正则搜索 下边是过滤后的日志
//与主页日志区共用 LogStore 那一份缓冲 渲染也同一套 差别只在没有命令输入区
//过滤器与搜索都只影响这一页 主页那份照旧是全量
public sealed partial class ServerLogPage : UserControl
{
    //显示上限 与 LogStore 的缓存上限一致 超出从头部丢
    private const int MaxLines = 20000;
    //每帧放行一小批 与主页日志区同样的节奏
    private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(16);
    //搜索防抖 每敲一个键都重排两千行会把输入拖出顿挫感
    private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(160);
    private const int MaxPerFlush = 200;
    //放行行数不超过这个值才做入场动画 积压时动画只会帮倒忙
    private const int AnimatedBatchLimit = 6;
    private const int MaxPending = 4000;
    //单行最多标这么多处命中 搜一个常见字母时一行能命中上千处 没必要全标
    private const int MaxMatchesPerRow = 200;
    //底部的容差 小于它就算贴着
    private const double BottomTolerance = 4;

    private readonly LogStore _store;
    //_pending 日志由输出线程写 UI 线程读
    private readonly Queue<LogStore.Entry> _pending = new();
    private readonly Lock _pendingLock = new();
    private readonly DispatcherTimer _flush;
    private readonly DispatcherTimer _searchDelay;
    //_all 收到过的全部行 过滤与搜索都在这上面挑 视图只是它的子集
    private readonly List<LogRow> _all = new();
    //_hits 命中搜索的行 按视图顺序排 上下跳转就靠它
    private readonly List<LogRow> _hits = new();
    private readonly RangeObservableCollection<LogRow> _view = new();
    //_active 选中的级别 空集表示不过滤
    private readonly HashSet<LogLevel> _active = new();
    private Regex? _search;
    private int _hitIndex;
    //_searchDirty 输入改过但还没搜 回车时据此决定是跳第一处还是下一处
    private bool _searchDirty;
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

    public ServerLogPage(LogStore store)
    {
        _store = store;
        InitializeComponent();
        LogLines.ItemsSource = _view;
        //挂在 ListBox 上接内部滚动条冒泡上来的事件 免得到 Loaded 里再去树里翻
        //handledEventsToo: 万一滚动条那边把事件标了 handled 也要收到 收不到就退化成永远跟随
        LogLines.AddHandler(ScrollViewer.ScrollChangedEvent, OnScrollChanged,
            RoutingStrategies.Bubble, handledEventsToo: true);

        BuildFilters();
        SearchBox.TextChanged += (_, _) => RestartSearch();
        SearchBox.KeyDown += OnSearchKeyDown;
        PrevButton.Click += (_, _) => JumpMatch(-1);
        NextButton.Click += (_, _) => JumpMatch(1);

        //构造期先把缓冲里的行全接过来 此刻过滤器为空 等于全部显示
        foreach (var entry in _store.Snapshot())
        {
            var row = MakeRow(entry, animate: false);
            row.InView = true;
            _all.Add(row);
        }
        _view.ReplaceAll(_all);
        UpdateMatchLabel();
        _store.LineAdded += OnLine;
        //历史在构造期填入 那会儿还没测量 直接滚到底是空操作
        //等布局跑完再排一次 否则开窗停在最老的一行
        LogLines.Loaded += (_, _) =>
            Dispatcher.UIThread.Post(ScrollToEnd, DispatcherPriority.Loaded);

        _flush = new DispatcherTimer { Interval = FlushInterval };
        _flush.Tick += (_, _) => FlushPending();
        _flush.Start();
        _searchDelay = new DispatcherTimer { Interval = SearchDelay };
        _searchDelay.Tick += (_, _) =>
        {
            _searchDelay.Stop();
            ApplySearch();
        };
    }

    //Detach 解订阅 窗口关闭时调 否则这个页面会被日志系统一直引用着
    public void Detach()
    {
        _store.LineAdded -= OnLine;
        _flush.Stop();
        _searchDelay.Stop();
    }

    //BuildFilters 按可用的级别生成过滤开关
    //DBG 只在开了调试模式时放出来 常规模式控制台根本不产 Debug 日志 摆一个常暗的按钮没意义
    private void BuildFilters()
    {
        var levels = new List<LogLevel> { LogLevel.Info, LogLevel.Warning, LogLevel.Error, LogLevel.Critical };
        if (Log.DebugEnabled) levels.Insert(0, LogLevel.Debug);
        foreach (var level in levels)
        {
            var toggle = new ToggleButton
            {
                Content = LevelName(level),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Classes = { "levelToggle", LevelClass(level) },
            };
            //闭包要的是这一轮的级别 循环变量直接捕进去会全是最后一个
            //盯属性变化而不是 Click: 那样不用去猜 Click 是在切换 IsChecked 之前还是之后发的
            //读到的永远是切换后的值
            var captured = level;
            toggle.PropertyChanged += (_, e) =>
            {
                if (e.Property == ToggleButton.IsCheckedProperty)
                    OnFilterChanged(captured, e.GetNewValue<bool?>() == true);
            };
            Filters.Children.Add(toggle);
        }
    }

    private void OnFilterChanged(LogLevel level, bool on)
    {
        if (on) _active.Add(level);
        else _active.Remove(level);
        Rebuild();
    }

    //Rebuild 按当前过滤器与搜索重排视图
    //整体重排而不是打补丁 这两件事都是低频动作 换来的逻辑简单值这个开销
    private void Rebuild()
    {
        _hits.Clear();
        _hitIndex = 0;
        var shown = new List<LogRow>(_all.Count);
        foreach (var row in _all)
        {
            //先一律复位 这行可能因为过滤器或搜索变化而退出视图
            row.Matches = Array.Empty<(int, int)>();
            row.InView = false;
            if (_active.Count > 0 && !_active.Contains(row.Level)) continue;
            row.InView = true;
            if (_search is not null)
            {
                var matches = FindMatches(row.PlainText);
                if (matches.Count > 0)
                {
                    row.Matches = matches;
                    _hits.Add(row);
                }
            }
            shown.Add(row);
        }
        _view.ReplaceAll(shown);
        UpdateMatchLabel();
        //重建后视图整体换过 高度全变了 粘着底部的话要重新贴一次 否则会停回最老的一行
        if (_follow) ScrollToEnd();
    }

    //RestartSearch 输入变化后重启防抖 连着敲字只会在停手后搜一次
    private void RestartSearch()
    {
        _searchDirty = true;
        _searchDelay.Stop();
        _searchDelay.Start();
    }

    //ApplySearch 编译正则并重排 正则不合法就退回不过滤 只在输入框上标红提示
    private void ApplySearch()
    {
        var pattern = SearchBox.Text?.Trim() ?? string.Empty;
        if (pattern.Length == 0)
        {
            _search = null;
            SearchBox.Classes.Remove("invalid");
        }
        else
        {
            try
            {
                _search = new Regex(pattern);
                SearchBox.Classes.Remove("invalid");
            }
            catch (ArgumentException)
            {
                _search = null;
                SearchBox.Classes.Add("invalid");
            }
        }
        _searchDirty = false;
        Rebuild();
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            //清空后 TextChanged 会走一遍 这里不重复触发
            SearchBox.Text = string.Empty;
            return;
        }
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        //回车不等防抖 立刻搜一次再跳 刚改过就跳第一处 没改就跳下一处
        var fresh = _searchDirty;
        _searchDelay.Stop();
        ApplySearch();
        JumpMatch(fresh ? 0 : 1);
    }

    //JumpMatch 在命中之间循环跳转 列表只滚到可见 不做居中
    private void JumpMatch(int delta)
    {
        if (_hits.Count == 0) return;
        _hitIndex = (_hitIndex + delta + _hits.Count) % _hits.Count;
        LogLines.ScrollIntoView(_hits[_hitIndex]);
        UpdateMatchLabel();
    }

    private void UpdateMatchLabel()
    {
        //没有搜索就不显示 搜索了但一处没中要说清 空着会让人以为还在算
        if (_search is null) MatchLabel.Text = string.Empty;
        else MatchLabel.Text = _hits.Count == 0 ? Loc.Get("netcraft.gui.log.no_match") : $"{_hitIndex + 1}/{_hits.Count}";
        //当前显示了几行比总数少多少 过滤到底有没有生效看这一行最直接
        FilterStats.Text = Loc.Format("netcraft.gui.log.filter_stats", _view.Count, _all.Count);
    }

    //FindMatches 在可见文本上跑正则 返回命中区间
    //零宽命中(^ 或 a* 这种)只给位置不给长度 直接丢掉 插出来会是一段空的高亮
    //单行命中过多就截断 搜一个常见字母时一行能命中上千处
    private List<(int Start, int Length)> FindMatches(string text)
    {
        var result = new List<(int, int)>();
        if (_search is null || text.Length == 0) return result;
        foreach (Match match in _search.Matches(text))
        {
            if (match.Length == 0) continue;
            result.Add((match.Index, match.Length));
            if (result.Count >= MaxMatchesPerRow) break;
        }
        return result;
    }

    private void OnLine(LogStore.Entry entry)
    {
        lock (_pendingLock)
        {
            //积压到顶就丢最老的 日志暴涨时保最新的一批才有意义
            if (_pending.Count >= MaxPending) _pending.Dequeue();
            _pending.Enqueue(entry);
        }
    }

    //FlushPending 每帧把积压的日志放行一小批
    //被过滤器挡下的行照旧进全量缓冲 只是不进视图 这样回头取消过滤还能看见它们
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
        var added = new List<LogRow>(batch.Count);
        foreach (var entry in batch)
        {
            var row = MakeRow(entry, animate);
            //这里只入全量缓冲 裁剪留到整批插完之后做一次
            //摆在循环里就成了每插一行裁一次 满上限后每行都要动一次视图 高频下抖得厉害
            _all.Add(row);
            if (_active.Count > 0 && !_active.Contains(row.Level)) continue;
            row.InView = true;
            if (_search is not null)
            {
                var matches = FindMatches(row.PlainText);
                if (matches.Count > 0)
                {
                    row.Matches = matches;
                    _hits.Add(row);
                }
            }
            added.Add(row);
        }
        TrimToLimit();
        if (added.Count > 0) _view.AddRange(added);
        //没有新行进视图时不要碰滚动条 过滤态下大部分帧都是这样 空滚一次就是白跑一次布局
        if (_follow && added.Count > 0) ScrollToEnd();
        UpdateMatchLabel();
    }

    //TrimToLimit 裁掉最老的那些行 视图与命中表都要跟着清
    //_view 是 _all 的子集且同序 数出前 over 条里有几条在视图上就能整批从头删
    private void TrimToLimit()
    {
        var over = _all.Count - MaxLines;
        if (over <= 0) return;
        var visible = 0;
        for (var i = 0; i < over; i++)
        {
            var row = _all[i];
            if (row.InView) visible++;
            if (row.Matches.Count > 0) _hits.Remove(row);
        }
        _all.RemoveRange(0, over);
        if (visible > 0) _view.RemoveRange(0, visible);
        if (_hits.Count > 0) _hitIndex = Math.Min(_hitIndex, _hits.Count - 1);
        else _hitIndex = 0;
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

    private static LogRow MakeRow(LogStore.Entry entry, bool animate)
        => new(entry.Text, entry.Level, animate);

    //OnRowPrepared 新行第一次挂进可见区时播一次入场动画
    //虚拟化会把滚出屏幕的容器回收 滚动时重挂是常事 用 IsNew 挡一道只放行真正的新行
    private void OnRowPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container is not ListBoxItem item || item.DataContext is not LogRow row || !row.IsNew) return;
        row.IsNew = false;
        //先把起始值写到本地 动画起来之前那一帧才不会闪出完整的一行
        item.Opacity = 0;
        item.RenderTransform = new TranslateTransform(0, 6);
        LogRowAnimation.Play(item);
    }

    private static string LevelName(LogLevel level) => level switch
    {
        LogLevel.Debug => "DBG",
        LogLevel.Warning => "WARN",
        LogLevel.Error => "ERROR",
        LogLevel.Critical => "CRIT",
        _ => "INFO",
    };

    //LevelClass 样式用的类名 与日志行里那支级别色对上
    private static string LevelClass(LogLevel level) => level switch
    {
        LogLevel.Debug => "dbg",
        LogLevel.Warning => "warn",
        LogLevel.Error => "error",
        LogLevel.Critical => "crit",
        _ => "info",
    };
}
