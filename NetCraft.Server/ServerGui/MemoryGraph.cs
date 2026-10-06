using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NetCraft.Server.Gui;

//MemoryGraph, memory usage bar chart
//Maps to the 256 bars in vanilla StatsComponent.paint, keeps the heap usage ratio of the last 256 samples
//Bar color goes from green through yellow to red by ratio, ServerStatsPanel pushes one sample in every 500ms
public sealed class MemoryGraph : Control
{
    private const int Columns = 256;

    private static readonly Color LowColor = Color.Parse("#22C55E");
    private static readonly Color MidColor = Color.Parse("#F59E0B");
    private static readonly Color HighColor = Color.Parse("#EF4444");
    //BreakBrush, the cutoff line where the cap changes
    //Uses bright blue instead of red, the bar color itself goes green through yellow to red and a red line would blur into a full bar
    private static readonly IBrush BreakBrush = new SolidColorBrush(Color.Parse("#2E9BFF"));
    //GcMarkBrush, GC dots on the baseline
    //Uses bright green instead of red, the bar color itself goes through yellow to red and a red dot would blur into a warning bar
    private static readonly IBrush GcMarkBrush = new SolidColorBrush(Color.Parse("#00E676"));
    //JitMarkBrush, Tier1 recompile purple dots on the baseline
    private static readonly IBrush JitMarkBrush = new SolidColorBrush(Color.Parse("#A855F7"));
    //Tier2MarkBrush, Tier2+ recompile cyan dots on the baseline
    private static readonly IBrush Tier2MarkBrush = new SolidColorBrush(Color.Parse("#22D3EE"));
    //MarkRadius shared by the three mark rows, shrunk a step so they pack closer without overlapping
    private const double MarkRadius = 1.8;
    //MarkRowGap center distance between adjacent mark rows, slightly more than the diameter so a gap remains at the edges
    private const double MarkRowGap = MarkRadius * 2 + 0.6;
    //BackdropFallback used when no backdrop color is injected
    private static readonly IBrush BackdropFallback = new SolidColorBrush(Color.Parse("#F0F2F5"));

    //BackdropBrush, the chart backdrop color injected by axaml via DynamicResource, follows theme switches automatically
    //A self-drawn control cannot reach ThemeDictionaries entries by looking up resources itself, going through the property system is the right way
    public static readonly StyledProperty<IBrush?> BackdropBrushProperty =
        AvaloniaProperty.Register<MemoryGraph, IBrush?>(nameof(BackdropBrush));

    public IBrush? BackdropBrush
    {
        get => GetValue(BackdropBrushProperty);
        set => SetValue(BackdropBrushProperty, value);
    }

    private readonly double[] _values = new double[Columns];
    private readonly SolidColorBrush[] _brushes = new SolidColorBrush[Columns];
    //_breaks records which columns had a cap change, same ring indices as _values
    private readonly bool[] _breaks = new bool[Columns];
    //_gcMarks records which columns received a GC event in this tick
    private readonly bool[] _gcMarks = new bool[Columns];
    //_jitMarks records which columns received a Tier1 recompile in this tick
    private readonly bool[] _jitMarks = new bool[Columns];
    //_tier2Marks records which columns received a Tier2+ recompile in this tick
    private readonly bool[] _tier2Marks = new bool[Columns];
    private int _head;

    public MemoryGraph()
    {
        //Build all 256 bar colors once, no allocation during rendering
        for (var i = 0; i < Columns; i++)
        {
            var t = (double)i / (Columns - 1);
            _brushes[i] = new SolidColorBrush(t < 0.5
                ? Lerp(LowColor, MidColor, t * 2)
                : Lerp(MidColor, HighColor, (t - 0.5) * 2));
        }
    }

    //Push pushes one sample, ratio is the heap usage ratio in 0-1 and directly sets the bar height, mapping to vanilla drawing bar height by usage percentage
    //rescaled means the cap changed on this sample, the column gets a cutoff line and bar heights before and after are not directly comparable
    //gcCount is the number of GCs in this tick, tier1Count and tier2Count are the two recompile levels, a value above zero draws a dot on the baseline
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
        //The cutoff line is drawn over the bars to mark where the scale changed, bar heights on either side are not the same unit
        for (var x = 0; x < Columns; x++)
        {
            if (!_breaks[(x + _head) & (Columns - 1)]) continue;
            context.FillRectangle(BreakBrush, new Rect(x * step, 0, barWidth, height));
        }
        //The three mark rows on the baseline, bottom to top are GC red dots / Tier1 purple dots / Tier2 cyan dots, sharing one time axis
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
