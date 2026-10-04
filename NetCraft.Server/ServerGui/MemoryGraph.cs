using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NetCraft.Server.Gui;

//MemoryGraph 内存占用柱状图
//对应原版 StatsComponent.paint 里那 256 根柱子 保留最近 256 次采样的堆占用比例
//柱色按占比从绿经黄到红 由 ServerStatsPanel 每 500ms 推一个采样进来
public sealed class MemoryGraph : Control
{
    private const int Columns = 256;

    private static readonly Color LowColor = Color.Parse("#22C55E");
    private static readonly Color MidColor = Color.Parse("#F59E0B");
    private static readonly Color HighColor = Color.Parse("#EF4444");
    //BreakBrush 上限变更处的截断线
    //用亮蓝而不是红 柱色本身就是绿经黄到红 红线和满格柱会糊在一起分不出来
    private static readonly IBrush BreakBrush = new SolidColorBrush(Color.Parse("#2E9BFF"));
    //GcMarkBrush 基线上的 GC 亮点
    //用亮绿而不是红 柱色本身就经黄到红 红点会和预警柱糊在一起
    private static readonly IBrush GcMarkBrush = new SolidColorBrush(Color.Parse("#00E676"));
    //JitMarkBrush 基线上的 Tier1 重编译紫点
    private static readonly IBrush JitMarkBrush = new SolidColorBrush(Color.Parse("#A855F7"));
    //Tier2MarkBrush 基线上的 Tier2 及以上重编译青点
    private static readonly IBrush Tier2MarkBrush = new SolidColorBrush(Color.Parse("#22D3EE"));
    //MarkRadius 三层打点共用的半径 收小一档让它们挤得近些又不至于叠在一起
    private const double MarkRadius = 1.8;
    //MarkRowGap 相邻两行打点的中心距 比直径多出一点 边缘才留得住一条缝
    private const double MarkRowGap = MarkRadius * 2 + 0.6;
    //BackdropFallback 没注入底色时的兜底
    private static readonly IBrush BackdropFallback = new SolidColorBrush(Color.Parse("#F0F2F5"));

    //BackdropBrush 柱图底色 由 axaml 用 DynamicResource 注入 主题切换时自动跟着换
    //自绘控件自己查资源拿不到 ThemeDictionaries 里的条目 交给属性系统才是正路
    public static readonly StyledProperty<IBrush?> BackdropBrushProperty =
        AvaloniaProperty.Register<MemoryGraph, IBrush?>(nameof(BackdropBrush));

    public IBrush? BackdropBrush
    {
        get => GetValue(BackdropBrushProperty);
        set => SetValue(BackdropBrushProperty, value);
    }

    private readonly double[] _values = new double[Columns];
    private readonly SolidColorBrush[] _brushes = new SolidColorBrush[Columns];
    //_breaks 记录哪些列发生过上限变更 与 _values 同一套环形下标
    private readonly bool[] _breaks = new bool[Columns];
    //_gcMarks 记录哪些列在这一拍里收到过 GC 事件
    private readonly bool[] _gcMarks = new bool[Columns];
    //_jitMarks 记录哪些列在这一拍里收到过 Tier1 重编译
    private readonly bool[] _jitMarks = new bool[Columns];
    //_tier2Marks 记录哪些列在这一拍里收到过 Tier2 及以上重编译
    private readonly bool[] _tier2Marks = new bool[Columns];
    private int _head;

    public MemoryGraph()
    {
        //柱色 256 档一次建好 绘制时不再分配
        for (var i = 0; i < Columns; i++)
        {
            var t = (double)i / (Columns - 1);
            _brushes[i] = new SolidColorBrush(t < 0.5
                ? Lerp(LowColor, MidColor, t * 2)
                : Lerp(MidColor, HighColor, (t - 0.5) * 2));
        }
    }

    //Push 推入一次采样 ratio 为 0-1 的堆占用比例 柱高直接按它取 对应原版按占用百分比画柱高
    //rescaled 表示这一次采样时上限换过挡 该列要标一条截断线 前后柱高不可直接比
    //gcCount 是这一拍里发生的 GC 次数 tier1Count 与 tier2Count 是两级重编译次数 大于零就在底边各点一个点
    public void Push(double ratio, bool rescaled, int gcCount, int tier1Count, int tier2Count)
    {
        var index = _head++ & (Columns - 1);
        _values[index] = Math.Clamp(ratio, 0, 1);
        _breaks[index] = rescaled;
        _gcMarks[index] = gcCount > 0;
        _jitMarks[index] = tier1Count > 0;
        _tier2Marks[index] = tier2Count > 0;
        InvalidateVisual();
    }

    private static Color Lerp(Color from, Color to, double t) => Color.FromRgb(
        (byte)(from.R + (to.R - from.R) * t),
        (byte)(from.G + (to.G - from.G) * t),
        (byte)(from.B + (to.B - from.B) * t));

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0) return;
        context.FillRectangle(BackdropBrush ?? BackdropFallback, new Rect(0, 0, width, height));
        var step = width / Columns;
        var barWidth = Math.Max(step, 1);
        for (var x = 0; x < Columns; x++)
        {
            var ratio = _values[(x + _head) & (Columns - 1)];
            if (ratio <= 0) continue;
            var barHeight = ratio * height;
            if (barHeight < 1) barHeight = 1;
            context.FillRectangle(_brushes[(int)(ratio * (Columns - 1))],
                new Rect(x * step, height - barHeight, barWidth, barHeight));
        }
        //截断线压在柱子之上 标出刻度在这里换过挡 两侧柱高不是一个量纲
        for (var x = 0; x < Columns; x++)
        {
            if (!_breaks[(x + _head) & (Columns - 1)]) continue;
            context.FillRectangle(BreakBrush, new Rect(x * step, 0, barWidth, height));
        }
        //底边上的三层打点 自下而上是 GC 红点 / Tier1 紫点 / Tier2 青点 共用同一条时间轴
        var markRow = height - MarkRadius;
        for (var x = 0; x < Columns; x++)
        {
            if (!_gcMarks[(x + _head) & (Columns - 1)]) continue;
            context.DrawEllipse(GcMarkBrush, null, new Point(x * step + step / 2, markRow),
                MarkRadius, MarkRadius);
        }
        markRow -= MarkRowGap;
        for (var x = 0; x < Columns; x++)
        {
            if (!_jitMarks[(x + _head) & (Columns - 1)]) continue;
            context.DrawEllipse(JitMarkBrush, null, new Point(x * step + step / 2, markRow),
                MarkRadius, MarkRadius);
        }
        markRow -= MarkRowGap;
        for (var x = 0; x < Columns; x++)
        {
            if (!_tier2Marks[(x + _head) & (Columns - 1)]) continue;
            context.DrawEllipse(Tier2MarkBrush, null, new Point(x * step + step / 2, markRow),
                MarkRadius, MarkRadius);
        }
    }
}
