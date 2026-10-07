namespace NetCraft.Gpu;

//LinearLayout linear layout, maps to vanilla LinearLayout implements Layout
//Wraps GridLayout: col increments when HORIZONTAL, row increments when VERTICAL
//spacing delegates to GridLayout's columnSpacing/rowSpacing
public sealed class LinearLayout : ILayout
{
    private readonly GridLayout _wrapped;
    private readonly Orientation _orientation;
    private int _nextChildIndex;

    public LinearLayout(Orientation orientation = Orientation.Vertical) : this(0, 0, orientation) { }

    public LinearLayout(int x, int y, Orientation orientation)
    {
        _wrapped = new GridLayout(x, y);
        _orientation = orientation;
    }

    //Spacing delegates to GridLayout: HORIZONTAL sets columnSpacing, VERTICAL sets rowSpacing
    public LinearLayout Spacing(int spacing)
    {
        if (_orientation == Orientation.Horizontal) _wrapped.ColumnSpacing(spacing);
        else _wrapped.RowSpacing(spacing);
        return this;
    }

    public LayoutSettings NewCellSettings() => _wrapped.NewCellSettings();
    public LayoutSettings DefaultCellSetting() => _wrapped.DefaultCellSetting();

    //AddChild default settings: HORIZONTAL advances col, VERTICAL advances row
    public T AddChild<T>(T child) where T : ILayoutElement
        => AddChild(child, NewCellSettings());

    public T AddChild<T>(T child, LayoutSettings settings) where T : ILayoutElement
    {
        int index = _nextChildIndex++;
        if (_orientation == Orientation.Horizontal)
            _wrapped.AddChild(child, 0, index, settings);
        else
            _wrapped.AddChild(child, index, 0, settings);
        return child;
    }

    //ILayout delegates to _wrapped
    public void VisitChildren(Action<ILayoutElement> visitor) => _wrapped.VisitChildren(visitor);
    public void RemoveChildren() { _wrapped.RemoveChildren(); _nextChildIndex = 0; }
    public void ArrangeElements() => _wrapped.ArrangeElements();

    //ILayoutElement delegates to _wrapped
    public int X { get => _wrapped.X; set => _wrapped.X = value; }
    public int Y { get => _wrapped.Y; set => _wrapped.Y = value; }
    public int Width => _wrapped.Width;
    public int Height => _wrapped.Height;

    //Orientation linear direction Horizontal horizontal Vertical vertical
    public enum Orientation { Horizontal, Vertical }

    public static LinearLayout Vertical() => new(Orientation.Vertical);
    public static LinearLayout Horizontal() => new(Orientation.Horizontal);
}
