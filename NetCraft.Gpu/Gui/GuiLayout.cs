namespace NetCraft.Gpu;

//GuiLayoutOrientation linear layout orientation
public enum GuiLayoutOrientation
{
    Horizontal,
    Vertical
}

//IGuiLayout layout engine interface measuring container children and arranging their positions
//GuiContainer.Update calls Measure every frame so children reflow with the container size
public interface IGuiLayout
{
    //Measure sets child X/Y per the layout strategy; child Width/Height stay unchanged
    void Measure(GuiContainer container);
}

//GuiLinearLayout linear layout stacking children horizontally or vertically
//Padding container padding Spacing child spacing; child Width/Height stay unchanged, only X/Y is arranged
//corresponds to vanilla client.gui.layouts.LinearLayout
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
