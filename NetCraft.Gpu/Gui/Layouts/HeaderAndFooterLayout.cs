namespace NetCraft.Gpu;

//HeaderAndFooterLayout 三段式布局对标原版 HeaderAndFooterLayout implements Layout
//header 顶部 footer 底部 content 居中含上边距约束不超出 footer 上沿
//原版依赖 Screen 此处用构造传入 screenWidth/screenHeight 去业务依赖保持 Gpu 层纯净
//addTitleHeader 依赖 Font/Component 省略业务层可自行 addToHeader(new StringWidget)
public sealed class HeaderAndFooterLayout : ILayout
{
    public const int MagicPadding = 13;
    public const int DefaultHeaderAndFooterHeight = 33;
    private const int ContentMarginTop = 30;

    private readonly FrameLayout _headerFrame = new();
    private readonly FrameLayout _footerFrame = new();
    private readonly FrameLayout _contentsFrame = new();
    private readonly int _screenWidth;
    private readonly int _screenHeight;
    private int _headerHeight;
    private int _footerHeight;

    public HeaderAndFooterLayout(int screenWidth, int screenHeight)
        : this(screenWidth, screenHeight, DefaultHeaderAndFooterHeight) { }

    public HeaderAndFooterLayout(int screenWidth, int screenHeight, int headerAndFooterHeight)
        : this(screenWidth, screenHeight, headerAndFooterHeight, headerAndFooterHeight) { }

    public HeaderAndFooterLayout(int screenWidth, int screenHeight, int headerHeight, int footerHeight)
    {
        _screenWidth = screenWidth;
        _screenHeight = screenHeight;
        _headerHeight = headerHeight;
        _footerHeight = footerHeight;
        //header/footer 子元素默认居中对齐
        _headerFrame.DefaultChildLayoutSetting().Align(0.5f, 0.5f);
        _footerFrame.DefaultChildLayoutSetting().Align(0.5f, 0.5f);
    }

    //X/Y 空操作布局锚定屏幕原点不可整体移动
    public int X { get => 0; set { } }
    public int Y { get => 0; set { } }
    public int Width => _screenWidth;
    public int Height => _screenHeight;

    public int HeaderHeight => _headerHeight;
    public int FooterHeight => _footerHeight;
    public void SetHeaderHeight(int height) => _headerHeight = height;
    public void SetFooterHeight(int height) => _footerHeight = height;

    //ContentHeight 屏幕高度减去 header/footer 剩余可用高度
    public int ContentHeight => _screenHeight - _headerHeight - _footerHeight;

    public void VisitChildren(Action<ILayoutElement> visitor)
    {
        _headerFrame.VisitChildren(visitor);
        _contentsFrame.VisitChildren(visitor);
        _footerFrame.VisitChildren(visitor);
    }

    public void RemoveChildren()
    {
        _headerFrame.RemoveChildren();
        _contentsFrame.RemoveChildren();
        _footerFrame.RemoveChildren();
    }

    //ArrangeElements header 贴顶 footer 贴底 content 居中且不越过 footer 上沿
    public void ArrangeElements()
    {
        int headerHeight = _headerHeight;
        int footerHeight = _footerHeight;

        _headerFrame.SetMinWidth(_screenWidth);
        _headerFrame.SetMinHeight(headerHeight);
        _headerFrame.SetPosition(0, 0);
        _headerFrame.ArrangeElements();

        _footerFrame.SetMinWidth(_screenWidth);
        _footerFrame.SetMinHeight(footerHeight);
        _footerFrame.ArrangeElements();
        _footerFrame.Y = _screenHeight - footerHeight;

        _contentsFrame.SetMinWidth(_screenWidth);
        _contentsFrame.ArrangeElements();
        //content 首选 Y=header+上边距但不得越过 footer 上沿
        int preferredContentY = headerHeight + ContentMarginTop;
        int maxContentY = _screenHeight - footerHeight - _contentsFrame.Height;
        _contentsFrame.SetPosition(0, Math.Min(preferredContentY, maxContentY));
    }

    public T AddToHeader<T>(T child) where T : ILayoutElement => _headerFrame.AddChild(child);
    public T AddToFooter<T>(T child) where T : ILayoutElement => _footerFrame.AddChild(child);
    public T AddToContents<T>(T child) where T : ILayoutElement => _contentsFrame.AddChild(child);

    public T AddToHeader<T>(T child, LayoutSettings settings) where T : ILayoutElement
        => _headerFrame.AddChild(child, settings);
    public T AddToFooter<T>(T child, LayoutSettings settings) where T : ILayoutElement
        => _footerFrame.AddChild(child, settings);
    public T AddToContents<T>(T child, LayoutSettings settings) where T : ILayoutElement
        => _contentsFrame.AddChild(child, settings);
}
