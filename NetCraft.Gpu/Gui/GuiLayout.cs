namespace NetCraft.Gpu;

//GuiLayoutOrientation 线性布局方向
public enum GuiLayoutOrientation
{
    Horizontal,
    Vertical
}

//IGuiLayout 布局引擎接口测量容器子控件排列位置
//GuiContainer.Update 每帧调用 Measure 让子控件跟随容器尺寸自适应排列
public interface IGuiLayout
{
    //Measure 按 layout 策略设置子控件 X/Y 子控件自身 Width/Height 不变
    void Measure(GuiContainer container);
}

//GuiLinearLayout 线性布局水平或垂直堆叠子控件
//Padding 容器内边距 Spacing 子控件间距子控件 Width/Height 保持不变只排 X/Y
//对应原版 client.gui.layouts.LinearLayout
public sealed class GuiLinearLayout : IGuiLayout
{
    public GuiLayoutOrientation Orientation { get; set; }
    public int Padding { get; set; } = 4;
    public int Spacing { get; set; } = 4;

    public GuiLinearLayout(GuiLayoutOrientation orientation = GuiLayoutOrientation.Vertical)
    {
        Orientation = orientation;
    }

    public void Measure(GuiContainer container)
    {
        var x = container.X + Padding;
        var y = container.Y + Padding;
        foreach (var child in container.Children)
        {
            if (!child.Visible) continue;
            child.X = x;
            child.Y = y;
            if (Orientation == GuiLayoutOrientation.Vertical)
                y += child.Height + Spacing;
            else
                x += child.Width + Spacing;
        }
    }
}
