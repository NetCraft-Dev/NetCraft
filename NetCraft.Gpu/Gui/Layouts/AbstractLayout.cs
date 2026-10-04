namespace NetCraft.Gpu;

//AbstractLayout 布局基类对标原版 AbstractLayout implements Layout
//持有 x/y/width/height X/Y setter 偏移所有子元素保持相对位置
//子类实现 VisitChildren/RemoveChildren 并 override ArrangeElements 加自身布局逻辑
//ChildWrapper 包装子元素+LayoutSettings SetX/SetY 用 lerp 计算 align 偏移
public abstract class AbstractLayout : ILayout
{
    private int _x;
    private int _y;
    protected int _width;
    protected int _height;

    protected AbstractLayout(int x, int y, int width, int height)
    {
        _x = x;
        _y = y;
        _width = width;
        _height = height;
    }

    //X setter 偏移所有子元素 dx 保持相对位置再设自身 x
    //布局引擎移动整个 Layout 时子元素跟随不脱节
    public int X
    {
        get => _x;
        set
        {
            int dx = value - _x;
            if (dx != 0) VisitChildren(child => child.X += dx);
            _x = value;
        }
    }

    public int Y
    {
        get => _y;
        set
        {
            int dy = value - _y;
            if (dy != 0) VisitChildren(child => child.Y += dy);
            _y = value;
        }
    }

    public int Width => _width;
    public int Height => _height;

    //SetPosition 同时设 X/Y 便利方法对标原版 LayoutElement.setPosition
    //C# 接口默认方法不能通过实例直接调用故在此提供实例实现
    public void SetPosition(int x, int y)
    {
        X = x;
        Y = y;
    }

    //ArrangeElements 默认递归子 Layout 子类 override 加自身布局逻辑后调 base
    //C# 接口默认方法不能通过实例直接调用故 AbstractLayout 提供实例实现
    public virtual void ArrangeElements()
    {
        VisitChildren(child =>
        {
            if (child is ILayout layout)
                layout.ArrangeElements();
        });
    }

    public abstract void VisitChildren(Action<ILayoutElement> visitor);
    public abstract void RemoveChildren();

    //ChildWrapper 包装子元素+LayoutSettings 提供 GetWidth/GetHeight 含 padding
    //SetX/SetY 在 availableSpace 内按 align lerp 计算偏移
    //paddingLeft 固定起点 paddingRight 固定终点 align=0 左对齐 0.5 居中 1 右对齐
    protected abstract class ChildWrapper
    {
        public readonly ILayoutElement Child;
        public readonly LayoutSettings.Impl Settings;

        protected ChildWrapper(ILayoutElement child, LayoutSettings settings)
        {
            Child = child;
            Settings = settings.GetExposed();
        }

        //GetHeight 含上下 padding 布局引擎算行高用
        public int GetHeight() => Child.Height + Settings.PaddingTopValue + Settings.PaddingBottomValue;

        //GetWidth 含左右 padding 布局引擎算列宽用
        public int GetWidth() => Child.Width + Settings.PaddingLeftValue + Settings.PaddingRightValue;

        //SetX 在 x 起点的 availableSpace 内按 align 计算子元素 x 偏移
        //least=paddingLeft 起点 most=availableSpace-childWidth-paddingRight 终点
        //align=0 贴左 paddingLeft align=0.5 居中 align=1 贴右 paddingRight
        public void SetX(int x, int availableSpace)
        {
            float least = Settings.PaddingLeftValue;
            float most = availableSpace - Child.Width - Settings.PaddingRightValue;
            int offset = (int)Math.Round(Lerp(Settings.XAlignment, least, most));
            Child.X = offset + x;
        }

        public void SetY(int y, int availableSpace)
        {
            float least = Settings.PaddingTopValue;
            float most = availableSpace - Child.Height - Settings.PaddingBottomValue;
            int offset = (int)Math.Round(Lerp(Settings.YAlignment, least, most));
            Child.Y = offset + y;
        }

        private static float Lerp(float t, float a, float b) => a + (b - a) * t;
    }
}
