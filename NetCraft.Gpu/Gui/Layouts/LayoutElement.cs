namespace NetCraft.Gpu;

//ILayoutElement 布局元素最小接口对标原版 LayoutElement
//GuiControl 实现此接口适配布局体系 SpacerElement 等纯布局元素也实现
//布局引擎通过此接口读写元素位置尺寸不关心具体类型
public interface ILayoutElement
{
    int X { get; set; }
    int Y { get; set; }
    int Width { get; }
    int Height { get; }

    //SetPosition 同时设 X/Y 布局引擎移动元素时用
    void SetPosition(int x, int y)
    {
        X = x;
        Y = y;
    }

    //GetRectangle 返回元素边界矩形供布局相交判断用
    GuiRectangle GetRectangle() => new(X, Y, Width, Height);
}
