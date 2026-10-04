namespace NetCraft.Gpu;

//EqualSpacingLayout 等间距布局对标原版 EqualSpacingLayout extends AbstractLayout
//子元素在主轴方向等间距分布副轴方向对齐到起点
//主轴间隙用 Divisor 均分 remainingSpace 到 size-1 个间隙
public sealed class EqualSpacingLayout : AbstractLayout
{
    private readonly Orientation _orientation;
    private readonly List<ChildContainer> _children = new();
    private LayoutSettings _defaultChildSettings;

    public EqualSpacingLayout(int width, int height, Orientation orientation) : this(0, 0, width, height, orientation) { }

    public EqualSpacingLayout(int x, int y, int width, int height, Orientation orientation) : base(x, y, width, height)
    {
        _defaultChildSettings = LayoutSettings.Defaults();
        _orientation = orientation;
    }

    public LayoutSettings NewChildLayoutSettings() => _defaultChildSettings.Copy();
    public LayoutSettings DefaultChildLayoutSetting() => _defaultChildSettings;

    //ArrangeElements 主轴等间距分布副轴对齐到起点
    //第一个孩子放主轴起点后续孩子按均分间隙分布副轴统一对齐
    public override void ArrangeElements()
    {
        base.ArrangeElements();
        if (_children.Count == 0) return;

        int totalPrimary = 0;
        //副轴初始取布局自身副轴长度再与子元素取 max
        int maxSecondary = GetSecondaryLength(this);
        foreach (var c in _children)
        {
            totalPrimary += GetPrimaryLength(c);
            maxSecondary = Math.Max(maxSecondary, GetSecondaryLength(c));
        }

        int remaining = GetPrimaryLength(this) - totalPrimary;
        int position = GetPrimaryPosition(this);

        var first = _children[0];
        SetPrimaryPosition(first, position);
        int nextPos = position + GetPrimaryLength(first);

        //size>=2 时 remaining 均分到 size-1 个间隙驱动后续孩子主轴位置
        if (_children.Count >= 2)
        {
            var divisor = new Divisor(remaining, _children.Count - 1);
            for (int i = 1; i < _children.Count; i++)
            {
                int p = nextPos + divisor.NextInt();
                var c = _children[i];
                SetPrimaryPosition(c, p);
                nextPos = p + GetPrimaryLength(c);
            }
        }

        int secondaryPos = GetSecondaryPosition(this);
        foreach (var c in _children)
            SetSecondaryPosition(c, secondaryPos, maxSecondary);

        //副轴尺寸收敛到子元素最大副轴长度主轴尺寸保持构造值
        if (_orientation == Orientation.Horizontal)
            _height = maxSecondary;
        else
            _width = maxSecondary;
    }

    public T AddChild<T>(T child) where T : ILayoutElement
        => AddChild(child, NewChildLayoutSettings());

    public T AddChild<T>(T child, LayoutSettings settings) where T : ILayoutElement
    {
        _children.Add(new ChildContainer(child, settings));
        return child;
    }

    public override void VisitChildren(Action<ILayoutElement> visitor)
    {
        foreach (var c in _children) visitor(c.Child);
    }

    public override void RemoveChildren() => _children.Clear();

    //Orientation 方向 Horizontal 水平主轴 Vertical 垂直主轴
    public enum Orientation
    {
        Horizontal,
        Vertical
    }

    //主轴/副轴长度与坐标读写根据 _orientation 分派
    //ChildContainer 的 GetWidth/GetHeight 含 padding 布局计算用此值
    private int GetPrimaryLength(ChildContainer c) => _orientation == Orientation.Horizontal ? c.GetWidth() : c.GetHeight();
    private int GetSecondaryLength(ChildContainer c) => _orientation == Orientation.Horizontal ? c.GetHeight() : c.GetWidth();
    private int GetPrimaryLength(ILayoutElement e) => _orientation == Orientation.Horizontal ? e.Width : e.Height;
    private int GetSecondaryLength(ILayoutElement e) => _orientation == Orientation.Horizontal ? e.Height : e.Width;
    private int GetPrimaryPosition(ILayoutElement e) => _orientation == Orientation.Horizontal ? e.X : e.Y;
    private int GetSecondaryPosition(ILayoutElement e) => _orientation == Orientation.Horizontal ? e.Y : e.X;

    //SetPrimaryPosition 主轴位置已由等间距计算确定 availableSpace=自身长度仅让 align 处理 padding 微调
    private void SetPrimaryPosition(ChildContainer c, int pos)
    {
        if (_orientation == Orientation.Horizontal) c.SetX(pos, c.GetWidth());
        else c.SetY(pos, c.GetHeight());
    }

    //SetSecondaryPosition 副轴方向在 availableSpace 内按 align 对齐
    private void SetSecondaryPosition(ChildContainer c, int pos, int availableSpace)
    {
        if (_orientation == Orientation.Horizontal) c.SetY(pos, availableSpace);
        else c.SetX(pos, availableSpace);
    }

    private sealed class ChildContainer : ChildWrapper
    {
        public ChildContainer(ILayoutElement child, LayoutSettings settings) : base(child, settings) { }
    }
}
