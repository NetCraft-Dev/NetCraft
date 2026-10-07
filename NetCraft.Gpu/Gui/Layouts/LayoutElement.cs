namespace NetCraft.Gpu;

//ILayoutElement minimal layout element interface, maps to vanilla LayoutElement
//GuiControl implements it to fit the layout system; pure layout elements like SpacerElement also implement it
//The layout engine reads/writes element position and size through it without caring about concrete types
public interface ILayoutElement
{
    int X { get; set; }
    int Y { get; set; }
    int Width { get; }
    int Height { get; }

    //SetPosition sets both X/Y, used when the layout engine moves an element
    void SetPosition(int x, int y)
    {
        X = x;
        Y = y;
    }

    //GetRectangle returns the element bounds for layout intersection tests
    GuiRectangle GetRectangle() => new(X, Y, Width, Height);
}
