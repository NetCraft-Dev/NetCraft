using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using NetCraft.Game;
using NetCraft.Server.Diagnostics;

namespace NetCraft.Server.Gui;

//ServerJitPanel JIT 编译事件列表
//状态栏按 T1+ > T1 > T0 分层 同层里刚变化的排前面 所以方法一升级就会浮到最上面
//行数不设上限 列表自带虚拟化 状态表多大都只画看得见的那些
public sealed partial class ServerJitPanel : UserControl
{
    //Appear 新行的入场动画 淡入并微微上移
    //FillMode 要设 Forward 默认的 None 会在动画跑完时把自己那层赋值撤掉
    //属性回落到起点 那一帧整行会闪 日志那边的入场动画踩过同一个坑
    private static readonly Animation Appear = new()
    {
        Duration = TimeSpan.FromMilliseconds(160),
        Easing = new CubicEaseOut(),
        FillMode = FillMode.Forward,
        Children =
        {
            new KeyFrame
            {
                Cue = new Cue(0d),
                Setters =
                {
                    new Setter(Visual.OpacityProperty, 0d),
                    new Setter(TranslateTransform.YProperty, 4d),
                },
            },
            new KeyFrame
            {
                Cue = new Cue(1d),
                Setters =
                {
                    new Setter(Visual.OpacityProperty, 1d),
                    new Setter(TranslateTransform.YProperty, 0d),
                },
            },
        },
    };

    //_view 列表的绑定源 只在构造时挂一次
    //整体换 ItemsSource 会让全部容器重建 那就分不出哪一行是新来的 动画也就无从谈起
    private readonly RangeObservableCollection<JitRow> _view = new();
    //_known 方法身份到行的映射 刷新时靠它把前后两次快照里的同一条认出来
    private readonly Dictionary<(ulong ModuleId, uint Token), JitRow> _known = new();
    private long _shownVersion = -1;

    public ServerJitPanel()
    {
        InitializeComponent();
        Rows.ItemsSource = _view;
    }

    //Refresh 状态表版本没变就不重算 与 500ms 轮询同频
    public void Refresh()
    {
        var version = JitEventMonitor.Version;
        if (version == _shownVersion) return;
        _shownVersion = version;

        var snapshot = JitEventMonitor.Snapshot();
        //第一次铺满走批量 逐项发通知时列表要为每一行跑一遍容器逻辑 几千行下来会卡住
        if (_view.Count == 0) Fill(snapshot);
        else Align(snapshot);
        Summary.Text = Loc.Format("netcraft.gui.jit.methods", snapshot.Count);
    }

    //Fill 首次把整个快照铺进视图 一次通知带完
    private void Fill(List<JitMethodState> snapshot)
    {
        var initial = new List<JitRow>(snapshot.Count);
        foreach (var state in snapshot)
        {
            var row = new JitRow(state);
            _known[(state.ModuleId, state.Token)] = row;
            initial.Add(row);
        }
        _view.ReplaceAll(initial);
    }

    //Align 把视图按快照顺序对齐 状态表只增不减 所以只有插入与移动 没有删除
    private void Align(List<JitMethodState> snapshot)
    {
        for (var i = 0; i < snapshot.Count; i++)
        {
            var state = snapshot[i];
            if (!_known.TryGetValue((state.ModuleId, state.Token), out var row))
            {
                row = new JitRow(state);
                _known[(state.ModuleId, state.Token)] = row;
                //先摆到目标位置 真正挂进可见区时才播入场动画
                _view.Insert(i, row);
                continue;
            }
            row.Update(state);
            //只有层级真的抬了才可能换位置
            //排序键里随时间变的只有 ChangedAt 而它只随层级一起更新 没升层的行相对顺序不会动
            //按这个前提收窄 IndexOf 否则每次刷新都要对全表做一遍线性查找
            if (!row.Changed) continue;
            //升级过的行要浮到前面 这里就是"一升层就置顶"的来源
            var current = _view.IndexOf(row);
            if (current != i) _view.Move(current, i);
        }
    }

    //OnRowPrepared 新行第一次挂进可见区时播一次入场动画
    //虚拟化会把滚出屏幕的容器回收 滚动时重挂是常事 用 IsNew 挡一道只放行真正的新行
    private void OnRowPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container is not ListBoxItem item || item.DataContext is not JitRow row || !row.IsNew) return;
        row.IsNew = false;
        item.Opacity = 0;
        item.RenderTransform = new TranslateTransform(0, 4);
        PlayAppear(item);
    }

    //PlayAppear 播一遍入场动画 跑完把终值落到本地
    //动画被取消时 FillAfter 不保证留住终值 这里补一次 否则该行会停在 Opacity=0 上
    private static async void PlayAppear(Visual target)
    {
        //行在动画途中被回收会让动画取消 取消异常冲进 SynchronizationContext 会崩掉进程
        try { await Appear.RunAsync(target); }
        catch (OperationCanceledException) { }
        target.Opacity = 1;
        if (target.RenderTransform is TranslateTransform transform) transform.Y = 0;
    }
}

//JitRow 列表里的一行 状态文案与配色随层级走
//独立成类是为了让 axaml 能用 x:DataType 做编译期绑定
//层级会随重编译往上抬 所以属性要能通知 列表里的行是就地改而不是整表重建
public sealed class JitRow : INotifyPropertyChanged
{
    private static readonly IBrush Tier0Brush = new SolidColorBrush(Color.Parse("#6B7280"));
    private static readonly IBrush Tier1Brush = new SolidColorBrush(Color.Parse("#A855F7"));
    private static readonly IBrush Tier1PlusBrush = new SolidColorBrush(Color.Parse("#22D3EE"));

    //_tier 取一个层级达不到的值 保证构造后第一次 Update 一定写进去
    private JitTier _tier = (JitTier)(-1);
    private string _status = "";
    private IBrush _brush = Tier0Brush;

    public JitRow(JitMethodState state)
    {
        Name = state.Name;
        Update(state);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get; }

    //IsNew 还没露过面 列表在它第一次挂进可见区时播一次入场动画 播完就没人再管它
    public bool IsNew { get; set; } = true;

    //Changed 上一次 Update 是否真的换了层 列表据此判断这行要不要挪位置
    public bool Changed { get; private set; }

    //Status 状态栏文案 T0 / T0→T1 / T1→T1+
    public string Status
    {
        get => _status;
        private set
        {
            if (_status == value) return;
            _status = value;
            Raise(nameof(Status));
        }
    }

    //Brush 状态栏配色 换色走 axaml 里的 BrushTransition 平滑过去
    public IBrush Brush
    {
        get => _brush;
        private set
        {
            if (ReferenceEquals(_brush, value)) return;
            _brush = value;
            Raise(nameof(Brush));
        }
    }

    //Update 按最新状态刷新文案与配色 层级没动就什么都不做
    public void Update(JitMethodState state)
    {
        Changed = state.Tier != _tier;
        if (!Changed) return;
        _tier = state.Tier;
        (Status, Brush) = state.Tier switch
        {
            JitTier.Tier1 => ("T0→T1", Tier1Brush),
            JitTier.Tier1Plus => ("T1→T1+", Tier1PlusBrush),
            _ => ("T0", Tier0Brush),
        };
    }

    private void Raise(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}

//RangeObservableCollection 能批量加入的 ObservableCollection
//ObservableCollection 每加一项就发一次通知 列表控件要为每一项跑一遍容器逻辑
//批量加完发一次就够了 但那条通知必须是增量的 Add/Remove 不能是 Reset
public sealed class RangeObservableCollection<T> : ObservableCollection<T>
{
    private bool _batching;

    //AddRange 逐项加入 每项发一条增量 Add 通知
    //原先批量加完只发一条 Reset 虚拟化列表收到 Reset 会把容器全部丢掉重建
    //日志区每帧都要插一批 于是每帧重建一次 滚动条的偏移与长度跟着重算 看着就是上下高频乱跳
    //单线程下逐项 Add 的通知顺序与索引天然对得上 追加到末尾生成容器的开销也只是 O(1)
    public void AddRange(IEnumerable<T> items)
    {
        foreach (var item in items) Add(item);
    }

    //RemoveRange 从指定下标起逐项移除 同样走增量通知
    //日志面板到上限后每帧都要从头部裁 一笔一笔删的通知远少于一次 Reset 带来的整体重建
    public void RemoveRange(int index, int count)
    {
        for (var i = 0; i < count; i++) RemoveAt(index);
    }

    //ReplaceAll 整体换一批 只发一条 Reset
    //留给首次铺满与过滤重排这类一次性动作 它们本来就换了整份内容 重建一次是应该的
    //常规的每帧追加必须走 AddRange 否则又回到每帧重建
    public void ReplaceAll(IEnumerable<T> items)
    {
        _batching = true;
        try
        {
            Clear();
            foreach (var item in items) Add(item);
        }
        finally
        {
            _batching = false;
        }
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (_batching) return;
        base.OnCollectionChanged(e);
    }
}
