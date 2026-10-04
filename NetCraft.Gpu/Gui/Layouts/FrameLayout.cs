namespace NetCraft.Gpu;

//FrameLayout 帧布局对标原版 FrameLayout extends AbstractLayout
//所有子元素在同一矩形内按各自 align 对齐默认居中
//提供静态 centerInRectangle/alignInRectangle 给手动定位场景用
public sealed class FrameLayout : AbstractLayout
{
    private readonly List<ChildContainer> _children = new();
    private int _minWidth;
    private int _minHeight;
    private LayoutSettings _defaultChildSettings;

    public FrameLayout() : this(0, 0, 0, 0) { }

    public FrameLayout(int minWidth, int minHeight) : this(0, 0, minWidth, minHeight) { }

    public FrameLayout(int x, int y, int minWidth, int minHeight) : base(x, y, minWidth, minHeight)
    {
        //默认子元素居中对齐对标原版 defaults().align(0.5,0.5)
        _defaultChildSettings = LayoutSettings.Defaults().Align(0.5f, 0.5f);
        SetMinDimensions(minWidth, minHeight);
    }

    //SetMinWidth/SetMinHeight/SetMinDimensions 链式设置最小尺寸
    //ArrangeElements 结果尺寸不小于此值
    public FrameLayout SetMinWidth(int minWidth) { _minWidth = minWidth; return this; }
    public FrameLayout SetMinHeight(int minHeight) { _minHeight = minHeight; return this; }
    public FrameLayout SetMinDimensions(int minWidth, int minHeight)
        => SetMinWidth(minWidth).SetMinHeight(minHeight);

    public LayoutSettings NewChildLayoutSettings() => _defaultChildSettings.Copy();
    public LayoutSettings DefaultChildLayoutSetting() => _defaultChildSettings;

    //ArrangeElements 取所有子元素最大宽高不小于 minDim 再按 align 在结果矩形内定位
    public override void ArrangeElements()
    {
        base.ArrangeElements();
        int resultWidth = _minWidth;
        int resultHeight = _minHeight;
        foreach (var c in _children)
        {
            resultWidth = Math.Max(resultWidth, c.GetWidth());
            resultHeight = Math.Max(resultHeight, c.GetHeight());
        }
        foreach (var c in _children)
        {
            c.SetX(X, resultWidth);
            c.SetY(Y, resultHeight);
        }
        _width = resultWidth;
        _height = resultHeight;
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

    //CenterInRectangle 将元素居中放到指定矩形内
    public static void CenterInRectangle(ILayoutElement widget, int x, int y, int width, int height)
        => AlignInRectangle(widget, x, y, width, height, 0.5f, 0.5f);

    public static void CenterInRectangle(ILayoutElement widget, GuiRectangle rectangle)
        => CenterInRectangle(widget, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);

    //AlignInRectangle 按 alignX/alignY 将元素对齐到矩形内
    public static void AlignInRectangle(ILayoutElement widget, GuiRectangle rectangle, float alignX, float alignY)
        => AlignInRectangle(widget, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, alignX, alignY);

    public static void AlignInRectangle(ILayoutElement widget, int x, int y, int width, int height, float alignX, float alignY)
    {
        AlignInDimension(x, width, widget.Width, v => widget.X = v, alignX);
        AlignInDimension(y, height, widget.Height, v => widget.Y = v, alignY);
    }

    //AlignInDimension 在 pos 起点 length 长度内按 align 放置 widgetLength 的元素
    //align=0 贴左/上 align=0.5 居中 align=1 贴右/下
    public static void AlignInDimension(int pos, int length, int widgetLength, Action<int> setWidgetPos, float align)
    {
        int offset = (int)Math.Round(Lerp(align, 0.0f, length - widgetLength));
        setWidgetPos(pos + offset);
    }

    private static float Lerp(float t, float a, float b) => a + (b - a) * t;

    //ChildContainer 帧布局子元素容器仅包装 child+settings 不额外存行列
    private sealed class ChildContainer : ChildWrapper
    {
        public ChildContainer(ILayoutElement child, LayoutSettings settings) : base(child, settings) { }
    }
}
