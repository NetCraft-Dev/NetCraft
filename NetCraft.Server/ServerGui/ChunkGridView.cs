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

//ChunkGridView, a chunk grid view, colors the surrounding chunks with built holders by ticket level centered on some chunk
//Only draws chunks present in the table and does not iterate every coordinate in the visible range: there is no holder beyond the view distance and the background color serves as "unloaded"
//Colors have only two tiers, entity-ticking in one tier and the rest within the loaded range in the other, anything else is not drawn
//The center is fractional, both dragging and following a player move it continuously, and each cell has its own transition state for load and unload fades
public sealed class ChunkGridView : Control
{
    //The valid range for cell size, zooming changes it within this interval
    public const double MinCellSize = 2;
    public const double MaxCellSize = 28;
    //Below this cell size the lines are denser than the cells, the grid is not drawn then
    private const double GridLineThreshold = 4;
    //Fraction advanced toward the target each frame, at 0.25 it converges in about 0.3 seconds, looking like a steady slide without lagging
    private const double EaseFactor = 0.25;
    //Anything below this gap counts as arrived, to avoid an endless approach that never stops
    private const double Epsilon = 0.002;
    //Fade advance per cell per second
    private const double FadeSpeed = 7;
    //Number of interpolation levels between the strong and weak tiers, colors are segmented by it to avoid building a brush per cell
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

    //Origin color, a fixed marker on the world center cell
    public static readonly StyledProperty<IBrush?> OriginBrushProperty =
        AvaloniaProperty.Register<ChunkGridView, IBrush?>(nameof(OriginBrush));

    //_targets the target snapshot, chunk -> whether strongly loaded, transition states converge toward it
    private readonly Dictionary<ChunkPos, bool> _targets = new();
    //_states per-cell transition state, progress governs appearing and strength governs shifting between yellow and green
    private readonly Dictionary<ChunkPos, CellState> _states = new();
    //_players the chunk and name of each player, drawn as a name tag in the corresponding cell
    private readonly List<(ChunkPos Pos, string Name)> _players = new();
    //_hover the hovered chunk, empty means the pointer is off the grid, the outline follows it
    private ChunkPos? _hover;
    //_fading cells to drop this round, the dictionary cannot be mutated while iterating
    private readonly List<ChunkPos> _fading = new();

    private double _centerX;
    private double _centerZ;
    private double _targetCenterX;
    private double _targetCenterZ;

    //_timer the frame loop, started only when a transition must run and stopped on convergence, no CPU cost when idle
    private DispatcherTimer? _timer;
    private bool _dragging;
    private Point _dragOrigin;
    private double _dragCenterX;
    private double _dragCenterZ;

    private sealed class CellState
    {
        //Progress the appear progress, 0 means not yet visible, 1 fully shown
        public double Progress;
        //Strength the strong-load degree, 0 is the weak color, 1 is the loaded color
        public double Strength;
    }

    static ChunkGridView()
    {
        //A change to the size or any brush forces a redraw, otherwise the view stays at the old scale when zooming
        AffectsRender<ChunkGridView>(CellSizeProperty, GridBackgroundProperty, GridLineProperty,
            LoadedBrushProperty, WeakBrushProperty, MarkerBrushProperty, OriginBrushProperty);
    }

    //CenterPanned fires the current center after a user drag, the page exits following and syncs the manual center
    public event Action<double, double>? CenterPanned;

    //HoverChanged fires when the hovered chunk changes, carrying player names in that cell, the chunk is null when the pointer leaves
    public event Action<ChunkPos?, IReadOnlyList<string>>? HoverChanged;

    //CurrentCenter the current rendered center, used to pin the view in place when following is cancelled or the player goes offline
    public (double X, double Z) CurrentCenter => (_centerX, _centerZ);

    //CellSize cell side length in pixels
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

    //OriginBrush the fixed marker color on the world center cell
    public IBrush? OriginBrush
    {
        get => GetValue(OriginBrushProperty);
        set => SetValue(OriginBrushProperty, value);
    }

    //SetSnapshot swaps in one frame's target snapshot, both the center and the cell set move smoothly toward the new values
    //strong being true means the chunk's ticket level has reached entity-ticking
    public void SetSnapshot(double centerX, double centerZ,
        IReadOnlyList<(ChunkPos Pos, bool Strong)> cells,
        IReadOnlyList<(ChunkPos Pos, string Name)> players)
    {
        //When the target is identical to last time the frame loop is not started, an idle frame every half second is wasted burn
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
            //A new cell starts fully transparent and its strength starts at the weak tier, so it brightens yellow then turns green, exactly the order loading progresses
            if (!_states.ContainsKey(pos)) _states[pos] = new CellState();
        }

        _players.Clear();
        for (var i = 0; i < players.Count; i++) _players.Add(players[i]);

        if (changed) EnsureAnimating();
        InvalidateVisual();
    }

    //EnsureAnimating starts the frame loop only when a transition must run, it stops itself when done
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

    //Step advances one transition step and returns whether more are needed
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
            //Align exactly at the end so it does not stop a hair away
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

    //DrawCells fills cells one by one, the color interpolates between the two tiers by strength and opacity follows the appear progress, so both loading and unloading fade
    private void DrawCells(DrawingContext context, double cell, double centerX, double centerY,
        double width, double height)
    {
        if (WeakBrush is not ISolidColorBrush weak || LoadedBrush is not ISolidColorBrush loaded) return;

        //Precompute the graded colors between the two tiers, only these brushes are built per frame, not one per cell
        var shades = new SolidColorBrush[ShadeCount + 1];
        for (var i = 0; i <= ShadeCount; i++)
            shades[i] = new SolidColorBrush(Lerp(weak.Color, loaded.Color, (double)i / ShadeCount));

        foreach (var (pos, state) in _states)
        {
            var x = centerX + (pos.X - _centerX - 0.5) * cell;
            var y = centerY + (pos.Z - _centerZ - 0.5) * cell;
            if (x + cell < 0 || x > width || y + cell < 0 || y > height) continue;

            //Appearing grows from the cell center outward and disappearing shrinks back, more natural than a hard cut
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
        //Cell borders land at control center + (cell number + 0.5 - center) * cell width, sliding continuously with the center so dragging follows the hand
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

    //DrawMarkers outlines the hovered cell, places name tags in player cells, and draws a red dot on the world origin cell
    //The outline used to be pinned to the rendered center, now it follows the pointer and frames whichever cell is pointed at
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

            //Players no longer draw a square dot, a name tag is placed in their cell, falling back to a small square when the cell is too small for text
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
                //Two FormattedText types share the name, the fully qualified name is used, the one needed here is Avalonia's text drawing one
                var label = new Avalonia.Media.FormattedText(name, CultureInfo.CurrentCulture,
                    Avalonia.Media.FlowDirection.LeftToRight, Typeface.Default, fontSize, marker);
                var box = new Rect(x - label.Width / 2 - 3, y - label.Height / 2 - 1,
                    label.Width + 6, label.Height + 2);
                if (GridBackground is { } background) context.FillRectangle(background, box);
                context.DrawText(label, new Point(box.X + 3, box.Y + 1));
            }
        }

        //The world center cell, its position does not follow the rendered center and it must stay visible when zoomed all the way out so the radius has a lower bound
        var origin = OriginBrush;
        if (origin is null) return;
        var originX = centerX - _centerX * cell;
        var originY = centerY - _centerZ * cell;
        if (originX < -cell || originX > width + cell || originY < -cell || originY > height + cell) return;
        var radius = Math.Clamp(cell * 0.22, 1.5, 3.5);
        context.DrawEllipse(origin, null, new Point(originX, originY), radius, radius);
    }

    //UpdateHover converts the pointer position to a chunk, only a change redraws the outline and notifies the page
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

    //ClearHover removes the outline after the pointer leaves
    private void ClearHover()
    {
        if (_hover is null) return;
        _hover = null;
        InvalidateVisual();
        HoverChanged?.Invoke(null, Array.Empty<string>());
    }

    //NamesAt player names in that chunk, for the hover hint
    private IReadOnlyList<string> NamesAt(ChunkPos pos)
    {
        List<string>? names = null;
        foreach (var (playerPos, name) in _players)
            if (playerPos == pos) (names ??= new List<string>()).Add(name);
        return names ?? (IReadOnlyList<string>)Array.Empty<string>();
    }

    //OnPointerPressed a left button press starts dragging the map
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

    //OnPointerMoved the map follows the hand while dragging, the center shifts the opposite way, no interpolation here since following the hand is the point
    //When the pointer rests on the grid the hovered cell is updated and both the outline and the page hint follow it
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
        //Dragging changes the center so the hovered cell must be computed on the new center, otherwise it lags one step
        UpdateHover(now, cell);
    }

    //OnPointerExited removes the hover outline when the pointer leaves the control, kept while dragging is captured outside
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

    //Stop the timer when detached from the visual tree so no timer keeps spinning after the page is switched away
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Stop();
        _timer = null;
    }

    //Lerp takes the midpoint between two colors by ratio
    private static Color Lerp(Color from, Color to, double t)
        => Color.FromArgb(
            (byte)(from.A + (to.A - from.A) * t),
            (byte)(from.R + (to.R - from.R) * t),
            (byte)(from.G + (to.G - from.G) * t),
            (byte)(from.B + (to.B - from.B) * t));
}
