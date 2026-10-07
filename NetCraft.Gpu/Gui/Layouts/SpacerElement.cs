namespace NetCraft.Gpu;

//SpacerElement placeholder spacer element, maps to vanilla SpacerElement implements LayoutElement
//Occupies only width/height, renders nothing and ignores input; the layout engine uses it to open up space
//Width/Height are read-only after construction; X/Y can be moved by the layout engine
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

    //OfWidth/OfHeight static factories creating a one-direction spacer with the other direction 0
    //C# disallows a static method and an instance property sharing a name, so an Of prefix is used, maps to vanilla width(int)/height(int)
    public static SpacerElement OfWidth(int width) => new(width, 0);
    public static SpacerElement OfHeight(int height) => new(0, height);
}
