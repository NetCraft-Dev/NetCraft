namespace NetCraft.Gpu;

//LinearLayout 线性布局对标原版 LinearLayout implements Layout
//包装 GridLayout HORIZONTAL 时 col 递增 VERTICAL 时 row 递增
//spacing 委托 GridLayout 的 columnSpacing/rowSpacing
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

    //Spacing 委托 GridLayout HORIZONTAL 设 columnSpacing VERTICAL 设 rowSpacing
    public LinearLayout Spacing(int spacing)
    {
        if (_orientation == Orientation.Horizontal) _wrapped.ColumnSpacing(spacing);
        else _wrapped.RowSpacing(spacing);
        return this;
    }

    public LayoutSettings NewCellSettings() => _wrapped.NewCellSettings();
    public LayoutSettings DefaultCellSetting() => _wrapped.DefaultCellSetting();

    //AddChild 默认 settings HORIZONTAL 放 col 递增 VERTICAL 放 row 递增
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

    //ILayout 委托 _wrapped
    public void VisitChildren(Action<ILayoutElement> visitor) => _wrapped.VisitChildren(visitor);
    public void RemoveChildren() { _wrapped.RemoveChildren(); _nextChildIndex = 0; }
    public void ArrangeElements() => _wrapped.ArrangeElements();

    //ILayoutElement 委托 _wrapped
    public int X { get => _wrapped.X; set => _wrapped.X = value; }
    public int Y { get => _wrapped.Y; set => _wrapped.Y = value; }
    public int Width => _wrapped.Width;
    public int Height => _wrapped.Height;

    //Orientation 线性方向 Horizontal 水平排列 Vertical 垂直排列
    public enum Orientation { Horizontal, Vertical }

    public static LinearLayout Vertical() => new(Orientation.Vertical);
    public static LinearLayout Horizontal() => new(Orientation.Horizontal);
}
