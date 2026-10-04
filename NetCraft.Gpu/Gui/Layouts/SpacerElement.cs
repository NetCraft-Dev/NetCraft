namespace NetCraft.Gpu;

//SpacerElement 占位空白元素对标原版 SpacerElement implements LayoutElement
//仅占 width/height 不渲染不响应输入布局引擎用它撑开空间
//Width/Height 构造后只读 X/Y 可由布局引擎移动
public sealed class SpacerElement : ILayoutElement
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; }
    public int Height { get; }

    public SpacerElement(int width, int height) : this(0, 0, width, height) { }

    public SpacerElement(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    //OfWidth/OfHeight 静态工厂创建单方向占位元素另一方向为 0
    //C# 不允许静态方法和实例属性同名改用 Of 前缀对标原版 width(int)/height(int)
    public static SpacerElement OfWidth(int width) => new(width, 0);
    public static SpacerElement OfHeight(int height) => new(0, height);
}
